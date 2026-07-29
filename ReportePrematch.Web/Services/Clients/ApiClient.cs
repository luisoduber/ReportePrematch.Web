using System.Net.Http.Json;
using System.Text.Json;
using System.Web;
using ReportePrematch.Web.Services.Interfaces;

namespace ReportePrematch.Web.Services.Clients;

/// <summary>
/// Cliente HTTP tipado para consumir la API de Reportes.
/// </summary>
public sealed class ApiClient(HttpClient http, ILogger<ApiClient> logger) : IApiClient
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true
    };

    // ── GET simple ────────────────────────────────────────────────────────────
    public async Task<T?> GetAsync<T>(string endpoint, CancellationToken ct = default)
    {
        try
        {
            var response = await http.GetAsync(endpoint, ct);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<T>(JsonOpts, ct);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error GET {Endpoint}", endpoint);
            throw;
        }
    }

    // ── GET con query string (Dictionary<string,string>) ─────────────────────
    public async Task<T?> GetQueryAsync<T>(string endpoint, Dictionary<string, string> query,
        CancellationToken ct = default)
    {
        try
        {
            var qs = string.Join("&", query
                .Where(kv => kv.Value is not null)
                .Select(kv => $"{HttpUtility.UrlEncode(kv.Key)}={HttpUtility.UrlEncode(kv.Value)}"));

            var url = qs.Length > 0 ? $"{endpoint}?{qs}" : endpoint;

            var response = await http.GetAsync(url, ct);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<T>(JsonOpts, ct);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error GET+QS {Endpoint}", endpoint);
            throw;
        }
    }

    // ── POST JSON body ────────────────────────────────────────────────────────
    public async Task<T?> PostAsync<T>(string endpoint, object body, CancellationToken ct = default)
    {
        try
        {
            var response = await http.PostAsJsonAsync(endpoint, body, ct);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<T>(JsonOpts, ct);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error POST {Endpoint}", endpoint);
            throw;
        }
    }
}