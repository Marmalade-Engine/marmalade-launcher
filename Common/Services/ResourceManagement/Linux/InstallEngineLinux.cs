using System.Runtime.InteropServices;
using MarmaladeLauncher.Common.Utils;
using MarmaladeLauncher.Models;
using MarmaladeLauncher.Services.ResourceManagement.Common;

namespace MarmaladeLauncher.Services.ResourceManagement.Linux;

public class InstallEngineLinux : IInstallEngine {
    public OSPlatform Platform => OSPlatform.Linux;

    public async Task<string> ProcessDownloadedFile(string filePath, string targetDirectory, RemoteBuildEntry entry) {
        if (!File.Exists(filePath)) {
            throw new FileNotFoundException($"Downloaded engine not found at: {filePath}");
        }

        Directory.CreateDirectory(targetDirectory);

        string nameContext = filePath;
        if (!string.IsNullOrWhiteSpace(entry.url)) {
            if (Uri.TryCreate(entry.url, UriKind.Absolute, out var uri)) {
                var query = System.Web.HttpUtility.ParseQueryString(uri.Query);
                var platform = query["platform"];

                if (platform == "linux-tar" || platform == "tgz") {
                    nameContext = "archive.tar.gz";
                }
                else if (platform == "zip") {
                    nameContext = "archive.zip";
                }
                else {
                    nameContext = Path.GetFileName(uri.AbsolutePath);
                }
            }
            else {
                nameContext = entry.url;
            }
        }

        string execPath;

        if (filePath.EndsWith(".AppImage", StringComparison.OrdinalIgnoreCase) ||
            filePath.EndsWith(".appimage", StringComparison.OrdinalIgnoreCase)) {
            var fileName = Path.GetFileName(filePath);
            execPath = Path.Combine(targetDirectory, fileName);

            File.Copy(filePath, execPath, true);
        }
        else {
            await ArchiveUtils.ExtractArchive(filePath, targetDirectory, nameContext);
            execPath = FindExecutablePath(targetDirectory, entry);
        }

        SetPlatformPermissions(execPath);

        return execPath;
    }

    public void SetPlatformPermissions(string execPath) {
        if (!File.Exists(execPath)) return;

        string targetDirectory = Path.GetDirectoryName(execPath) ?? string.Empty;
        if (string.IsNullOrEmpty(targetDirectory) || !Directory.Exists(targetDirectory)) return;

            var allFiles = Directory.GetFiles(targetDirectory, "*", SearchOption.AllDirectories);

        foreach (var file in allFiles) {
            try {
                var currentMode = File.GetUnixFileMode(file);
                var executePermissions = UnixFileMode.UserExecute | UnixFileMode.GroupExecute | UnixFileMode.OtherExecute;
                var readWritePermissions = UnixFileMode.UserRead | UnixFileMode.UserWrite |
                                           UnixFileMode.GroupRead | UnixFileMode.OtherRead;

                File.SetUnixFileMode(file, currentMode | executePermissions | readWritePermissions);
            }
            catch (Exception ex) {
                Console.WriteLine($"Failed setting Unix file permissions for {file}: {ex.Message}");
            }
        }
    }

    private static string FindExecutablePath(string targetDirectory, RemoteBuildEntry entry) {
        var files = Directory.GetFiles(targetDirectory, "*", SearchOption.AllDirectories);

        if (!string.IsNullOrEmpty(entry.name)) {
            var exactMatch = files.FirstOrDefault(f =>
                Path.GetFileName(f).Equals(entry.name, StringComparison.OrdinalIgnoreCase) ||
                f.EndsWith(entry.name, StringComparison.OrdinalIgnoreCase));

            if (exactMatch != null && File.Exists(exactMatch)) {
                return exactMatch;
            }
        }

        var candidateFiles = files.Where(f => {
            var name = Path.GetFileName(f);
            return !name.StartsWith("temp_", StringComparison.OrdinalIgnoreCase) &&
                   !name.StartsWith('.') &&
                   !name.EndsWith(".debug", StringComparison.OrdinalIgnoreCase) &&
                   !name.EndsWith(".so", StringComparison.OrdinalIgnoreCase) &&
                   !name.EndsWith(".json", StringComparison.OrdinalIgnoreCase);
        }).ToList();

        var binary = candidateFiles.FirstOrDefault(f =>
            f.EndsWith(".x86_64", StringComparison.OrdinalIgnoreCase) ||
            f.EndsWith(".AppImage", StringComparison.OrdinalIgnoreCase));

        if (binary != null) return binary;

        var executableCandidates = candidateFiles.Where(f => {
            var ext = Path.GetExtension(f);
            if (!string.IsNullOrEmpty(ext)) return false;

            var mode = File.GetUnixFileMode(f);
            return (mode & (UnixFileMode.UserExecute | UnixFileMode.GroupExecute | UnixFileMode.OtherExecute)) != 0;
        }).ToList();

        binary = executableCandidates.FirstOrDefault(f =>
                     Path.GetFileName(f).Equals("marmalade", StringComparison.OrdinalIgnoreCase))
                 ?? executableCandidates.FirstOrDefault();

        return binary ??
               throw new FileNotFoundException($"Could not determine main engine binary inside {targetDirectory}");
    }
}