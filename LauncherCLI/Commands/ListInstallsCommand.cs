using System.CommandLine;
using MarmaladeLauncher.Services;

namespace MarmaladeLauncher.CLI.Commands;

public class ListInstallsCommand : Command {
    public ListInstallsCommand() : base("list-installs",
        "Show list of installed engine versions") {
        
        this.SetAction(async _ => {
            var installationService = new InstallationRegistryService();
            
            await ListInstalls(installationService);
        });
    }

    public static async Task ListInstalls(InstallationRegistryService installationRegistryService) {
        var installations = await installationRegistryService.LoadInstallations();

        if (!installations.Any()) {
            Console.WriteLine("No installations found");
            return;
        }
        
        Console.WriteLine($"{"Name",-25} {"Version",-15} {"Path"}");
        Console.WriteLine(new string('-', 128));

        foreach (var item in installations) {
            Console.WriteLine(
                $"{(string)item.Name,-25} {(string)item.Version,-15} {(string)item.ExecutablePath}");
        }
    }
}