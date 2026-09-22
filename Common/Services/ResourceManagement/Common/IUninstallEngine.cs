using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Runtime.Serialization;

namespace MarmaladeLauncher.Services.ResourceManagement.Common;

public interface IUninstallEngine {
    OSPlatform Platform { get; }
    Task<string> UninstallFiles(string execPath);

    public static async Task EnsureNotRunning(string execPath) {
        string procName = Path.GetFileName(execPath);
        var runningProcs = Process.GetProcessesByName(procName);

        foreach (var proc in runningProcs) {
            try {
                if (proc.MainModule?.FileName.Equals(execPath, StringComparison.OrdinalIgnoreCase) == true) {
                    proc.Kill();
                    await proc.WaitForExitAsync();
                }
            }
            catch (Exception e) {
                Console.WriteLine($"Could not kill process '{procName}': {e.Message}");
            }
        }
    }
}