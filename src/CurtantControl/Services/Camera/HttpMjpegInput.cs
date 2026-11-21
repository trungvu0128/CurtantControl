using System;
using System.IO;
using System.Net.Http;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace CurtantControl.Services.Camera;

public static class HttpMjpegInput
{
    public static async Task CopyAsync(string url, Stream output, CancellationToken cancellationToken, bool activatePythonCamera = true)
    {
        using var client = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false })
            { Timeout = Timeout.InfiniteTimeSpan, MaxResponseContentBufferSize = 8192 };
        await CopyAsync(client, url, output, cancellationToken, activatePythonCamera).ConfigureAwait(false);
    }

    public static async Task CopyAsync(HttpClient client, string url, Stream output, CancellationToken cancellationToken, bool activatePythonCamera = true)
    {
        using var headersTimeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        headersTimeout.CancelAfter(TimeSpan.FromSeconds(10));
        using var response = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, headersTimeout.Token).ConfigureAwait(false);
        if (response.StatusCode == HttpStatusCode.NoContent)
        {
            var uri = new Uri(url);
            if (!activatePythonCamera || !IsKnownPythonFeed(uri))
                throw new InvalidDataException("Camera service returned 204: streaming is disabled. Enable camera mode in that service.");
            response.Dispose();
            using var activation = new HttpRequestMessage(HttpMethod.Post, new Uri(uri, "/camera_control"))
            {
                Content = new StringContent("{\"mode\":1}", Encoding.UTF8, "application/json")
            };
            using var enabled = await client.SendAsync(activation, headersTimeout.Token).ConfigureAwait(false);
            enabled.EnsureSuccessStatusCode();
            using var result = JsonDocument.Parse(await enabled.Content.ReadAsStringAsync(headersTimeout.Token).ConfigureAwait(false));
            if (!result.RootElement.TryGetProperty("type", out var type) || type.GetString() != "camera")
                throw new InvalidDataException("Python service refused camera activation. Check its camera availability/permissions and server log.");
            await CopyAsync(client, url, output, cancellationToken, activatePythonCamera: false).ConfigureAwait(false);
            return;
        }
        response.EnsureSuccessStatusCode();
        var contentType = response.Content.Headers.ContentType?.MediaType;
        if (contentType is "text/html" or "application/json")
            throw new InvalidDataException($"Camera endpoint returned {contentType}, not JPEG video.");
        await using var input = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        await MjpegFrameReader.CopyAsync(input, output, cancellationToken).ConfigureAwait(false);
    }

    public static bool IsKnownPythonFeed(Uri uri) => uri.Scheme == "http"
        && (uri.Host is "127.0.0.1" or "localhost" or "[::1]") && uri.Port == 5000
        && uri.AbsolutePath == "/video_feed" && uri.Query.Length == 0 && uri.UserInfo.Length == 0;
}
