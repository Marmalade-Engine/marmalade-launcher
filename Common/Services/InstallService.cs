using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using MarmaladeLauncher.Models;

namespace MarmaladeLauncher.Services;

public class InstallService {
    private readonly HttpClient _httpClient;
    private readonly InstallationService _installationService;
    private readonly SettingsService _settingsService;

    public InstallService(InstallationService installationService, SettingsService settingsService,
        HttpClient? httpClient = null) {
        _installationService = installationService;
        _settingsService = settingsService;
        _httpClient = httpClient ?? new HttpClient();
    }

    /// <summary>
    /// Download, extract, and register a new engine build
    /// </summary>
    public async Task<EngineInstallation?> InstallEngine(
        InstallationEntry entry,
        string targetDirectory,
        IProgress<double>? progress = null,
        CancellationToken cancellationToken = default) {
        if (string.IsNullOrEmpty(entry.url)) {
            throw new ArgumentException("Download URL cannot be empty.", nameof(entry));
        }

        string resolvedVersion = entry.ResolvedVersion;

        string installDirectory = Path.Combine(targetDirectory, Guid.NewGuid().ToString());
        Directory.CreateDirectory(installDirectory);

        string fileExtension = GetExtensionFromUrl(entry.url);
        string tempFilePath = Path.Combine(installDirectory, $"temp_{Guid.NewGuid()}{fileExtension}");

        try {
            await DownloadFile(entry.url, tempFilePath, progress, cancellationToken);

            string finalExecutablePath = await ProcessDownloadedFile(tempFilePath, installDirectory, entry);

            string relativeExecutablePath = GetRelativeInstallPath(targetDirectory, finalExecutablePath);

            await WriteInstallInfo(installDirectory, entry, relativeExecutablePath);

            string displayName = !string.IsNullOrWhiteSpace(entry.name)
                ? entry.name
                : $"Marmalade {resolvedVersion}";

            var existingList = await _installationService.LoadInstallations();

            int duplicateCount = existingList
                .FindAll(i => i.Version.Equals(resolvedVersion, StringComparison.OrdinalIgnoreCase)).Count;
            if (duplicateCount > 0) {
                displayName = $"{displayName} ({duplicateCount + 1})";
            }

            var newInstall = new EngineInstallation {
                Name = displayName,
                Version = resolvedVersion,
                ExecutablePath = relativeExecutablePath,
                InstallSize = entry.size,
                DateAdded = DateTime.Now
            };

            existingList.RemoveAll(i =>
                i.ExecutablePath.Equals(finalExecutablePath, StringComparison.OrdinalIgnoreCase));
            existingList.Add(newInstall);
            await _installationService.SaveInstallations(existingList);

            return newInstall;
        }
        catch {
            if (Directory.Exists(installDirectory)) {
                try {
                    Directory.Delete(installDirectory, true);
                }
                catch { }
            }

            throw;
        }
        finally {
            if (File.Exists(tempFilePath)) {
                try {
                    File.Delete(tempFilePath);
                }
                catch { }
            }
        }
    }

    /// <summary>
    /// Get relative exec path
    /// </summary>
    /// <param name="baseTargetDir"></param>
    /// <param name="absExecPath"></param>
    /// <returns></returns>
    private string GetRelativeInstallPath(string baseTargetDir, string absExecPath) {
        string pathTarget = absExecPath;
        int appIndex = pathTarget.IndexOf(".app", StringComparison.OrdinalIgnoreCase);
        if (appIndex != -1) {
            pathTarget = pathTarget.Substring(0, appIndex + 4);
        }

        string? parentDir =
            Path.GetDirectoryName(baseTargetDir.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        if (!string.IsNullOrEmpty(parentDir)) {
            string relative = Path.GetRelativePath(parentDir, pathTarget);
            return relative.Replace('\\', '/');
        }

        return pathTarget.Replace('\\', '/');
    }

    /// <summary>
    /// Terminate active instances of the target engine, remove it files in the file system, and unregister the engine
    /// </summary>
    public async Task UninstallEngineAsync(EngineInstallation entry, IProgress<double>? progress = null,
        CancellationToken cancellationToken = default) {
        Console.WriteLine($"Uninstalling {entry.Name}...");

        if (entry == null || string.IsNullOrWhiteSpace(entry.ExecutablePath)) {
            throw new ArgumentNullException(nameof(entry), "Invalid engine installation record");
        }

        progress?.Report(10.0);

        string targetPath = entry.GetResolvedExecutablePath(_settingsService.Settings.DefaultInstallLocation);

        await EnsureNotRunningAsync(targetPath);

        progress?.Report(30.0f);

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) {
            await UninstallWindowsEngine(targetPath);
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX)) {
            await UninstallMacEngine(targetPath);
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux)) {
            await UninstallLinuxEngine(targetPath);
        }

        progress?.Report(80.0f);

        var existingList = await _installationService.LoadInstallations();
        existingList.RemoveAll(i => i.ExecutablePath.Equals(entry.ExecutablePath, StringComparison.OrdinalIgnoreCase));
        await _installationService.SaveInstallations(existingList);

        progress?.Report(100);
    }

    /// <summary>
    /// Checks for and terminates active processes
    /// </summary>
    private async Task EnsureNotRunningAsync(string execPath) {
        string processName = Path.GetFileNameWithoutExtension(execPath);
        var runningProcesses = Process.GetProcessesByName(processName);

        foreach (var proc in runningProcesses) {
            try {
                if (proc.MainModule?.FileName.Equals(execPath, StringComparison.OrdinalIgnoreCase) == true) {
                    proc.Kill();
                    await proc.WaitForExitAsync();
                }
            }
            catch (Exception ex) {
                Debug.WriteLine($"Could not terminate process {processName}: {ex.Message}");
            }
        }
    }

    private async Task UninstallWindowsEngine(string execPath) {
        throw new NotImplementedException();
    }
    
    /// <summary>
    /// Perform MacOS-specific engine uninstallation process
    /// </summary>
    private async Task UninstallMacEngine(string execPath) {
        string? appBundleDir = GetContainingAppBundle(execPath);
        string rootDirToDir = appBundleDir ?? Path.GetDirectoryName(execPath)!;

        string? parentDir = Path.GetDirectoryName(rootDirToDir);

        if (Directory.Exists(rootDirToDir)) {
            Directory.Delete(rootDirToDir, true);
        }
        else if (File.Exists(execPath)) {
            File.Delete(execPath);
        }

        if (!string.IsNullOrEmpty(parentDir) && Directory.Exists(parentDir) &&
            Directory.GetFileSystemEntries(parentDir).Length == 0) {
            Directory.Delete(parentDir, recursive: true);
        }

        await Task.CompletedTask;
    }

    private async Task UninstallLinuxEngine(string execPath) {
        throw new NotImplementedException();
    }

    /// <summary>
    /// Resolve the encolosing <c>.app</c> directory path
    /// </summary>
    /// <param name="path"></param>
    /// <returns></returns>
    private string? GetContainingAppBundle(string path) {
        int appIndex = path.IndexOf(".app", StringComparison.OrdinalIgnoreCase);
        if (appIndex != -1) {
            string bundlePath = path.Substring(0, appIndex + 4);
            if (Directory.Exists(bundlePath)) {
                return bundlePath;
            }
        }

        return null;
    }

    /// <summary>
    /// Streams remote files to disk
    /// </summary>
    private async Task DownloadFile(string url, string destinationPath, IProgress<double>? progress,
        CancellationToken cancellationToken) {
        using var response =
            await _httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        response.EnsureSuccessStatusCode();

        long totalBytes = response.Content.Headers.ContentLength ?? -1L;
        using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var fileStream =
            new FileStream(destinationPath, FileMode.Create, FileAccess.Write, FileShare.Read, 8192, true);

        var buffer = new byte[8192];
        long totalBytesRead = 0;
        int bytesRead;

        while ((bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length, cancellationToken)) > 0) {
            await fileStream.WriteAsync(buffer, 0, bytesRead, cancellationToken);
            totalBytesRead += bytesRead;

            if (totalBytes > 0 && progress != null) {
                progress.Report((double)totalBytesRead / totalBytes * 100);
            }
        }
    }

    /// <summary>
    /// Handle post download file placements, extract bundles
    /// </summary>
    private async Task<string> ProcessDownloadedFile(string filePath, string targetDirectory, InstallationEntry entry) {
        string ext = GetExtensionFromUrl(entry.url).ToLowerInvariant();

        if (string.IsNullOrEmpty(ext) && RuntimeInformation.IsOSPlatform(OSPlatform.OSX)) {
            ext = ".dmg";
        }

        if (ext == ".dmg") {
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.OSX)) {
                throw new PlatformNotSupportedException("Extracting .dmg files is only supported on macOS");
            }

            return await ProcessDmgFile(filePath, targetDirectory);
        }

        string destinationFileName = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? "MarmaladeEngine.exe"
            : "MarmaladeEngine";
        string fallbackPath = Path.Combine(targetDirectory, destinationFileName);

        if (File.Exists(fallbackPath)) File.Delete(fallbackPath);
        File.Move(filePath, fallbackPath);

        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) {
            SetUnixExecutablePermissions(fallbackPath);
        }

        return fallbackPath;
    }

    /// <summary>
    /// Applies unix execute permissions
    /// </summary>
    /// <param name="filePath"></param>
    private void SetUnixExecutablePermissions(string filePath) {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) return;

        try {
            using var process = new Process {
                StartInfo = new ProcessStartInfo {
                    FileName = "chmod",
                    Arguments = $"+x \"{filePath}\"",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };
            process.Start();
            process.WaitForExit();
        }
        catch (Exception e) {
            Console.WriteLine($"Failed to set permissions via chmod: {e.Message}");
        }
    }

    /// <summary>
    /// Process the MacOS <c>.dmg</c> by mounting it and copying the files over
    /// </summary>
    private async Task<string> ProcessDmgFile(string filePath, string targetDirectory) {
        string mountPoint = Path.Combine(Path.GetTempPath(), $"marmalade_mount_{Guid.NewGuid()}");
        Directory.CreateDirectory(mountPoint);

        try {
            var mountResult = await RunProcess("/usr/bin/hdiutil",
                $"attach \"{filePath}\" -mountpoint \"{mountPoint}\" -nobrowse -quiet");

            if (mountResult.ExitCode != 0) {
                throw new InvalidOperationException(
                    $"Failed to mount DMG file. Exit code: {mountResult.ExitCode}. Error: {mountResult.Error}");
            }

            var appBundles = Directory.GetDirectories(mountPoint, "*.app", SearchOption.TopDirectoryOnly);
            if (appBundles.Length == 0) {
                appBundles = Directory.GetDirectories(mountPoint, "*.app", SearchOption.AllDirectories);
            }

            if (appBundles.Length == 0) {
                throw new FileNotFoundException("No .app bundle was found inside the mounted DMG.");
            }

            string sourceAppPath = appBundles[0];
            string appName = Path.GetFileName(sourceAppPath);
            string destinationAppPath = Path.Combine(targetDirectory, appName);

            if (Directory.Exists(destinationAppPath)) {
                Directory.Delete(destinationAppPath, true);
            }

            var copyResult = await RunProcess("/bin/cp", $"-R \"{sourceAppPath}\" \"{targetDirectory}/\"");

            if (copyResult.ExitCode != 0) {
                throw new InvalidOperationException(
                    $"Failed to copy .app bundle. Exit code: {copyResult.ExitCode}. Error: {copyResult.Error}");
            }

            string bundleNameWithoutExt = Path.GetFileNameWithoutExtension(appName);
            string innerBinaryPath = Path.Combine(destinationAppPath, "Contents", "MacOS", bundleNameWithoutExt);

            if (File.Exists(innerBinaryPath)) {
                SetUnixExecutablePermissions(innerBinaryPath);
                
                return innerBinaryPath;
            }

            string macOsDir = Path.Combine(destinationAppPath, "Contents", "MacOS");
            if (Directory.Exists(macOsDir)) {
                var files = Directory.GetFiles(macOsDir);
                if (files.Length > 0) {
                    SetUnixExecutablePermissions(files[0]);
                    return files[0];
                }
            }

            return destinationAppPath;
        }
        finally {
            await RunProcess("/usr/bin/hdiutil", $"detach \"{mountPoint}\" -force -quiet");

            if (Directory.Exists(mountPoint)) {
                try {
                    Directory.Delete(mountPoint, recursive: true);
                }
                catch { }
            }
        }
    }

    /// <summary>
    /// Runs a given process
    /// </summary>
    private static async Task<(int ExitCode, string Output, string Error)>
        RunProcess(string fileName, string arguments) {
        var startInfo = new ProcessStartInfo {
            FileName = fileName,
            Arguments = arguments,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        using var process = new Process { StartInfo = startInfo };
        process.Start();

        string output = await process.StandardOutput.ReadToEndAsync();
        string error = await process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();

        return (process.ExitCode, output, error);
    }
    
    /// <summary>
    /// Infer file extenssion from url
    /// </summary>
    /// <param name="url"></param>
    /// <returns></returns>
    private static string GetExtensionFromUrl(string url) {
        try {
            var uri = new Uri(url);
            string path = uri.AbsolutePath;

            if (path.EndsWith(".dmg", StringComparison.OrdinalIgnoreCase)) return ".dmg";
            if (path.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)) return ".exe";
            if (path.EndsWith(".appimage", StringComparison.OrdinalIgnoreCase)) return ".appimage";

            if (uri.Query.Contains("platform=macos", StringComparison.OrdinalIgnoreCase) ||
                uri.Query.Contains("platform=osx", StringComparison.OrdinalIgnoreCase)) {
                return ".dmg";
            }

            if (uri.Query.Contains("platform=windows", StringComparison.OrdinalIgnoreCase)) {
                return ".exe";
            }

            return Path.GetExtension(path);
        }
        catch {
            return string.Empty;
        }
    }

    /// <summary>
    /// Get a query param from a given url string
    /// </summary>
    private static string GetQueryParam(string url, string paramName, string fallback = "unknown") {
        try {
            var uri = new Uri(url);
            var query = HttpUtility.ParseQueryString(uri.Query);
            return query[paramName] ?? fallback;
        }
        catch (Exception e) {
            return fallback;
        }
    }

    /// <summary>
    /// Write the info json file for an installation
    /// </summary>
    private static async Task WriteInstallInfo(
        string installDir,
        InstallationEntry entry,
        string execPath,
        string? checksum = null) {
        var info = new InstallInfo {
            Branch = GetQueryParam(entry.url, "branch", "main"),
            Platform = GetQueryParam(entry.url, "platform", RuntimeInformation.OSDescription),
            Version = entry.ResolvedVersion,
            BuildId = entry.id,
            ExecutablePath = execPath,
            DownloadUrl = entry.url,
            Checksum = checksum,
            InstalledAt = DateTime.Now
        };

        string jsonPath = Path.Combine(installDir, "install_info.json");
        var options = new JsonSerializerOptions { WriteIndented = true };

        await File.WriteAllTextAsync(jsonPath, JsonSerializer.Serialize(info, options));
    }
}