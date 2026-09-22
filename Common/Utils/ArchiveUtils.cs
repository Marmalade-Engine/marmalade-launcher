using System.Formats.Tar;
using System.IO.Compression;

namespace MarmaladeLauncher.Common.Utils;

public static class ArchiveUtils {
    public static async Task ExtractArchive(string arcPath, string destDir, string? originalFileName = null) {
        if (!File.Exists(arcPath)) {
            throw new FileNotFoundException($"Archive not found at: {arcPath}");
        }

        Directory.CreateDirectory(destDir);

        var extensionCtx = !string.IsNullOrWhiteSpace(originalFileName)
            ? originalFileName.Trim()
            : arcPath.Trim();
        
        if (IsTar(extensionCtx)) {
            await ExtractTar(arcPath, destDir);
        }
        else if (extensionCtx.EndsWith(".zip", StringComparison.OrdinalIgnoreCase)) {
            ZipFile.ExtractToDirectory(arcPath, destDir, true);
        }
        else {
            var ext = Path.GetExtension(extensionCtx);
            var displayExt = string.IsNullOrWhiteSpace(ext) ? "(Invalid or No Archive Extenstion)" : ext;
            throw new NotSupportedException($"Unsupported archive extension: {displayExt}");
        }

        FlattenDirectory(destDir);
    }

    private static void FlattenDirectory(string destDir) {
        var dirs = Directory.GetDirectories(destDir);

        var nonMetadataFiles = Directory.GetFiles(destDir)
            .Where(f => {
                var fileName = Path.GetFileName(f);
            
                if (fileName.Equals("install_info.json", StringComparison.OrdinalIgnoreCase)) return false;
                if (fileName.StartsWith("temp_", StringComparison.OrdinalIgnoreCase)) return false;
                if (fileName.EndsWith(".tar.gz", StringComparison.OrdinalIgnoreCase) ||
                    fileName.EndsWith(".tgz", StringComparison.OrdinalIgnoreCase) ||
                    fileName.EndsWith(".zip", StringComparison.OrdinalIgnoreCase)) return false;

                return true;
            })
            .ToArray();

        if (dirs.Length == 1 && nonMetadataFiles.Length == 0) {
            var singleDir = dirs[0];

            foreach (var dir in Directory.GetDirectories(singleDir)) {
                var destPath = Path.Combine(destDir, Path.GetFileName(dir));
                if (Directory.Exists(destPath)) {
                    Directory.Delete(destPath, true);
                }

                Directory.Move(dir, destPath);
            }

            foreach (var file in Directory.GetFiles(singleDir)) {
                var destPath = Path.Combine(destDir, Path.GetFileName(file));
                File.Move(file, destPath, overwrite: true);
            }

            Directory.Delete(singleDir, recursive: true);
        }
    }

    private static bool IsTar(string path) {
        return path.EndsWith(".tar.gz", StringComparison.OrdinalIgnoreCase) ||
               path.EndsWith(".tgz", StringComparison.OrdinalIgnoreCase);
    }

    private static async Task ExtractTar(string arcPath, string destDir) {
        await using var fStream = File.OpenRead(arcPath);
        await using var gzipStream = new GZipStream(fStream, CompressionMode.Decompress);
        await TarFile.ExtractToDirectoryAsync(gzipStream, destDir, true);
    }
}