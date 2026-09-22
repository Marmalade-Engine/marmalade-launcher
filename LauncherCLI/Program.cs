using MarmaladeLauncher.Services;
using MarmaladeLauncher.Services.ResourceManagement.Common;
using MarmaladeLauncher.Services.ResourceManagement.Linux;

namespace MarmaladeLauncher.CLI {
    public class Program {
        public static async Task<int> Main(string[] args) {
            var settingsService = new SettingsService();
            settingsService.LoadSettings();

            var installEngines = new IInstallEngine[] {
                new InstallEngineLinux(),
            };

            var uninstallEngines = new IUninstallEngine[] {
                new UninstallEngineLinux(),
            };

            var launchEngines = new ILaunchEngine[] {
                new LaunchEngineLinux(),
            };
            
            var platformEngineResolver = new PlatformEngineResolver(installEngines, uninstallEngines, launchEngines);
            
            var launchService = new LaunchService(settingsService, platformEngineResolver);
            var installationService = new InstallationRegistryService();
            var fileDownloader = new FileDownloader();
            

            var installService = new EngineInstallerService(
                installationService, 
                settingsService, 
                fileDownloader, 
                platformEngineResolver
            );

            var commandRoot = CommandBuilder.CreateCommandRoot(
                settingsService, installationService, installService, launchService);
            
            return await commandRoot.Parse(args).InvokeAsync();
        }
    }
}