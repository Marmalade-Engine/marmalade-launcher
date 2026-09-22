using System.Runtime.InteropServices;

namespace MarmaladeLauncher.Services.ResourceManagement.Common;

public interface IPlatformEngineResolver {
    IInstallEngine GetInstallEngine();
    IUninstallEngine GetUninstallEngine();
    ILaunchEngine GetLaunchEngine();
}

public class PlatformEngineResolver : IPlatformEngineResolver {
    private readonly Dictionary<OSPlatform, IInstallEngine> _installEngines;
    private readonly Dictionary<OSPlatform, IUninstallEngine> _uninstallEngines;
    private readonly Dictionary<OSPlatform, ILaunchEngine> _launchEngines;

    public PlatformEngineResolver(
        IEnumerable<IInstallEngine> installEngines,
        IEnumerable<IUninstallEngine> uninstallEngines,
        IEnumerable<ILaunchEngine> launchEngines) {
        
        _installEngines = installEngines.ToDictionary(e => e.Platform);
        _uninstallEngines = uninstallEngines.ToDictionary(e => e.Platform);
        _launchEngines = launchEngines.ToDictionary(e => e.Platform);
    }

    public IInstallEngine GetInstallEngine() {
        var os = GetCurrentOSPlatform();
        if (_installEngines.TryGetValue(os, out var engine))
            return engine;

        throw new PlatformNotSupportedException($"Installing for {RuntimeInformation.OSDescription} is unsupported.");
    }

    public IUninstallEngine GetUninstallEngine() {
        var os = GetCurrentOSPlatform();
        if (_uninstallEngines.TryGetValue(os, out var engine))
            return engine;

        throw new PlatformNotSupportedException($"Uninstalling for {RuntimeInformation.OSDescription} is unsupported.");
    }

    public ILaunchEngine GetLaunchEngine() {
        var os = GetCurrentOSPlatform();
        if (_launchEngines.TryGetValue(os, out var engine))
            return engine;
        
        throw new PlatformNotSupportedException($"Launching for {RuntimeInformation.OSDescription} is unsupported.");
    }

    private static OSPlatform GetCurrentOSPlatform() {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) return OSPlatform.Windows;
        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX)) return OSPlatform.OSX;
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux)) return OSPlatform.Linux;

        throw new PlatformNotSupportedException($"Unsupported platform: {RuntimeInformation.OSDescription}");
    }
}