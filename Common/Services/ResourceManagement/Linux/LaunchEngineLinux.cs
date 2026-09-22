using System.Diagnostics;
using System.Runtime.InteropServices;
using MarmaladeLauncher.Services.ResourceManagement.Common;

namespace MarmaladeLauncher.Services.ResourceManagement.Linux;

public class LaunchEngineLinux : ILaunchEngine {
    public OSPlatform Platform => OSPlatform.Linux;

    public ProcessStartInfo CreateStartInfo(string resolvedPath, IEnumerable<string> args) {
        string workingDir = Path.GetDirectoryName(resolvedPath) ?? string.Empty;

        var argList = args.ToList();

        string escapedArgs = string.Join(" ", argList.Select(a => $"\"{a.Replace("\"", "\\\"")}\""));
        string combinedArgsStr = string.IsNullOrWhiteSpace(escapedArgs) ? "" : $" {escapedArgs}";

        return new ProcessStartInfo {
            FileName = "/bin/bash",
            Arguments = $"-c \"cd '{workingDir}' && nohup '{resolvedPath}'{combinedArgsStr} > /dev/null 2>&1 &\"",
            WorkingDirectory = workingDir,
            UseShellExecute = false,
            CreateNoWindow = true
        };
    }
}