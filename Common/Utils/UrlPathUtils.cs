using System.Web;

namespace MarmaladeLauncher.Services.ResourceManagement.Common;

public static class UrlPathUtils {
    public static string GetExtensionFromUrl(string url) {
        try {
            var uri = new Uri(url);
            string path = uri.AbsolutePath;
            if (path.EndsWith(".dmg", StringComparison.OrdinalIgnoreCase)) return ".dmg";
            if (path.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)) return ".exe";
            if (path.EndsWith(".appimage", StringComparison.OrdinalIgnoreCase)) return ".appimage";

            if (uri.Query.Contains("platform=macos", StringComparison.OrdinalIgnoreCase) ||
                uri.Query.Contains("platform=osx", StringComparison.OrdinalIgnoreCase)) {
                return ".dmg";
            }

            if (uri.Query.Contains("platform=windows", StringComparison.OrdinalIgnoreCase)) {
                return ".exe";
            }

            return Path.GetExtension(path);
        }
        catch {
            return string.Empty;
        }
    }

    public static string GetQueryParam(string url, string paramName, string fallback = "unknown") {
        try {
            var uri = new Uri(url);
            var query = HttpUtility.ParseQueryString(uri.Query);
            return query[paramName] ?? fallback;
        }
        catch {
            return fallback;
        }
    }

    public static string GetRelativePath(string baseTargetDir, string absExecPath) {
        try {
            var fullBaseDir = Path.GetFullPath(baseTargetDir);
            var fullAbsPath = Path.GetFullPath(absExecPath);
            return Path.GetRelativePath(fullBaseDir, fullAbsPath).Replace('\\', '/');
        }
        catch (ArgumentException) {
            return Path.GetFileName(absExecPath);
        }
    }
}