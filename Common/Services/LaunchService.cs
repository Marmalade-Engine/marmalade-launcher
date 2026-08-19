using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using MarmaladeLauncher.Models;

namespace MarmaladeLauncher.Services;

public class LaunchService {
    private readonly SettingsService _settingsService;

    public LaunchService(SettingsService settingsService) {
        _settingsService = settingsService;
    }

    public async Task<bool> LaunchAsync(EngineInstallation item, IEnumerable<string>? extraArgs = null,
        Func<Task>? onPostLaunch = null) {
        if (item == null || string.IsNullOrWhiteSpace(item.ExecutablePath))
            return false;

        try {
            string defaultLocation = _settingsService.Settings.DefaultInstallLocation;
            string resolvedPath = item.GetResolvedExecutablePath(defaultLocation);
            string combinedArgs = BuildCombinedArguments(item.Arguments, extraArgs);

            ProcessStartInfo startInfo;

            if (OperatingSystem.IsMacOS()) {
                startInfo = CreateMacDetachedStartInfo(item, resolvedPath, combinedArgs, defaultLocation);
            }
            else if (OperatingSystem.IsLinux()) {
                startInfo = CreateLinuxDetachedStartInfo(resolvedPath, combinedArgs);
            }
            else {
                startInfo = new ProcessStartInfo {
                    FileName = resolvedPath,
                    Arguments = combinedArgs,
                    UseShellExecute = true,
                    WorkingDirectory = Path.GetDirectoryName(resolvedPath) ?? string.Empty
                };
            }

            var process = Process.Start(startInfo);
            if (process == null) {
                Console.WriteLine($"Error launching engine: {item.ExecutablePath}");
                return false;
            }
            
            Console.WriteLine($"Launching engine: {item.ExecutablePath}");
            
            if (onPostLaunch != null) {
                await onPostLaunch();
            }

            return true;
        }
        catch (Exception e) {
            Console.WriteLine($"Error launching executable: {e.Message}");
            return false;
        }
    }

    private ProcessStartInfo CreateMacDetachedStartInfo(EngineInstallation item, string resolvedPath,
        string combinedArgs, string defaultLocation) {
        string appBundlePath = GetMacAppBundlePath(resolvedPath);

        if (!string.IsNullOrEmpty(appBundlePath)) {
            var startInfo = new ProcessStartInfo {
                FileName = "/usr/bin/open",
                UseShellExecute = false,
                CreateNoWindow = true,
                WorkingDirectory = Path.GetDirectoryName(appBundlePath) ?? string.Empty
            };
            
            startInfo.ArgumentList.Add("-n");
            startInfo.ArgumentList.Add("-a");
            startInfo.ArgumentList.Add(appBundlePath);

            string finalArgs = string.IsNullOrWhiteSpace(combinedArgs) 
                ? "-ApplePersistenceIgnoreState YES" 
                : $"{combinedArgs} -ApplePersistenceIgnoreState YES";

            startInfo.ArgumentList.Add("--args");
            startInfo.ArgumentList.Add(finalArgs);

            return startInfo;
        }

        return new ProcessStartInfo {
            FileName = resolvedPath,
            Arguments = combinedArgs,
            UseShellExecute = false,
            CreateNoWindow = true,
            WorkingDirectory = Path.GetDirectoryName(resolvedPath) ?? string.Empty
        };
    }
    
    private ProcessStartInfo CreateLinuxDetachedStartInfo(string resolvedPath, string combinedArgs) {
        string workingDir = Path.GetDirectoryName(resolvedPath) ?? string.Empty;
        string binaryArgs = string.IsNullOrWhiteSpace(combinedArgs) ? "" : $" {combinedArgs}";

        return new ProcessStartInfo {
            FileName = "/bin/bash",
            Arguments = $"-c \"nohup '{resolvedPath}'{binaryArgs} > /dev/null 2>&1 &\"",
            UseShellExecute = false,
            CreateNoWindow = true,
            WorkingDirectory = workingDir
        };
    }

    private string GetMacAppBundlePath(string resolvedPath) {
        if (resolvedPath.EndsWith(".app", StringComparison.OrdinalIgnoreCase) && Directory.Exists(resolvedPath)) {
            return resolvedPath;
        }

        int appIndex = resolvedPath.IndexOf(".app", StringComparison.OrdinalIgnoreCase);
        if (appIndex != -1) {
            string bundlePath = resolvedPath.Substring(0, appIndex + 4);
            if (Directory.Exists(bundlePath)) {
                return bundlePath;
            }
        }

        return string.Empty;
    }

    private string BuildCombinedArguments(string? defaultArgs, IEnumerable<string>? extraArgs) {
        var parts = new List<string>();

        if (!string.IsNullOrWhiteSpace(defaultArgs)) {
            parts.Add(defaultArgs.Trim());
        }

        if (extraArgs != null) {
            var formattedExtraArgs = extraArgs
                .Where(arg => !string.IsNullOrWhiteSpace(arg))
                .Select(arg => arg.Contains(' ') ? $"\"{arg}\"" : arg);

            parts.AddRange(formattedExtraArgs);
        }

        return string.Join(" ", parts);
    }
}