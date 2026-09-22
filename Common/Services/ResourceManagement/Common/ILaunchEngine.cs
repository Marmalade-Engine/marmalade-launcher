using System.Diagnostics;
using System.Runtime.InteropServices;

namespace MarmaladeLauncher.Services.ResourceManagement.Common;

public interface ILaunchEngine {
    OSPlatform Platform { get; }
    ProcessStartInfo CreateStartInfo(string resolvedPath, IEnumerable<string> args);
}