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
            string combinedArgs = BuildCombinedArguments(item.Arguments, extraArgs);

            ProcessStartInfo startInfo;

            if (OperatingSystem.IsMacOS()) {
                startInfo = CreateMacDetachedStartInfo(item, combinedArgs);
            } else if (OperatingSystem.IsLinux()) {
                startInfo = CreateLinuxDetachedStartInfo(item, combinedArgs);
            } else {
                startInfo = new ProcessStartInfo {
                    FileName = item.ExecutablePath,
                    Arguments = combinedArgs,
                    UseShellExecute = true,
                    WorkingDirectory = Path.GetDirectoryName(item.ExecutablePath) ?? string.Empty
                };
            }

            using (var process = Process.Start(startInfo)) {
                Console.WriteLine($"[Launch] Detached process started for '{item.Name}' with args: '{combinedArgs}'");
            }

            if (onPostLaunch != null) {
                await onPostLaunch();
            }

            return true;
        }
        catch (Exception e) {
            Console.WriteLine($"[Launch] Error launching executable: {e.Message}");
            return false;
        }
    }

    private ProcessStartInfo CreateMacDetachedStartInfo(EngineInstallation item, string combinedArgs) {
        string workingDir = Path.GetDirectoryName(item.ExecutablePath) ?? string.Empty;
        string appBundlePath = GetMacAppBundlePath(item.ExecutablePath);

        if (!string.IsNullOrEmpty(appBundlePath)) {
            var args = $"-n \"{appBundlePath}\"";
            if (!string.IsNullOrWhiteSpace(combinedArgs)) {
                args += $" --args {combinedArgs}";
            }

            return new ProcessStartInfo {
                FileName = "/usr/bin/open",
                Arguments = args,
                UseShellExecute = false,
                CreateNoWindow = true,
                WorkingDirectory = workingDir
            };
        }

        string binaryArgs = string.IsNullOrWhiteSpace(combinedArgs) ? "" : $" {combinedArgs}";
        return new ProcessStartInfo {
            FileName = "/bin/zsh",
            Arguments = $"-c \"nohup '{item.ExecutablePath}'{binaryArgs} > /dev/null 2>&1 &\"",
            UseShellExecute = false,
            CreateNoWindow = true,
            WorkingDirectory = workingDir
        };
    }

    private ProcessStartInfo CreateLinuxDetachedStartInfo(EngineInstallation item, string combinedArgs) {
        string workingDir = Path.GetDirectoryName(item.ExecutablePath) ?? string.Empty;
        string binaryArgs = string.IsNullOrWhiteSpace(combinedArgs) ? "" : $" {combinedArgs}";

        return new ProcessStartInfo {
            FileName = "/bin/bash",
            Arguments = $"-c \"nohup '{item.ExecutablePath}'{binaryArgs} > /dev/null 2>&1 &\"",
            UseShellExecute = false,
            CreateNoWindow = true,
            WorkingDirectory = workingDir
        };
    }

    private string GetMacAppBundlePath(string executablePath) {
        if (executablePath.EndsWith(".app", StringComparison.OrdinalIgnoreCase) && Directory.Exists(executablePath)) {
            return executablePath;
        }

        int appIndex = executablePath.IndexOf(".app", StringComparison.OrdinalIgnoreCase);
        if (appIndex != -1) {
            string bundlePath = executablePath.Substring(0, appIndex + 4);
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