using System.Globalization;
using System.Text.Json;

namespace Dotsider.DeployHost;

/// <summary>
/// Queries the five Prometheus metrics used by the hosted demo report.
/// Unavailable or malformed responses produce the established N/A placeholder.
/// Each invocation appends one invariant UTC line to the metrics log.
/// </summary>
internal sealed class MetricsReportCommand(
    HttpClient httpClient,
    TimeProvider timeProvider,
    string prometheusUrl,
    string logPath)
{
    private static readonly string[] s_queries =
    [
        "sum(rate(caddy_http_requests_total[5m]))",
        "sum(rate(caddy_http_request_errors_total[5m]))",
        "histogram_quantile(0.95, sum(rate(caddy_http_request_duration_seconds_bucket[5m])) by (le))",
        "sum(caddy_http_requests_in_flight)",
        "caddy_reverse_proxy_upstreams_healthy",
    ];

    /// <summary>
    /// Queries Prometheus sequentially and appends the established report format.
    /// A failed individual query does not prevent the remaining fields from being written.
    /// The command exits successfully after the log line is persisted.
    /// </summary>
    /// <param name="cancellationToken">Stops HTTP and file operations.</param>
    /// <returns>Zero after a report line is appended.</returns>
    internal async Task<int> RunAsync(CancellationToken cancellationToken)
    {
        string[] values = new string[s_queries.Length];
        for (var index = 0; index < s_queries.Length; index++)
        {
            values[index] = await QueryAsync(s_queries[index], cancellationToken).ConfigureAwait(false);
        }

        string timestamp = timeProvider.GetUtcNow().ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture);
        string line = string.Format(
            CultureInfo.InvariantCulture,
            "{0} | req/s={1,-8} err/s={2,-8} p95={3,-10} inflight={4,-4} upstream_healthy={5}{6}",
            timestamp,
            values[0],
            values[1],
            values[2],
            values[3],
            values[4],
            Environment.NewLine);
        await File.AppendAllTextAsync(logPath, line, cancellationToken).ConfigureAwait(false);
        return 0;
    }

    private async Task<string> QueryAsync(string query, CancellationToken cancellationToken)
    {
        try
        {
            string requestUri = prometheusUrl.TrimEnd('/')
                + "/api/v1/query?query="
                + Uri.EscapeDataString(query);
            using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutSource.CancelAfter(TimeSpan.FromSeconds(5));
            using HttpResponseMessage response = await httpClient.GetAsync(
                requestUri,
                HttpCompletionOption.ResponseHeadersRead,
                timeoutSource.Token).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                return "N/A";
            }

            await using Stream stream = await response.Content.ReadAsStreamAsync(timeoutSource.Token).ConfigureAwait(false);
            using JsonDocument document = await JsonDocument.ParseAsync(
                stream,
                cancellationToken: timeoutSource.Token).ConfigureAwait(false);
            if (!document.RootElement.TryGetProperty("data", out JsonElement data)
                || !data.TryGetProperty("result", out JsonElement result)
                || result.ValueKind != JsonValueKind.Array
                || result.GetArrayLength() == 0
                || !result[0].TryGetProperty("value", out JsonElement value)
                || value.ValueKind != JsonValueKind.Array
                || value.GetArrayLength() < 2)
            {
                return "N/A";
            }

            return value[1].ValueKind == JsonValueKind.String
                ? value[1].GetString() ?? "N/A"
                : value[1].GetRawText();
        }
        catch (Exception exception) when (exception is HttpRequestException
            or JsonException
            or TaskCanceledException)
        {
            return "N/A";
        }
    }
}
