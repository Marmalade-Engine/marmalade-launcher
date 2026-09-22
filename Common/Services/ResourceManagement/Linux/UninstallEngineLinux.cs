using System.Runtime.InteropServices;
using MarmaladeLauncher.Services.ResourceManagement.Common;

namespace MarmaladeLauncher.Services.ResourceManagement.Linux;

public class UninstallEngineLinux : IUninstallEngine {
    public OSPlatform Platform => OSPlatform.Linux;

    public async Task<string> UninstallFiles(string execPath) {
        if (string.IsNullOrWhiteSpace(execPath) || !File.Exists(execPath)) {
            throw new ArgumentException("Executable path must be valid to perform cleanup");
        }

        try {
            string rootToRemove = GetContainingInstallationRoot(execPath);

            if (!string.IsNullOrEmpty(rootToRemove) && Directory.Exists(rootToRemove)) {
                Console.WriteLine($"Attempting removal of installation directory: {rootToRemove}");

                Directory.Delete(rootToRemove, true);
                return $"Successfully removed installation files from {rootToRemove}";
            }
            else {
                if (File.Exists(execPath)) {
                    Console.WriteLine("Installation root not found, deleting just the executable file.");
                    File.Delete(execPath);
                    return $"Successfully deleted executable standalone: {execPath}";
                }

                throw new DirectoryNotFoundException(
                    $"Could not locate installation directory or primary files for cleanup at {execPath}");
            }
        }
        catch (Exception e) {
            throw new InvalidOperationException(
                $"Failed to remove installation directory: {e.Message}");
        }
    }

    private string GetContainingInstallationRoot(string execPath) {
        var fileInfo = new FileInfo(execPath);
        var currentDir = fileInfo.Directory;

        // Traverse upwards until finding a directory containing install_info.json
        // or a directory whose parent is "installations"
        while (currentDir != null) {
            if (File.Exists(Path.Combine(currentDir.FullName, "install_info.json"))) {
                return currentDir.FullName;
            }

            if (currentDir.Parent != null &&
                currentDir.Parent.Name.Equals("installations", StringComparison.OrdinalIgnoreCase)) {
                return currentDir.FullName;
            }

            currentDir = currentDir.Parent;
        }

        return fileInfo.DirectoryName ?? string.Empty;
    }
}