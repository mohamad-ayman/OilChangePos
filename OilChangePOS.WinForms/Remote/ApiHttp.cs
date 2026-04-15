using System.Net.Http.Json;
using System.Text.Json;

namespace OilChangePOS.WinForms.Remote;

internal static class ApiHttp
{
    internal static async Task EnsureSuccessAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode)
            return;
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        string? msg = null;
        try
        {
            using var doc = JsonDocument.Parse(body);
            if (doc.RootElement.TryGetProperty("error", out var err))
                msg = err.GetString();
        }
        catch
        {
            // ignore
        }

        throw new InvalidOperationException(msg ?? body ?? response.ReasonPhrase ?? $"HTTP {(int)response.StatusCode}");
    }

    internal static async Task<T> ReadFromJsonAsync<T>(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        await EnsureSuccessAsync(response, cancellationToken);
        var r = await response.Content.ReadFromJsonAsync<T>(OilChangeJson.Options, cancellationToken);
        if (r is null && default(T) is null)
            throw new InvalidOperationException("Empty API response.");
        return r!;
    }
}
