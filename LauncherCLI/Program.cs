using System;
using System.CommandLine;
using System.Linq;
using System.Threading.Tasks;
using MarmaladeLauncher.Models;
using MarmaladeLauncher.Services;
using MarmaladeLauncher.Utils;

namespace MarmaladeLauncher.CLI {
    public class Program {
        public static async Task<int> Main(string[] args) {
            var settingsService = new SettingsService();
            settingsService.LoadSettings();

            var launchService = new LaunchService(settingsService);
            var installationService = new InstallationService();
            var installService = new InstallService(installationService, settingsService);

            var commandRoot = new RootCommand("Marmalade Launcher CLI");

            var listCommand = new Command("list-installs", "Show list of installed engine versions");

            listCommand.SetAction(async (ParseResult parseResult) => {
                var installations = await installationService.LoadInstallations();

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
            });

            var searchDownloadsCommand =
                new Command("search-downloads", "Show list of engine versions available for download");

            searchDownloadsCommand.SetAction(async (ParseResult parseResult) => {
                await SearchAvailableEngines(installationService);
            });

            var versionArg = new Argument<string?>("version") {
                Arity = ArgumentArity.ZeroOrOne,
                DefaultValueFactory = _ => null
            };
            var outputDirOpt = new Option<string?>("--output", "-o") {
                Description = "Override the default installation directory path",
                Arity = ArgumentArity.ZeroOrOne
            };
            var searchOpt = new Option<bool>("--search", "-s") {
                Description = "Search engine versions available for download",
            };
            var skipConfirmationOpt = new Option<bool>("--yes", "-y") {
                Arity = ArgumentArity.ZeroOrOne,
                Description = "Skip confirmation prompt",
            };
            var installCommand = new Command("install", "Install a new engine version");
            installCommand.Add(versionArg);
            installCommand.Add(outputDirOpt);
            installCommand.Add(searchOpt);
            installCommand.Add(skipConfirmationOpt);

            installCommand.SetAction(async (ParseResult parseResult) => {
                string? version = parseResult.GetValue(versionArg);
                bool search = parseResult.GetValue(searchOpt);
                bool skipConfirmation = parseResult.GetValue(skipConfirmationOpt);
                string? customOutputDir = parseResult.GetValue(outputDirOpt);

                if (search || string.IsNullOrEmpty(version)) {
                    await SearchAvailableEngines(installationService);
                    return;
                }

                var (availableEngines, versionToEntryMap) = await installationService.FetchEngineVersionsAsync();

                EngineInstallation? targetEngine = availableEngines.FirstOrDefault(x =>
                    string.Equals(x.Version, version, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(x.Name, version, StringComparison.OrdinalIgnoreCase));

                if (targetEngine == null) {
                    Console.WriteLine($"Engine version '{version}' could not be found.");
                    return;
                }

                if (!versionToEntryMap.TryGetValue(targetEngine, out var entry)) {
                    Console.WriteLine($"Error mapping engine version '{version}' to remote manifest entry.");
                    return;
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

                if (!skipConfirmation) {
                    Console.WriteLine($"Target Directory: {targetDirectory}");
                    Console.WriteLine($"Estimated Size:   {installSize}");
                    Console.Write(
                        $"Are you sure you want to install engine version '{entry.ResolvedVersion}'? [y/N]: ");

                    string response = Console.ReadLine()?.Trim();

                    if (!string.Equals(response, "y", StringComparison.OrdinalIgnoreCase) &&
                        !string.Equals(response, "yes", StringComparison.OrdinalIgnoreCase)) {
                        Console.WriteLine("Installation cancelled.");
                        return;
                    }
                }

                var progress = new Progress<double>(val => { Console.Write($"\rDownloading: {val:F1}%"); });
                
                Console.WriteLine($"Installing engine version {entry.ResolvedVersion} to '{targetDirectory}'...");

                EngineInstallation? installedEngine = await installService.InstallEngine(entry, targetDirectory, progress);

                Console.WriteLine();
                if (installedEngine != null) {
                    var currentInstallations = (await installationService.LoadInstallations()).ToList();

                    bool exists = currentInstallations.Any(i => 
                        string.Equals(i.ExecutablePath, installedEngine.ExecutablePath, StringComparison.OrdinalIgnoreCase));

                    if (!exists) {
                        currentInstallations.Add(installedEngine);
        
                        await installationService.SaveInstallations(currentInstallations);
                    }

                    Console.WriteLine($"Successfully installed and registered engine '{installedEngine.Name}'!");
                } else {
                    Console.WriteLine("Installation failed.");
                }
            });

            var uninstallTargetArg = new Argument<string>("target") {
                Description = "Name or version of the installed engine to remove"
            };
            var skipUninstallConfirmOpt = new Option<bool>("--yes", "-y") {
                Arity = ArgumentArity.ZeroOrOne,
                Description = "Skip confirmation prompt"
            };

            var uninstallCommand = new Command("uninstall", "Uninstall an engine installation") {
                uninstallTargetArg,
                skipUninstallConfirmOpt
            };

            uninstallCommand.SetAction(async (ParseResult parseResult) => {
                string target = parseResult.GetValue(uninstallTargetArg);
                bool skipConfirmation = parseResult.GetValue(skipUninstallConfirmOpt);

                var installations = await installationService.LoadInstallations();

                var engine = installations.FirstOrDefault(x =>
                    x.Name.Equals(target, StringComparison.OrdinalIgnoreCase) ||
                    x.Version.Equals(target, StringComparison.OrdinalIgnoreCase));

                if (engine == null) {
                    Console.WriteLine($"Engine '{target}' not found.");
                    return;
                }

                if (!skipConfirmation) {
                    Console.Write($"Are you sure you want to uninstall '{engine.Name}'? [y/N]: ");
                    string response = Console.ReadLine()?.Trim();

                    if (!string.Equals(response, "y", StringComparison.OrdinalIgnoreCase) &&
                        !string.Equals(response, "yes", StringComparison.OrdinalIgnoreCase)) {
                        Console.WriteLine("Uninstallation cancelled.");
                        return;
                    }
                }

                var progress = new Progress<double>(val => { Console.Write($"\rUninstalling: {val:F0}%"); });

                try {
                    await installService.UninstallEngineAsync(engine, progress);
                    Console.WriteLine($"\nSuccessfully uninstalled '{engine.Name}'.");
                }
                catch (Exception ex) {
                    Console.WriteLine($"\nFailed to uninstall '{engine.Name}': {ex.Message}");
                }
            });

            var targetArgument = new Argument<string>("target");
            var engineArgsArgument = new Argument<string[]>("engineArgs") {
                Description = "Arguments to pass directly to the launched engine",
                Arity = ArgumentArity.ZeroOrMore
            };
            var launchCommand = new Command("launch", "Launch engine installation") {
                targetArgument,
                engineArgsArgument
            };

            launchCommand.SetAction(async (ParseResult parseResult) => {
                string target = parseResult.GetValue(targetArgument);
                string[] extraArgs = parseResult.GetValue(engineArgsArgument) ?? Array.Empty<string>();

                var installations = await installationService.LoadInstallations();

                var engine = installations.FirstOrDefault(x =>
                    x.Name.Equals(target, StringComparison.OrdinalIgnoreCase) ||
                    x.Version.Equals(target, StringComparison.OrdinalIgnoreCase));

                if (engine == null) {
                    Console.WriteLine($"Engine '{target}' not found.");
                    return;
                }

                Console.WriteLine($"Launching {engine.Name} ({engine.ExecutablePath})");

                await launchService.LaunchAsync(engine, extraArgs);
            });

            commandRoot.Add(listCommand);
            commandRoot.Add(searchDownloadsCommand);
            commandRoot.Add(installCommand);
            commandRoot.Add(uninstallCommand);
            commandRoot.Add(launchCommand);

            return await commandRoot.Parse(args).InvokeAsync();
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
}