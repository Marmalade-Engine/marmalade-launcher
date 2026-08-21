using System.CommandLine;
using MarmaladeLauncher.Models;
using MarmaladeLauncher.Services;
using MarmaladeLauncher.Utils;

namespace MarmaladeLauncher.CLI.Commands;

public static class InstallEngineCommand {
    public static async Task<int> Install(
        string? version,
        string? customOutputDir,
        bool search,
        bool skipConfirm,
        SettingsService settingsService,
        InstallationRegistryService installationRegistryService,
        EngineInstallerService engineInstallerService) {
        
        if (search || string.IsNullOrEmpty(version)) {
            await SearchDownloadsCommand.SearchAvailableEngines(installationRegistryService);

            return 0;
        }

        var (availableEngines, versionEntryMap) = await installationRegistryService.FetchEngineVersionsAsync();

        var targetEngine = availableEngines.FirstOrDefault(x =>
            string.Equals(x.Version, version!, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(x.Name, version!, StringComparison.OrdinalIgnoreCase));

        if (targetEngine == null) {
            Console.WriteLine($"Unable to find engine version {version}");
            return 1;
        }

        if (!versionEntryMap.TryGetValue(targetEngine, out var entry)) {
            Console.WriteLine($"Error mapping version `{version}` to remote manifest");
            return 1;
        }

        string targetDirectory = !string.IsNullOrWhiteSpace(customOutputDir)
            ? customOutputDir
            : (!string.IsNullOrWhiteSpace(settingsService.Settings.DefaultInstallLocation)
                ? settingsService.Settings.DefaultInstallLocation
                : Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    SettingsService.DefaultBaseDirectory,
                    "installations"));

        string installSize = ByteFormatter.FormatSize(entry.size);

        if (!skipConfirm) {
            Console.WriteLine(
                $"Are you sure you want to install version: '{entry.ResolvedVersion}', in directory: '{targetDirectory}'?\nThis will take {installSize} of space! [y/N]: ");

            string response = Console.ReadLine()?.Trim();

            if (!string.Equals(response, "y", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(response, "yes", StringComparison.OrdinalIgnoreCase)) {
                Console.WriteLine("Installation cancelled.");
                return 1;
            }
        }
        
        var progress = new Progress<double>(val => {
            Console.Write($"\rInstalling engine '{entry.ResolvedVersion}' {val,5:F1}%");
        });
        
        LocalEngineInstallation? installedEngine = await engineInstallerService.InstallEngine(entry, targetDirectory, progress);
        
        if (installedEngine != null) {
            var currentInstallations = (await installationRegistryService.LoadInstallations()).ToList();

            bool exists = currentInstallations.Any(i => 
                string.Equals(i.ExecutablePath, installedEngine.ExecutablePath, StringComparison.OrdinalIgnoreCase));

            if (!exists) {
                currentInstallations.Add(installedEngine);
                await installationRegistryService.SaveInstallations(currentInstallations);
            }

            Console.WriteLine();
            Console.WriteLine($"Successfully installed engine version: '{installedEngine.Name}'. Execuable Path: `{installedEngine.ExecutablePath}`");
        } else {
            Console.WriteLine("Installation failed!");
        }
        return 0;
    }
}