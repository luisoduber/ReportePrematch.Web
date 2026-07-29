namespace ReportePrematch.Web.Services.Interfaces;

/// <summary>Cliente HTTP tipado para consumir la API de Reportes.</summary>
public interface IApiClient
{
    Task<T?> GetAsync<T>(string endpoint, CancellationToken ct = default);
    Task<T?> GetQueryAsync<T>(string endpoint, Dictionary<string, string> query, CancellationToken ct = default);
    Task<T?> PostAsync<T>(string endpoint, object body, CancellationToken ct = default);
}
