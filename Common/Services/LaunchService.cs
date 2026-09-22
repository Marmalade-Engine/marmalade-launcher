using System.Diagnostics;
using MarmaladeLauncher.Models;
using MarmaladeLauncher.Services.ResourceManagement.Common;

namespace MarmaladeLauncher.Services;

public class LaunchService {
    private readonly SettingsService _settingsService;
    private readonly IPlatformEngineResolver _engineResolver;

    public LaunchService(SettingsService settingsService, IPlatformEngineResolver engineResolver) {
        _settingsService = settingsService;
        _engineResolver = engineResolver;
    }

    public async Task<bool> LaunchAsync(
        LocalEngineInstallation item, 
        IEnumerable<string>? extraArgs = null, 
        Func<Task>? onPostLaunch = null
    ) {
        if (string.IsNullOrWhiteSpace(item?.ExecutablePath)) return false;

        try {
            string resolvedPath = item.GetResolvedExecutablePath(_settingsService.Settings.DefaultInstallLocation);
            var args = CombineArgs(item.Arguments, extraArgs);

            var launcher = _engineResolver.GetLaunchEngine();
            var startInfo = launcher.CreateStartInfo(resolvedPath, args);

            using var process = Process.Start(startInfo);
            if (process == null) return false;

            Console.WriteLine($"Launched engine: {item.ExecutablePath}");

            if (onPostLaunch != null) await onPostLaunch();
            return true;
        }
        catch (Exception ex) {
            Console.WriteLine($"Error launching executable: {ex.Message}");
            return false;
        }
    }

    private static IEnumerable<string> CombineArgs(string? defaultArgs, IEnumerable<string>? extraArgs) {
        var result = new List<string>();

        if (!string.IsNullOrWhiteSpace(defaultArgs)) {
            result.AddRange(defaultArgs.Split(' ', StringSplitOptions.RemoveEmptyEntries));
        }

        if (extraArgs != null) {
            result.AddRange(extraArgs.Where(a => !string.IsNullOrWhiteSpace(a)).Select(a => a.Trim()));
        }

        return result;
    }
}