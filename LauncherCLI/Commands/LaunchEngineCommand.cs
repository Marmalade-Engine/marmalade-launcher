using MarmaladeLauncher.Services;

namespace MarmaladeLauncher.CLI.Commands;

public static class LaunchEngineCommand {
    public static async Task<int> Launch(
        string target,
        string[]? launchArgs,
        InstallationRegistryService installationRegistryService,
        LaunchService launchService) {

        var installations = await installationRegistryService.LoadInstallations();
        
        var engine = installations.FirstOrDefault(x =>
            x.Name.Equals(target, StringComparison.OrdinalIgnoreCase) ||
            x.Version.Equals(target, StringComparison.OrdinalIgnoreCase));

        if (engine == null) {
            Console.WriteLine($"Engine '{target}' not found.");
            return 1;
        }

        Console.WriteLine($"Launching {engine.Name} ({engine.ExecutablePath})");

        await launchService.LaunchAsync(engine, launchArgs);
        
        return 0;
    }
}