namespace MarmaladeLauncher.Utils;

public static class ByteFormatter {
    private static readonly string[] SizeSuffixes = { "B", "KB", "MB", "GB", "TB" };

    public static string FormatSize(long bytes) {
        if (bytes < 0) return "0 B";
        int i = 0;
        double size = bytes;

        while (size >= 1024 && i < SizeSuffixes.Length - 1) {
            size /= 1024;
            i++;
        }

        return $"{size:0.##} {SizeSuffixes[i]}";
    }
}