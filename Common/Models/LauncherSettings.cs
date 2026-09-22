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
    UserSpace,
    SystemSpace
}

public class LauncherSettings {
    public string DefaultInstallLocation { get; set; } = SettingsService.DefaultBaseDirectory;
    public PostLaunchBehaviour PostLaunchBehaviour { get; set; } = PostLaunchBehaviour.PostLaunchBehaviour_KEEPOPEN;
    public string CurrentLocale { get; set; } = "en-GB";
    public bool EnableDevBuilds { get; set; } = false;
    public InstallScope PreferredInstallScope { get; set; } = InstallScope.UserSpace;
}