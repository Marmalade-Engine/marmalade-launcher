using System;
using System.CommandLine;
using System.Linq;
using System.Threading.Tasks;
using MarmaladeLauncher.Services;

namespace MarmaladeLauncher.CLI {
    public class Program {
        public static async Task<int> Main(string[] args) {
            var settingsService = new SettingsService();
            settingsService.LoadSettings();

            var launchService = new LaunchService(settingsService);

            var installService = new InstallationService();

            var commandRoot = new RootCommand("Marmalade Launcher CLI");

            var listCommand = new Command("--list-installs", "Show list of installed engine versions");

            listCommand.SetAction(async (parseResult) => {
                var installations = await installService.LoadInstallations();

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

            var targetArgument = new Argument<string>("target");

            var engineArgsArgument = new Argument<string[]>("engineArgs") {
                Description = "Arguments to pass directly to the launched engine",
                Arity = ArgumentArity.ZeroOrMore
            };

            var launchCommand = new Command("--launch", "Launch engine installation") {
                targetArgument,
                engineArgsArgument
            };
            
            launchCommand.SetAction(async (parseResult) => {
                string target = parseResult.GetValue(targetArgument);
                string[] extraArgs = parseResult.GetValue(engineArgsArgument) ?? Array.Empty<string>();
                
                var installations = await installService.LoadInstallations();

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
            commandRoot.Add(launchCommand);

            return await commandRoot.Parse(args).InvokeAsync();
        }
    }
}