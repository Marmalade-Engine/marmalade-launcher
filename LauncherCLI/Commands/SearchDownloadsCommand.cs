using System.CommandLine;
using MarmaladeLauncher.Services;

namespace MarmaladeLauncher.CLI.Commands;

public class SearchDownloadsCommand : Command {
    private readonly InstallationService _installationService;
    
    public SearchDownloadsCommand(InstallationService installationService) : base("search", "Show list of engine versions available for download") {
        _installationService = installationService;
        
        this.SetAction(_ => SearchAvailableEngines(_installationService));
    }

    public static async Task SearchAvailableEngines(InstallationService installationService) {
        var (installations, _) = await installationService.FetchEngineVersionsAsync();

        if (installations.Count == 0) {
            Console.WriteLine(
                "There are no available installations available for download, please try again later");
            return;
        }

        Console.WriteLine($"There are {installations.Count} versions of Marmalade available to download");

        Console.WriteLine(new string('-', 128));
        Console.WriteLine($"{"Name",-25} {"Version",-15} {"Size",-12} {"Date",-12}");
        Console.WriteLine(new string('-', 128));

        foreach (var item in installations) {
            Console.WriteLine(
                $"{(string)item.Name,-25} {(string)item.Version,-15} {(string)item.FormattedInstallSize,-12} {item.DateAdded,-12:dd MMM yyyy}");
        }

        Console.WriteLine(new string('-', 128));

        Console.WriteLine($"To install a new engine, provide a valid version number to the install command");
    }
}