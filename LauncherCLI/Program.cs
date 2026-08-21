using MarmaladeLauncher.Services;

namespace MarmaladeLauncher.CLI {
    public class Program {
        public static async Task<int> Main(string[] args) {
            var settingsService = new SettingsService();
            settingsService.LoadSettings();

            var launchService = new LaunchService(settingsService);
            var installationService = new InstallationRegistryService();
            var installService = new EngineInstallerService(installationService, settingsService);

            var commandRoot = CommandBuilder.CreateCommandRoot(
                settingsService, installationService, installService, launchService);
            
            return await commandRoot.Parse(args).InvokeAsync();
        }
    }
}