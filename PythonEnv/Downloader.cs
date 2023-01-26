namespace PythonEnv
{
    internal static class Downloader
    {
        private static readonly HttpClient httpClient = new();

        public static async Task Download(
            string downloadUrl,
            string outputFilePath,
            Action<float>? progress = null,
            CancellationToken token = default)
        {
            try
            {
                await using FileStream fileStream = new(outputFilePath, FileMode.Create);
                await httpClient.DownloadWithProgressAsync(downloadUrl, fileStream, progress, token);
            }
            catch
            {
                if (File.Exists(outputFilePath))
                {
                    File.Delete(outputFilePath);
                }
                throw;
            }
        }
    }

    internal static class HttpClientExtension
    {
        public static async Task DownloadWithProgressAsync(
            this HttpClient client,
            string requestUri,
            Stream destination,
            Action<float>? progress = null,
            CancellationToken cancellationToken = default)
        {
            using var response = await client.GetAsync(requestUri, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            response.EnsureSuccessStatusCode();

            var contentLength = response.Content.Headers.ContentLength;

            await using var download = await response.Content.ReadAsStreamAsync(cancellationToken);
            const int bufferSize = 81920;

            if (progress == null || !contentLength.HasValue)
            {
                await download.CopyToAsync(destination, bufferSize, cancellationToken);
                return;
            }

            var buffer = new byte[bufferSize];
            long totalBytesRead = 0;
            int bytesRead;
            while ((bytesRead = await download.ReadAsync(buffer, cancellationToken)) != 0)
            {
                await destination.WriteAsync(buffer.AsMemory(0, bytesRead), cancellationToken);
                totalBytesRead += bytesRead;
                var progressPercentage = ((float)totalBytesRead / contentLength.Value) * 100;
                progress.Invoke(progressPercentage);
            }
        }
    }
}
