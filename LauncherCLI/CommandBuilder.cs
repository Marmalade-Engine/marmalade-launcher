using System.CommandLine;
using MarmaladeLauncher.CLI.Commands;
using MarmaladeLauncher.Services;

namespace MarmaladeLauncher.CLI;

public class CommandBuilder {
    public static RootCommand CreateCommandRoot(
        SettingsService settingsService,
        InstallationService installationService,
        InstallService installService,
        LaunchService launchService
    ) {
        var commandRoot = new RootCommand("Marmalade Launcher CLI");

        var listCommand = new ListInstallsCommand();
        commandRoot.Add(listCommand);

        var searchDownloadsCommand = new SearchDownloadsCommand(installationService);
        commandRoot.Add(searchDownloadsCommand);

        // Install Command

        var versionArg = new Argument<string?>("version") { Arity = ArgumentArity.ZeroOrOne };
        var outputDirOpt = new Option<string?>("--output", "-o")
            { Description = "Override the default installation directory" };
        var searchOpt = new Option<bool>("--search", "-s")
            { Description = "Search engine versions available for download" };
        var skipConfirm = new Option<bool>("--yes", "-y") { Description = "Skip install confirmation prompt" };

        var installCommand = new Command("install", "Install a new engine version") {
            versionArg, outputDirOpt, searchOpt, skipConfirm
        };

        installCommand.SetAction(async (parseResult, cancellationToken) => {
            var version = parseResult.GetValue(versionArg);
            var customOutputDir = parseResult.GetValue(outputDirOpt);
            var search = parseResult.GetValue(searchOpt);
            var skipConfirmation = parseResult.GetValue(skipConfirm);

            return await InstallEngineCommand.Install(
                version, customOutputDir, search, skipConfirmation, settingsService, installationService,
                installService);
        });
        commandRoot.Add(installCommand);

        // Uninstall Command

        var uninstallTargetArg = new Argument<string?>("target")
            { Description = "Name or version of the installed engine to remove", Arity = ArgumentArity.ZeroOrOne };
        var skipUninstallConfirmOpt = new Option<bool>("--yes", "-y") { Description = "Skip confirmation prompt" };

        var uninstallCommand = new Command("uninstall", "Uninstall an engine installation") {
            uninstallTargetArg, skipUninstallConfirmOpt
        };

        uninstallCommand.SetAction(async (parseResult, cancellationToken) => {
            var target = parseResult.GetValue(uninstallTargetArg)!;
            var skipConfirmation = parseResult.GetValue(skipUninstallConfirmOpt);

            return await UninstallEngineCommand.Uninstall(target, skipConfirmation, installationService,
                installService);
        });
        commandRoot.Add(uninstallCommand);

        // Launch Command

        var targetArgument = new Argument<string>("target");
        var engineArgsArgument = new Argument<string[]>("engineArgs")
            { Description = "Arguments to pass directly to the launched engine", Arity = ArgumentArity.ZeroOrMore };

        var launchCommand = new Command("launch", "Launch engine installation") {
            targetArgument, engineArgsArgument
        };

        launchCommand.SetAction(async (parseResult, cancellationToken) => {
            var target = parseResult.GetValue(targetArgument)!;
            var extraArgs = parseResult.GetValue(engineArgsArgument) ?? Array.Empty<string>();

            await LaunchEngineCommand.Launch(target, extraArgs, installationService, launchService);
        });
        commandRoot.Add(launchCommand);

        return commandRoot;
    }
}