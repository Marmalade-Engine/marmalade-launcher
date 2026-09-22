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
using MarmaladeLauncher.Services.ResourceManagement.Common;

namespace MarmaladeLauncher.Services;

public class EngineInstallerService {
    private readonly InstallationRegistryService _installationRegistryService;
    private readonly SettingsService _settingsService;
    private readonly IFileDownloader _fileDownloader;
    private readonly IPlatformEngineResolver _engineResolver;

    public EngineInstallerService(
        InstallationRegistryService installationRegistryService,
        SettingsService settingsService,
        IFileDownloader fileDownloader,
        IPlatformEngineResolver engineResolver
    ) {
        _installationRegistryService = installationRegistryService;
        _settingsService = settingsService;
        _fileDownloader = fileDownloader;
        _engineResolver = engineResolver;
    }

    public async Task<LocalEngineInstallation?> InstallEngine(
        RemoteBuildEntry entry,
        string targetDirectory,
        IProgress<double>? progress = null,
        CancellationToken cancellationToken = default
    ) {
        if (string.IsNullOrEmpty(entry.url)) {
            throw new ArgumentException("Download URL cannot be empty: ", nameof(entry.url));
        }

        string resolvedEngineVersion = entry.ResolvedVersion;

        string installDirectory = Path.Combine(targetDirectory, Guid.NewGuid().ToString());
        Directory.CreateDirectory(installDirectory);

        string fileExt = UrlPathUtils.GetExtensionFromUrl(entry.url);
        string tmpFilePath = Path.Combine(installDirectory, $"temp_{Guid.NewGuid()}{fileExt}");

        try {
            await _fileDownloader.DownloadFileAsync(entry.url, tmpFilePath, progress, cancellationToken);

            var installEngine = _engineResolver.GetInstallEngine();
            string finalExecPath = await installEngine.ProcessDownloadedFile(tmpFilePath, installDirectory, entry);

            string relativeExecPath = UrlPathUtils.GetRelativePath(targetDirectory, finalExecPath);
            await IInstallEngine.WriteInstallInfo(installDirectory, entry, relativeExecPath);

            installEngine.SetPlatformPermissions(finalExecPath);
            
            var existingList = await _installationRegistryService.LoadInstallations();
            string displayName = FormatDisplayName(entry, resolvedEngineVersion, existingList);

            var newInstall = new LocalEngineInstallation {
                Name = displayName,
                Version = resolvedEngineVersion,
                ExecutablePath = relativeExecPath,
                InstallSize = entry.size,
                DateAdded = DateTime.Now
            };

            existingList.RemoveAll(i =>
                i.ExecutablePath.Equals(finalExecPath, StringComparison.OrdinalIgnoreCase) ||
                i.ExecutablePath.Equals(relativeExecPath, StringComparison.OrdinalIgnoreCase));

            existingList.Add(newInstall);
            await _installationRegistryService.SaveInstallations(existingList);

            return newInstall;
        }
        catch (Exception ex) {
            Console.WriteLine($"Engine installation failed: {ex}");
            if (Directory.Exists(installDirectory)) {
                try {
                    Directory.Delete(installDirectory, true);
                }
                catch {
                }
            }

            throw;
        }
        finally {
            if (File.Exists(tmpFilePath)) {
                try {
                    File.Delete(tmpFilePath);
                }
                catch {
                }
            }
        }
    }

    public async Task UninstallEngine(LocalEngineInstallation entry, IProgress<double>? progress = null,
        CancellationToken cancellationToken = default) {

        string targetPath = entry.GetResolvedExecutablePath(_settingsService.Settings.DefaultInstallLocation);
        
        var uninstallEngine = _engineResolver.GetUninstallEngine();
        
        await IUninstallEngine.EnsureNotRunning(targetPath);
        
        progress?.Report(30.0f);
        
        await uninstallEngine.UninstallFiles(targetPath);
        
        progress?.Report(80.0f);
        
        var existingList = await _installationRegistryService.LoadInstallations();
        existingList.RemoveAll(i => i.ExecutablePath.Equals(entry.ExecutablePath, StringComparison.OrdinalIgnoreCase));
        await _installationRegistryService.SaveInstallations(existingList);

        progress?.Report(100);
    }

    private static string FormatDisplayName(RemoteBuildEntry entry, string version,
        List<LocalEngineInstallation> existing) {
        string displayName = !string.IsNullOrWhiteSpace(entry.name) ? entry.name : $"Marmalade {version}";
        int duplicateCount = existing.FindAll(i => i.Version.Equals(version, StringComparison.OrdinalIgnoreCase)).Count;
        return duplicateCount > 0 ? $"{displayName} ({duplicateCount + 1})" : displayName;
    }
}