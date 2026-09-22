namespace MarmaladeLauncher.Services.ResourceManagement.Common;

public interface IFileDownloader {
    Task DownloadFileAsync(
        string url,
        string destinationPath,
        IProgress<double>? progress = null,
        CancellationToken cancellationToken = default);
}

public class FileDownloader : IFileDownloader {
    private readonly HttpClient _httpClient;

    public FileDownloader(HttpClient? httpClient = null) {
        _httpClient = httpClient ?? new HttpClient();
    }

    public async Task DownloadFileAsync(
        string url,
        string destinationPath,
        IProgress<double>? progress = null,
        CancellationToken cancellationToken = default) {
        
        using var response = await _httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        response.EnsureSuccessStatusCode();

        long totalBytes = response.Content.Headers.ContentLength ?? -1L;

        using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var fStream = new FileStream(destinationPath, FileMode.Create, FileAccess.Write, FileShare.Read, 8192, true);

        var buffer = new byte[8192];
        long totalBytesRead = 0;
        int bytesRead;

        while ((bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length, cancellationToken)) > 0) {
            await fStream.WriteAsync(buffer, 0, bytesRead, cancellationToken);
            totalBytesRead += bytesRead;

            if (totalBytes > 0 && progress != null) {
                progress.Report((double)totalBytesRead / totalBytes * 100);
            }
        }
    }
}