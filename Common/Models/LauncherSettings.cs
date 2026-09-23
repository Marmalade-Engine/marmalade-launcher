using System;
using System.Globalization;
using System.IO;
using MarmaladeLauncher.Services;

namespace MarmaladeLauncher.Models;

public enum PostLaunchBehaviour {
    PostLaunchBehaviour_KEEPOPEN,
    PostLaunchBehaviour_MINIMISE,
    PostLaunchBehaviour_CLOSE,
}

public enum InstallScope {
    InstallScope_USER,
    InstallScope_SYS
}

public enum LinuxPackageType {
    LinuxPackageType_TAR,
    LinuxPackageType_APPIMAGE,
}

public enum MacPackageType {
    MacPackageType_UNIVERSAL,
    MacPackageType_TAR,
}


public class LauncherSettings {
    public string DefaultInstallLocation { get; set; } = SettingsService.DefaultBaseDirectory;
    public PostLaunchBehaviour PostLaunchBehaviour { get; set; } = PostLaunchBehaviour.PostLaunchBehaviour_KEEPOPEN;
    public string CurrentLocale { get; set; } = "en-GB";
    public bool EnableDevBuilds { get; set; } = false;
    public InstallScope PreferredInstallScope { get; set; } = InstallScope.InstallScope_USER;
    
    public LinuxPackageType PreferredLinuxPackageType { get; set; } = LinuxPackageType.LinuxPackageType_TAR;
    public MacPackageType PreferredMacPackageType { get; set; } = MacPackageType.MacPackageType_TAR;
}