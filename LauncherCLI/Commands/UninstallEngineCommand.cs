using MarmaladeLauncher.Common.Utils;
using MarmaladeLauncher.Services;

namespace MarmaladeLauncher.CLI.Commands;

public static class UninstallEngineCommand {
    public static async Task<int> Uninstall(
        string? target,
        bool skipConfirm,
        InstallationRegistryService installationRegistryService,
        EngineInstallerService engineInstallerService) {

        var installations = await installationRegistryService.LoadInstallations();
        
        if (string.IsNullOrEmpty(target)) {
            await ListInstallsCommand.ListInstalls(installationRegistryService);
            return 0;
        }
        
        var engine = installations.FirstOrDefault(x =>
            x.Name.Equals(target, StringComparison.OrdinalIgnoreCase) ||
            x.Version.Equals(target, StringComparison.OrdinalIgnoreCase));

        if (engine == null) {
            Console.WriteLine($"Engine '{target}' not found.");
            return 1;
        }

        if (!skipConfirm) {
            Console.Write($"Are you sure you want to uninstall engine '{engine.Name}'? [y/N]: ");
            string response = Console.ReadLine()?.Trim();
            
            if (!string.Equals(response, "y", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(response, "yes", StringComparison.OrdinalIgnoreCase)) {
                Console.WriteLine("Uninstallation cancelled.");
                return 1;
            }
        }

        IProgress<double> progress = new SynchronousProgress<double>(val => { 
            Console.Write($"\rUninstalling engine: {val,3:F0}%");
        });
        
        try {
            await engineInstallerService.UninstallEngineAsync(engine, progress);
            
            Console.WriteLine();
            Console.WriteLine($"Successfully uninstalled '{engine.Name}'");
            return 0;
        }
        catch (Exception e) {
            Console.WriteLine($"\nFailed to uninstall '{engine.Name}': {e.Message}");
            return 1;
        }
    }
}