using ReportePrematch.Web.Models.ApiReportes;
using ReportePrematch.Web.Services.Interfaces;
using System.Xml;

namespace ReportePrematch.Web.Services.Clients;

/// <summary>
/// Implementación de IReportesService.
///
/// ARQUITECTURA SOAP:
/// El servicio wsReportes.asmx expone SOLO dos métodos genéricos:
///   - ejecutarQuerys(clave, query, db)  → retorna DataSet XML (para SELECT)
///   - ejecutarComando(clave, query, db) → retorna bool (para INSERT/UPDATE/DELETE)
///
/// db=2 → base de datos principal (apuestas, agentes, clientes…)
/// db=3 → bases de datos externas (casinos, Dotsprin, Pragmatic, etc.)
///
/// Los reportes de la nueva API (VentasWeb, VentasTaq, AvilaCash, etc.)
/// se resuelven a través de IApiClient → GET https://localhost:44322/api/…
/// </summary>
public class ReportesService : IReportesService
{
    private readonly IApiClient _api;
    private readonly IConfiguration _config;
    private readonly ILogger<ReportesService> _logger;

    private readonly string _wsReportesUrl;
    private readonly string _brXmlUrl;
    private readonly string _licenciasUrl;
    private readonly string _clavePropia;

    public ReportesService(IApiClient api, IConfiguration config, ILogger<ReportesService> logger)
    {
        _api = api;
        _config = config;
        _logger = logger;

        _wsReportesUrl = config["SoapServices:WsReportesUrl"]
            ?? throw new InvalidOperationException("SoapServices:WsReportesUrl no configurado.");
        _brXmlUrl = config["SoapServices:BrXmlUrl"]
            ?? throw new InvalidOperationException("SoapServices:BrXmlUrl no configurado.");
        _licenciasUrl = config["SoapServices:LicenciasUrl"]
            ?? throw new InvalidOperationException("SoapServices:LicenciasUrl no configurado.");
        _clavePropia = config["SoapServices:ClavePropia"]
            ?? throw new InvalidOperationException("SoapServices:ClavePropia no configurado.");
    }

    /// <summary>Wrapper genérico que devuelven los endpoints de ReportePrematch.Api.</summary>
    private sealed record ApiWrapper<T>(bool Success, T? Data, string? Error);

    /* ══════════════════════════════════════════════════════════
       HELPERS SOAP — ejecutarQuerys
    ══════════════════════════════════════════════════════════ */

    /// <summary>Escapa comillas simples para SQL inline.</summary>
    private static string S(string? s) => (s ?? "").Replace("'", "''");

    /// <summary>
    /// Subquery que retorna la lista de agentes según rol.
    /// Se usa como IN ({SubqAgentes(...)}) en el WHERE de la query principal.
    /// </summary>
    private static string SubqAgentes(string agente, string pais, string role) =>
        role.ToUpper() switch
        {
            "USUARIO REGULAR"       => $"SELECT DISTINCT UPPER(nombreagente) FROM Agentes WHERE nombreagente = '{S(agente)}'",
            "USUARIO ADMINISTRADOR" => $"SELECT DISTINCT UPPER(nombreagente) FROM Agentes WHERE Paisdependencia = '{S(pais)}'",
            _                       => "SELECT DISTINCT UPPER(nombreagente) FROM Agentes"  // suAdmin / default
        };

    /// <summary>
    /// Llama a ejecutarQuerys del wsReportes.asmx con la query SQL indicada.
    /// Retorna las filas como lista de diccionarios con nombres de columna en clave.
    /// </summary>
    private async Task<List<Dictionary<string, object?>>> EjecutarQuerysAsync(string sql, int db = 2)
    {
        _logger.LogInformation("SOAP ejecutarQuerys db={Db} sql[{Len}]={Preview}",
            db, sql.Length, sql.Length > 120 ? sql[..120] + "…" : sql);
        try
        {
            var escapedClave = System.Security.SecurityElement.Escape(_clavePropia);
            var escapedSql   = System.Security.SecurityElement.Escape(sql);

            var envelope = $@"<?xml version=""1.0"" encoding=""utf-8""?>
<soap:Envelope xmlns:xsi=""http://www.w3.org/2001/XMLSchema-instance""
               xmlns:xsd=""http://www.w3.org/2001/XMLSchema""
               xmlns:soap=""http://schemas.xmlsoap.org/soap/envelope/"">
  <soap:Body>
    <ejecutarQuerys xmlns=""http://tempuri.org/"">
      <clave>{escapedClave}</clave>
      <query>{escapedSql}</query>
      <db>{db}</db>
    </ejecutarQuerys>
  </soap:Body>
</soap:Envelope>";

            using var client  = new HttpClient { Timeout = TimeSpan.FromSeconds(120) };
            client.DefaultRequestHeaders.Add("SOAPAction", "\"http://tempuri.org/ejecutarQuerys\"");

            var content  = new StringContent(envelope, System.Text.Encoding.UTF8, "text/xml");
            var response = await client.PostAsync(_wsReportesUrl, content);
            response.EnsureSuccessStatusCode();

            var xml = await response.Content.ReadAsStringAsync();
            return ParseDataSetXml(xml);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error SOAP ejecutarQuerys db={Db}", db);
            throw;
        }
    }

    /// <summary>
    /// Parsea la respuesta SOAP de ejecutarQuerys.
    /// El resultado es un DataSet serializado como XML con estructura
    /// &lt;ejecutarQuerysResult&gt;&lt;diffgr:diffgram&gt;&lt;NewDataSet&gt;&lt;Table .../&gt;...
    /// </summary>
    private static List<Dictionary<string, object?>> ParseDataSetXml(string xml)
    {
        var result = new List<Dictionary<string, object?>>();
        try
        {
            var doc = new XmlDocument();
            doc.LoadXml(xml);

            // El nodo resultado puede llamarse ejecutarQuerysResult
            var resultNode = doc.GetElementsByTagName("ejecutarQuerysResult");
            if (resultNode.Count == 0) return result;

            var innerXml = resultNode[0]!.InnerXml;
            if (string.IsNullOrWhiteSpace(innerXml)) return result;

            // El InnerXml contiene xs:schema + diffgr:diffgram/NewDataSet/Table
            var innerDoc = new XmlDocument();
            innerDoc.LoadXml(innerXml);

            // Buscar todos los nodos Table (filas) bajo NewDataSet a cualquier nivel
            var rows = innerDoc.SelectNodes("//*[local-name()='NewDataSet']/*");
            if (rows == null || rows.Count == 0)
            {
                // Fallback: cualquier descendiente que no sea schema, diffgram ni NewDataSet
                rows = innerDoc.SelectNodes("//*[local-name()!='schema' and local-name()!='diffgram' and local-name()!='NewDataSet' and not(ancestor::*[local-name()='schema'])]");
            }

            if (rows == null) return result;

            foreach (XmlNode row in rows)
            {
                var dict = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
                foreach (XmlNode field in row.ChildNodes)
                {
                    if (field.NodeType == XmlNodeType.Element)
                        dict[field.LocalName] = field.InnerText;
                }
                if (dict.Count > 0) result.Add(dict);
            }
        }
        catch (Exception ex)
        {
            _ = ex;
        }
        return result;
    }

    /* ══════════════════════════════════════════════════════════
       LISTA DE AGENTES  — vía API (no SOAP)
       Usa los endpoints existentes en GCITReportes:
         GET api/Reportes/ListaAgentes           → todos los agentes
         GET api/Reportes/ListaAgentesPorPais    → agentes de un país
       La lógica de rol se aplica aquí en el Web.
    ══════════════════════════════════════════════════════════ */

    // DTO mínimo para deserializar AgenteDTO del API
    private sealed record AgenteApiDto(string? NombreAgente, string? Paisdependencia);

    public async Task<List<string>> GetListAgenciasAsync(string agente, string pais = "", string role = "")
    {
        try
        {
            var roleUp = (role ?? "").Trim().ToUpperInvariant();

            List<AgenteApiDto>? lista;

            if (roleUp == "USUARIO ADMINISTRADOR" && !string.IsNullOrWhiteSpace(pais))
            {
                // Filtrar por país desde el API
                lista = await _api.GetQueryAsync<List<AgenteApiDto>>(
                    "api/Reportes/ListaAgentesPorPais",
                    new Dictionary<string, string> { ["pais"] = pais });
            }
            else
            {
                // Todos los agentes
                lista = await _api.GetAsync<List<AgenteApiDto>>("api/Reportes/ListaAgentes");

            }

            IEnumerable<string?> nombres = lista?
                .Select(a => a.NombreAgente?.Trim().ToUpperInvariant())
                .Where(n => !string.IsNullOrWhiteSpace(n))
                ?? [];

            // USUARIO REGULAR solo ve su propio agente
            if (roleUp == "USUARIO REGULAR" && !string.IsNullOrWhiteSpace(agente))
                nombres = nombres.Where(n => n == agente.Trim().ToUpperInvariant());

            return nombres.Distinct().OrderBy(n => n).ToList()!;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error GetListAgencias via API");
            return [];
        }
    }

    /* ══════════════════════════════════════════════════════════
       DEPORTES MÁS VENDIDOS
    ══════════════════════════════════════════════════════════ */

    public async Task<object> GetDeportesMasVendidosPorAgenteAsync(
        string fechaD, string fechaH, string agente, string pais, string role)
    {
        var query = new Dictionary<string, string>
        {
            ["fechaD"] = fechaD, ["fechaH"] = fechaH, ["agente"] = agente,
            ["pais"] = pais, ["role"] = role
        };
        var result = await _api.GetQueryAsync<List<DeportesMasVendidosDto>>(
            "api/Reportes/DeportesMasVendidosPorAgente", query);
        return result ?? new List<DeportesMasVendidosDto>();
    }

    public async Task<object> GetDeportesMasVendidosPorDeporteAsync(
        string fechaD, string fechaH, string agente, string pais, string role)
    {
        var query = new Dictionary<string, string>
        {
            ["fechaD"] = fechaD, ["fechaH"] = fechaH, ["agente"] = agente,
            ["pais"] = pais, ["role"] = role
        };
        var result = await _api.GetQueryAsync<List<DeportesMasVendidosDto>>(
            "api/Reportes/DeportesMasVendidosPorDeporte", query);
        return result ?? new List<DeportesMasVendidosDto>();
    }

    /* ══════════════════════════════════════════════════════════
       TICKETS DETALLADOS
    ══════════════════════════════════════════════════════════ */

    public async Task<object> GetTicketsDetalladosWebAsync(
        string fechaD, string fechaH, string agente, string pais, string role)
    {
        var query = new Dictionary<string, string>
        {
            ["fechaD"] = fechaD, ["fechaH"] = fechaH, ["agente"] = agente,
            ["pais"] = pais, ["role"] = role
        };
        var result = await _api.GetQueryAsync<List<TicketsDetalladosDto>>(
            "api/Reportes/TicketsDetalladosWeb", query);
        return result ?? new List<TicketsDetalladosDto>();
    }

    public async Task<object> GetTicketsDetalladosTaqAsync(
        string fechaD, string fechaH, string agente, string pais, string role)
    {
        var query = new Dictionary<string, string>
        {
            ["fechaD"] = fechaD, ["fechaH"] = fechaH, ["agente"] = agente,
            ["pais"] = pais, ["role"] = role
        };
        var result = await _api.GetQueryAsync<List<TicketsDetalladosDto>>(
            "api/Reportes/TicketsDetalladosTaq", query);
        return result ?? new List<TicketsDetalladosDto>();
    }

    /* ══════════════════════════════════════════════════════════
       VENTAS POR PAÍS
    ══════════════════════════════════════════════════════════ */

    public async Task<object> GetVentasPorPaisWebAsync(string fechaD, string fechaH, string pais)
    {
        var query = new Dictionary<string, string>
        {
            ["fechaD"] = fechaD, ["fechaH"] = fechaH,
            ["agente"] = "TODOS", ["pais"] = pais, ["role"] = string.Empty
        };
        var result = await _api.GetQueryAsync<List<VentasPorPaisDto>>(
            "api/Reportes/VentasPorPaisWeb", query);
        return result ?? new List<VentasPorPaisDto>();
    }

    public async Task<object> GetVentasPorPaisTaqAsync(string fechaD, string fechaH, string pais)
    {
        var query = new Dictionary<string, string>
        {
            ["fechaD"] = fechaD, ["fechaH"] = fechaH,
            ["agente"] = "TODOS", ["pais"] = pais, ["role"] = string.Empty
        };
        var result = await _api.GetQueryAsync<List<VentasPorPaisDto>>(
            "api/Reportes/VentasPorPaisTaq", query);
        return result ?? new List<VentasPorPaisDto>();
    }

    /* ══════════════════════════════════════════════════════════
       RETIROS Y RECARGAS (db=3)
    ══════════════════════════════════════════════════════════ */

    public async Task<object> GetRetirosRecargasAsync(
        string fechaD, string fechaH, string agente, string pais, string role)
    {
        var query = new Dictionary<string, string>
        {
            ["fechaD"] = fechaD, ["fechaH"] = fechaH, ["agente"] = agente,
            ["pais"] = pais, ["role"] = role
        };
        var result = await _api.GetQueryAsync<List<RetirosRecargasApiDto>>(
            "api/Reportes/RetirosRecargas", query);
        return result ?? new List<RetirosRecargasApiDto>();
    }

    /* ══════════════════════════════════════════════════════════
       SALDOS DISPONIBLES
    ══════════════════════════════════════════════════════════ */

    public async Task<object> GetSaldosDisponiblesAsync(string agente)
    {
        var result = await _api.GetQueryAsync<List<RetirosRecargasApiDto>>(
            "api/Reportes/SaldosDisponibles",
            new Dictionary<string, string> { ["agente"] = agente });
        return result ?? new List<RetirosRecargasApiDto>();
    }

    /* ══════════════════════════════════════════════════════════
       VENTAS POR GAME ID
    ══════════════════════════════════════════════════════════ */

    public async Task<object> GetVentasAgenteGameIDAsync(
        string agente, string pais, string role, string gameId, string filtApuesta)
    {
        var query = new Dictionary<string, string>
        {
            ["agente"]      = agente,
            ["pais"]        = pais,
            ["role"]        = role,
            ["gameId"]      = gameId,
            ["filtApuesta"] = filtApuesta
        };
        var result = await _api.GetQueryAsync<List<GameIDApiDto>>(
            "api/Reportes/VentasAgenteGameID", query);
        return result ?? new List<GameIDApiDto>();
    }

    /* ══════════════════════════════════════════════════════════
       CASINO MAQUINITAS (db=3)
    ══════════════════════════════════════════════════════════ */

    public async Task<object> GetCasinoMaquinitasAsync(
        string fechaD, string fechaH, string agente, string pais, string role)
    {
        var query = new Dictionary<string, string>
        {
            ["fechaD"] = fechaD, ["fechaH"] = fechaH, ["agente"] = agente,
            ["pais"] = pais, ["role"] = role
        };
        var result = await _api.GetQueryAsync<List<CasinoDto>>(
            "api/Reportes/CasinoMaquinitas", query);
        return result ?? new List<CasinoDto>();
    }

    /* ══════════════════════════════════════════════════════════
       CASINO SLOT GRECIA (db=3)
    ══════════════════════════════════════════════════════════ */

    public async Task<object> GetCasinoSlotGreciaAsync(
        string fechaD, string fechaH, string agente, string pais, string role)
    {
        var query = new Dictionary<string, string>
        {
            ["fechaD"] = fechaD, ["fechaH"] = fechaH, ["agente"] = agente,
            ["pais"] = pais, ["role"] = role
        };
        var result = await _api.GetQueryAsync<List<CasinoDto>>(
            "api/Reportes/CasinoSlotGrecia", query);
        return result ?? new List<CasinoDto>();
    }

    /* ══════════════════════════════════════════════════════════
       ICON SLOT (db=3)
    ══════════════════════════════════════════════════════════ */

    public async Task<object> GetIconSlotAsync(
        string fechaD, string fechaH, string agente, string pais, string role)
    {
        var query = new Dictionary<string, string>
        {
            ["fechaD"] = fechaD, ["fechaH"] = fechaH, ["agente"] = agente,
            ["pais"] = pais, ["role"] = role
        };
        var result = await _api.GetQueryAsync<List<CasinoDto>>(
            "api/Reportes/CasinoIconSlot", query);
        return result ?? new List<CasinoDto>();
    }

    /* ══════════════════════════════════════════════════════════
       CASINO SLOT RUSSIA (db=3)
    ══════════════════════════════════════════════════════════ */

    public async Task<object> GetCasinoSlotRussiaAsync(
        string fechaD, string fechaH, string agente, string pais, string role)
    {
        var query = new Dictionary<string, string>
        {
            ["fechaD"] = fechaD, ["fechaH"] = fechaH, ["agente"] = agente,
            ["pais"] = pais, ["role"] = role
        };
        var result = await _api.GetQueryAsync<List<CasinoDto>>(
            "api/Reportes/CasinoSlotRussia", query);
        return result ?? new List<CasinoDto>();
    }

    /* ══════════════════════════════════════════════════════════
       PROPS (db=3)
    ══════════════════════════════════════════════════════════ */

    public async Task<object> GetPropsAsync(
        string fechaD, string fechaH, string agente, string pais, string role)
    {
        var query = new Dictionary<string, string>
        {
            ["fechaD"] = fechaD, ["fechaH"] = fechaH, ["agente"] = agente,
            ["pais"] = pais, ["role"] = role
        };
        var result = await _api.GetQueryAsync<List<PropsApiDto>>(
            "api/Reportes/Props", query);
        return result ?? new List<PropsApiDto>();
    }

    /* ══════════════════════════════════════════════════════════
       POKER (db=2)
    ══════════════════════════════════════════════════════════ */

    public async Task<object> GetPokerAsync(
        string fechaD, string fechaH, string agente, string pais, string role)
    {
        var query = new Dictionary<string, string>
        {
            ["fechaD"] = fechaD, ["fechaH"] = fechaH, ["agente"] = agente,
            ["pais"] = pais, ["role"] = role
        };
        var result = await _api.GetQueryAsync<List<CasinoDto>>(
            "api/Reportes/Poker", query);
        return result ?? new List<CasinoDto>();
    }

    /* ══════════════════════════════════════════════════════════
       PRAGMATIC (db=3)
    ══════════════════════════════════════════════════════════ */

    public async Task<object> GetPragmaticAsync(
        string fechaD, string fechaH, string agente, string pais, string role)
    {
        var query = new Dictionary<string, string>
        {
            ["fechaD"] = fechaD, ["fechaH"] = fechaH, ["agente"] = agente,
            ["pais"] = pais, ["role"] = role
        };
        var result = await _api.GetQueryAsync<List<CasinoDto>>(
            "api/Reportes/Pragmatic", query);
        return result ?? new List<CasinoDto>();
    }

    /* ══════════════════════════════════════════════════════════
       VIRTUALES BR (db=3)
    ══════════════════════════════════════════════════════════ */

    public async Task<object> GetVirtualesBRAsync(
        string fechaD, string fechaH, string agente, string pais, string role)
    {
        var query = new Dictionary<string, string>
        {
            ["fechaD"] = fechaD, ["fechaH"] = fechaH, ["agente"] = agente,
            ["pais"] = pais, ["role"] = role
        };
        var result = await _api.GetQueryAsync<List<PropsApiDto>>(
            "api/Reportes/VirtualesBR", query);
        return result ?? new List<PropsApiDto>();
    }

    /* ══════════════════════════════════════════════════════════
       VIRTUALES CARIBE (db=3)
    ══════════════════════════════════════════════════════════ */

    public async Task<object> GetVirtualesCarAsync(
        string fechaD, string fechaH, string agente, string pais, string role)
    {
        var query = new Dictionary<string, string>
        {
            ["fechaD"] = fechaD, ["fechaH"] = fechaH, ["agente"] = agente,
            ["pais"] = pais, ["role"] = role
        };
        var result = await _api.GetQueryAsync<List<CasinoDto>>(
            "api/Reportes/VirtualesCar", query);
        return result ?? new List<CasinoDto>();
    }

    /* ══════════════════════════════════════════════════════════
       TICKETS PROMO (db=3)
    ══════════════════════════════════════════════════════════ */

    public async Task<object> GetTicketsPromoAsync(
        string fechaD, string fechaH, string agente, string webSite)
    {
        var desde = $"{fechaD} 00:00:00";
        var hasta = $"{fechaH} 23:59:59";
        var filtroWebSite = string.IsNullOrEmpty(webSite) ? "" : $"WebSite='{S(webSite)}' AND ";
        var filtroAgente  = string.IsNullOrEmpty(agente)  ? "" : $"AND pt.Agente='{S(agente)}'";

        var sql = $@"SELECT TOP 20000
    pt.idPromocion, pr.nombre AS Promocion, pt.idCliente, pt.Usuario,
    pt.idAgente, pt.Agente, pt.idEmpresa, pt.Empresa,
    pt.ticket, pt.fecha, pt.monto, pt.ValorDivisa, pt.MontoDiv, pt.WebSite
FROM [GCITBR].[dbo].[PromoTicket] AS pt
INNER JOIN [GCITBR].[dbo].[Promocion] AS pr ON pt.idPromocion = pr.id
WHERE {filtroWebSite}pt.fecha BETWEEN '{desde}' AND '{hasta}'
{filtroAgente}";

        return await EjecutarQuerysAsync(sql, 3);
    }

    /* ══════════════════════════════════════════════════════════
       CLIENTES REGISTRADOS (db=3)
    ══════════════════════════════════════════════════════════ */

    public async Task<object> GetClientesRegAsync(
        string fechaD, string fechaH, string agente, string role, string pais)
    {
        var desde = $"{fechaD} 00:00:00";
        var hasta = $"{fechaH} 23:59:59";
        var esAdmin = role.ToUpper() == "USUARIO SUADMIN";

        var filtro = esAdmin
            ? ""
            : !string.IsNullOrEmpty(agente)
                ? $"AND ci.NombreAgente = '{S(agente)}' "
                : $"AND ci.NombreAgente IN ({SubqAgentes(agente, pais, role)}) ";

        var sql = $@"SELECT c.id, c.Usuario, c.Email, c.Telefono,
       ci.NombreAgente, ci.idAgente, COUNT(ap.ticket) AS Tickets
FROM Clientes AS c
INNER JOIN ClientesID AS ci ON c.id = ci.idCliente
LEFT JOIN Apuestas AS ap ON ap.idCliente = c.id
WHERE ci.FechaIngreso BETWEEN '{desde}' AND '{hasta}'
{filtro}
GROUP BY c.id, c.Usuario, c.Email, c.Telefono, ci.NombreAgente, ci.idAgente";

        return await EjecutarQuerysAsync(sql, 3);
    }

    /* ══════════════════════════════════════════════════════════
       BÚSQUEDA DE TICKETS
    ══════════════════════════════════════════════════════════ */

    public async Task<object> GetBusquedaTicketsWebAsync(
        string fechaD, string fechaH, string agente, string operacion,
        string ticket, string pais, string role)
    {
        var desde = string.IsNullOrEmpty(fechaD) ? "CONVERT(varchar, GETDATE(), 111) + ' 00:00:00'" : $"'{fechaD} 00:00:00'";
        var hasta  = string.IsNullOrEmpty(fechaH) ? "CONVERT(varchar, GETDATE(), 111) + ' 23:59:59'" : $"'{fechaH} 23:59:59'";
        var esAdmin = role.ToUpper() == "USUARIO SUADMIN";

        var filtroAgente = esAdmin
            ? ""
            : !string.IsNullOrEmpty(agente)
                ? $"AND a.nombreagente = '{S(agente)}' "
                : $"AND a.nombreagente IN ({SubqAgentes(agente, pais, role)}) ";

        string filtroOp;
        if (!string.IsNullOrEmpty(ticket))
            filtroOp = $"AND a.ticket = '{S(ticket)}'";
        else if (!string.IsNullOrEmpty(operacion))
            filtroOp = $"AND a.operacion = {S(operacion)}";
        else
            filtroOp = "";

        var sql = $@"SELECT a.nombreagente, a.Ticket, d.usuario, a.Fecha,
       b.tipoAp, a.operacion, b.gano, a.montoTotal, a.ganando,
       b.nss, b.equipo, b.fechajuego, c.nombreliga, c.nombre, b.CategoriaAp, b.logro
FROM apuestas AS a, apuestaequipo AS b, betradaruof.dbo.feeds AS c, clientes AS d
WHERE a.ticket = b.ticket
  AND a.idCliente = d.id
  AND b.nss = c.nss
  AND a.idCliente != 0
  AND a.fecha BETWEEN {desde} AND {hasta}
  {filtroOp}
  {filtroAgente}
ORDER BY 1,2,3 ASC";

        return await EjecutarQuerysAsync(sql, 2);
    }

    public async Task<object> GetBusquedaTicketsTaqAsync(
        string fechaD, string fechaH, string agente, string operacion,
        string ticket, string pais, string role)
    {
        var desde = string.IsNullOrEmpty(fechaD) ? "CONVERT(varchar, GETDATE(), 111) + ' 00:00:00'" : $"'{fechaD} 00:00:00'";
        var hasta  = string.IsNullOrEmpty(fechaH) ? "CONVERT(varchar, GETDATE(), 111) + ' 23:59:59'" : $"'{fechaH} 23:59:59'";
        var esAdmin = role.ToUpper() == "USUARIO SUADMIN";

        var filtroAgente = esAdmin
            ? ""
            : !string.IsNullOrEmpty(agente)
                ? $"AND a.nombreagente = '{S(agente)}' "
                : $"AND a.nombreagente IN ({SubqAgentes(agente, pais, role)}) ";

        string filtroOp;
        if (!string.IsNullOrEmpty(ticket))
            filtroOp = $"AND a.ticket = '{S(ticket)}'";
        else if (operacion == "99")
            filtroOp = "AND a.pagado = 0 AND (a.operacion = 1 OR a.operacion = 3)";
        else if (!string.IsNullOrEmpty(operacion))
            filtroOp = $"AND a.operacion = {S(operacion)}";
        else
            filtroOp = "";

        var sql = $@"SELECT a.nombreagente, a.Ticket, a.usuario, a.Fecha,
       b.tipoAp, a.operacion, b.gano, a.montoTotal, a.ganando,
       b.nss, b.equipo, b.fechajuego, c.nombreliga, c.nombre, b.CategoriaAp, b.logro
FROM Apuestas AS a, ApuestaEquipo AS b, betradaruof.dbo.feeds AS c
WHERE a.ticket = b.ticket
  AND b.nss = c.nss
  AND a.idCliente = 0
  AND a.fecha BETWEEN {desde} AND {hasta}
  {filtroOp}
  {filtroAgente}
ORDER BY 1,2,3 ASC";

        return await EjecutarQuerysAsync(sql, 2);
    }

    /* ══════════════════════════════════════════════════════════
       BÚSQUEDA TICKETS  →  REST API (GCITReportes)
    ══════════════════════════════════════════════════════════ */

    public async Task<object> GetBusquedaTicketsTaqApiAsync(string fecha, string agente, long ticket, string operacion)
    {
        var query = new Dictionary<string, string>
        {
            ["fecha"]     = fecha ?? string.Empty,
            ["agente"]    = agente ?? string.Empty,
            ["ticket"]    = ticket.ToString(),
            ["operacion"] = operacion ?? string.Empty
        };
        var result = await _api.GetQueryAsync<List<BusquedaTicketsDto>>(
            "api/Reportes/BusquedaTicketTaq", query);
        return result ?? new List<BusquedaTicketsDto>();
    }

    public async Task<object> GetBusquedaTicketsWebApiAsync(string fecha, string agente, long ticket, string operacion)
    {
        var query = new Dictionary<string, string>
        {
            ["fecha"]     = fecha ?? string.Empty,
            ["agente"]    = agente ?? string.Empty,
            ["ticket"]    = ticket.ToString(),
            ["operacion"] = operacion ?? string.Empty
        };
        var result = await _api.GetQueryAsync<List<BusquedaTicketsDto>>(
            "api/Reportes/BusquedaTicketWeb", query);
        return result ?? new List<BusquedaTicketsDto>();
    }

    /* ══════════════════════════════════════════════════════════
       VENTAS DETALLADAS LIVE  →  REST API
    ══════════════════════════════════════════════════════════ */

    public async Task<object> GetVentasDetalladasLiveAsync(
        string fechaD, string fechaH, string agente, string pais, string role)
    {
        var query = new Dictionary<string, string>
        {
            ["fechaD"] = fechaD, ["fechaH"] = fechaH,
            ["agente"] = agente, ["pais"] = pais, ["role"] = role
        };
        var result = await _api.GetQueryAsync<List<VentasDetalladasLiveDto>>(
            "api/Reportes/VentasDetalladasLive", query);
        return result ?? new List<VentasDetalladasLiveDto>();
    }

    /* ══════════════════════════════════════════════════════════
       TICKETS EN JUEGO  →  REST API
    ══════════════════════════════════════════════════════════ */

    public async Task<object> GetTicketsEnJuegoPorPagarTaqAsync(string fechaD, string fechaH, string agente)
    {
        var query = new Dictionary<string, string>
        {
            ["fechaD"] = fechaD, ["fechaH"] = fechaH, ["agente"] = agente
        };
        var result = await _api.GetQueryAsync<List<TicketsEnJuegoDto>>(
            "api/Reportes/TicketsEnJuegoPorPagarTaq", query);
        return result ?? new List<TicketsEnJuegoDto>();
    }

    public async Task<object> GetTicketsEnJuegoPorCobrarTaqAsync(string fechaD, string fechaH, string agente)
    {
        var query = new Dictionary<string, string>
        {
            ["fechaD"] = fechaD, ["fechaH"] = fechaH, ["agente"] = agente
        };
        var result = await _api.GetQueryAsync<List<TicketsEnJuegoDto>>(
            "api/Reportes/TicketsEnJuegoPorCobrarTaq", query);
        return result ?? new List<TicketsEnJuegoDto>();
    }

    public async Task<object> GetTicketsEnJuegoPorPagarWebAsync(string fechaD, string fechaH, string agente)
    {
        var query = new Dictionary<string, string>
        {
            ["fechaD"] = fechaD, ["fechaH"] = fechaH, ["agente"] = agente
        };
        var result = await _api.GetQueryAsync<List<TicketsEnJuegoDto>>(
            "api/Reportes/TicketsEnJuegoPorPagarWeb", query);
        return result ?? new List<TicketsEnJuegoDto>();
    }

    public async Task<object> GetTicketsEnJuegoPorCobrarWebAsync(string fechaD, string fechaH, string agente)
    {
        var query = new Dictionary<string, string>
        {
            ["fechaD"] = fechaD, ["fechaH"] = fechaH, ["agente"] = agente
        };
        var result = await _api.GetQueryAsync<List<TicketsEnJuegoDto>>(
            "api/Reportes/TicketsEnJuegoPorCobrarWeb", query);
        return result ?? new List<TicketsEnJuegoDto>();
    }

    /* ══════════════════════════════════════════════════════════
       VENTAS POR AGENTE NIKOLS WEB  →  REST API
    ══════════════════════════════════════════════════════════ */

    public async Task<object> GetVentasPorAgenteNikolsWebAsync(
        string fechaD, string fechaH, string agente, string pais, string role)
    {
        var query = new Dictionary<string, string>
        {
            ["fechaD"] = fechaD, ["fechaH"] = fechaH,
            ["agente"] = agente, ["pais"] = pais, ["role"] = role
        };
        var result = await _api.GetQueryAsync<List<VentasPorAgenteNikolsWebDto>>(
            "api/Reportes/VentasPorAgenteNikolsWeb", query);
        return result ?? new List<VentasPorAgenteNikolsWebDto>();
    }

    public async Task<object> GetVentasPorAgenteNikolsTaqAsync(
        string fechaD, string fechaH, string agente, string local, string pais, string role)
    {
        var query = new Dictionary<string, string>
        {
            ["fechaD"] = fechaD, ["fechaH"] = fechaH, ["agente"] = agente,
            ["local"] = local, ["pais"] = pais, ["role"] = role
        };
        var result = await _api.GetQueryAsync<List<NikolsTaqDto>>(
            "api/Reportes/VentasPorAgenteNikolsTaq", query);
        return result ?? new List<NikolsTaqDto>();
    }

    /* ══════════════════════════════════════════════════════════
       ESTADÍSTICAS
    ══════════════════════════════════════════════════════════ */

    public async Task<object> GetEstadisticasWebAsync(
        string fechaD, string fechaH, string agente, string pais, string role)
    {
        var query = new Dictionary<string, string>
        {
            ["fechaD"] = fechaD, ["fechaH"] = fechaH,
            ["agente"] = agente, ["pais"] = pais, ["role"] = role
        };
        var result = await _api.GetQueryAsync<List<EstadisticasTaqDto>>(
            "api/Reportes/EstadisticasWeb", query);
        return result ?? new List<EstadisticasTaqDto>();
    }

    public async Task<object> GetEstadisticasTaqAsync(
        string fechaD, string fechaH, string agente, string local, string pais, string role)
    {
        var query = new Dictionary<string, string>
        {
            ["fechaD"] = fechaD, ["fechaH"] = fechaH,
            ["agente"] = agente, ["local"] = local, ["pais"] = pais, ["role"] = role
        };
        var result = await _api.GetQueryAsync<List<EstadisticasTaqDto>>(
            "api/Reportes/EstadisticasTaq", query);
        return result ?? new List<EstadisticasTaqDto>();
    }

    /* ══════════════════════════════════════════════════════════
       HIPISMO
    ══════════════════════════════════════════════════════════ */

    public async Task<object> GetHipismoAsync(
        string fechaD, string fechaH, string agente, string pais, string role)
    {
        var query = new Dictionary<string, string>
        {
            ["fechaD"] = fechaD, ["fechaH"] = fechaH, ["agente"] = agente,
            ["pais"]   = pais,   ["role"]   = role
        };
        var result = await _api.GetQueryAsync<List<CasinoDto>>(
            "api/Hipismo/ReporteCaballo", query);
        return result ?? new List<CasinoDto>();
    }

    /* ══════════════════════════════════════════════════════════
       CIERRE TAQUILLA (usa servicio webControlLicencias, no wsReportes)
    ══════════════════════════════════════════════════════════ */

    public async Task<object> GetCierreTaquillaAsync(
        string agente, string agencia, string usuario, string fecha)
    {
        _logger.LogInformation("SOAP Licencias → GetCierreTaquilla agente={Agente}", agente);
        try
        {
            var escapedClave    = System.Security.SecurityElement.Escape(_clavePropia);
            var escapedAgente   = System.Security.SecurityElement.Escape(agente);
            var escapedAgencia  = System.Security.SecurityElement.Escape(agencia);
            var escapedUsuario  = System.Security.SecurityElement.Escape(usuario);
            var escapedFecha    = System.Security.SecurityElement.Escape(fecha);

            var envelope = $@"<?xml version=""1.0"" encoding=""utf-8""?>
<soap:Envelope xmlns:xsi=""http://www.w3.org/2001/XMLSchema-instance""
               xmlns:xsd=""http://www.w3.org/2001/XMLSchema""
               xmlns:soap=""http://schemas.xmlsoap.org/soap/envelope/"">
  <soap:Body>
    <GetCierreTaquilla xmlns=""http://tempuri.org/"">
      <agente>{escapedAgente}</agente>
      <agencia>{escapedAgencia}</agencia>
      <usuario>{escapedUsuario}</usuario>
      <fecha>{escapedFecha}</fecha>
      <ClavePropia>{escapedClave}</ClavePropia>
    </GetCierreTaquilla>
  </soap:Body>
</soap:Envelope>";

            using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(120) };
            client.DefaultRequestHeaders.Add("SOAPAction", "\"http://tempuri.org/GetCierreTaquilla\"");

            var content  = new StringContent(envelope, System.Text.Encoding.UTF8, "text/xml");
            var response = await client.PostAsync(_licenciasUrl, content);
            response.EnsureSuccessStatusCode();

            var xml  = await response.Content.ReadAsStringAsync();
            var doc  = new XmlDocument();
            doc.LoadXml(xml);

            var resultNodes = doc.GetElementsByTagName("GetCierreTaquillaResult");
            if (resultNodes.Count == 0) return new List<object>();

            var innerXml = resultNodes[0]!.InnerXml;
            if (string.IsNullOrWhiteSpace(innerXml)) return new List<object>();

            var innerDoc = new XmlDocument();
            innerDoc.LoadXml(innerXml);
            var rows = innerDoc.SelectNodes("//*[local-name()='NewDataSet']/*");
            if (rows == null) return new List<object>();

            var result = new List<Dictionary<string, object?>>();
            foreach (XmlNode row in rows)
            {
                var dict = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
                foreach (XmlNode field in row.ChildNodes)
                    if (field.NodeType == XmlNodeType.Element)
                        dict[field.LocalName] = field.InnerText;
                if (dict.Count > 0) result.Add(dict);
            }
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error SOAP Licencias GetCierreTaquilla");
            throw;
        }
    }

    /* ══════════════════════════════════════════════════════════
       REPORTES VÍA NUEVA API (localhost:44322)
    ══════════════════════════════════════════════════════════ */

    public async Task<object> GetVentasPorAgenteWebAsync(
        string fechaD, string fechaH, string agente, string pais, string role)
    {
        var query = new Dictionary<string, string>
        {
            ["fechaD"] = fechaD, ["fechaH"] = fechaH, ["agente"] = agente
        };
        if (!string.IsNullOrEmpty(pais)) query["pais"] = pais;
        if (!string.IsNullOrEmpty(role)) query["role"] = role;

        var result = await _api.GetQueryAsync<List<VentasPorAgenteWebDto>>(
            "api/Reportes/VentasPorAgenteWeb", query);
        return result ?? new List<VentasPorAgenteWebDto>();
    }

    public async Task<object> GetVentasPorAgenteTaqAsync(
        string fechaD, string fechaH, string agente, string local, string pais, string role)
    {
        var query = new Dictionary<string, string>
        {
            ["fechaD"] = fechaD, ["fechaH"] = fechaH, ["agente"] = agente
        };
        if (!string.IsNullOrEmpty(local)) query["local"] = local;
        if (!string.IsNullOrEmpty(pais))  query["pais"]  = pais;
        if (!string.IsNullOrEmpty(role))  query["role"]  = role;

        var result = await _api.GetQueryAsync<List<VentasPorAgenteTaqDto>>(
            "api/Reportes/VentasPorAgenteTaq", query);
        return result ?? new List<VentasPorAgenteTaqDto>();
    }

    public async Task<object> GetRepTransaccionesAsync(
        string fechaD, string fechaH, string agente, string webSite)
    {
        var query = new Dictionary<string, string>
        {
            ["fechaD"] = fechaD, ["fechaH"] = fechaH, ["agente"] = agente
        };
        if (!string.IsNullOrEmpty(webSite)) query["webSite"] = webSite;

        var result = await _api.GetQueryAsync<List<AvilaCashDto>>(
            "api/Reportes/AvilaCash", query);
        return result ?? new List<AvilaCashDto>();
    }

    public async Task<object> GetDetallesHipismoAsync(
        string fechaD, string fechaH, string usuario)
    {
        var result = await _api.GetQueryAsync<List<DetallesHipismoDto>>(
            "api/Hipismo/DetallesHipismo",
            new Dictionary<string, string>
            {
                ["fechaD"] = fechaD, ["fechaH"] = fechaH, ["usuario"] = usuario
            });
        return result ?? new List<DetallesHipismoDto>();
    }

    // TODO: GetIconBetAsync — pendiente de conectar al nuevo API
    /*
    public async Task<object> GetIconBetAsync(
        string fechaD, string fechaH, string agente, string pais, string role) => ...
    */

    public async Task<object> GetLoteriasAsync(
        string fechaD, string fechaH, string agente, string role = "", string pais = "")
    {
        var query = new Dictionary<string, string>
        {
            ["fechaD"] = fechaD, ["fechaH"] = fechaH, ["agente"] = agente
        };
        if (!string.IsNullOrEmpty(pais)) query["pais"] = pais;
        if (!string.IsNullOrEmpty(role)) query["role"] = role;

        var result = await _api.GetQueryAsync<List<LoteriasDto>>(
            "api/Reportes/Loterias", query);
        return result ?? new List<LoteriasDto>();
    }

    public async Task<object> GetLoteriasTripleAsync(
        string fechaD, string fechaH, string agente, string role)
    {
        var query = new Dictionary<string, string>
        {
            ["fechaD"] = fechaD, ["fechaH"] = fechaH,
            ["agente"] = agente, ["role"] = role
        };
        var result = await _api.GetQueryAsync<List<LoteriasDto>>(
            "api/Reportes/Triples", query);
        return result ?? new List<LoteriasDto>();
    }

    public async Task<object> GetAviatrixAsync(
        string fechaD, string fechaH, string agente, string role = "", string pais = "")
    {
        var query = new Dictionary<string, string>
        {
            ["fechaD"] = fechaD, ["fechaH"] = fechaH, ["agente"] = agente ?? ""
        };
        if (!string.IsNullOrEmpty(pais)) query["pais"] = pais;
        if (!string.IsNullOrEmpty(role)) query["role"] = role;

        var wrapper = await _api.GetQueryAsync<ApiWrapper<List<AviatrixDto>>>(
            "api/Reportes/Aviatrix", query);
        return wrapper?.Data ?? new List<AviatrixDto>();
    }

    public async Task<object> GetFantasyBsbAsync(
        string fechaD, string fechaH, string agente, string role = "", string pais = "")
    {
        var query = new Dictionary<string, string>
        {
            ["fechaD"] = fechaD, ["fechaH"] = fechaH, ["agente"] = agente
        };
        if (!string.IsNullOrEmpty(pais)) query["pais"] = pais;
        if (!string.IsNullOrEmpty(role)) query["role"] = role;

        var wrapper = await _api.GetQueryAsync<ApiWrapper<List<FantasyBsbDto>>>(
            "api/Reportes/FantasyBsb", query);
        return wrapper?.Data ?? new List<FantasyBsbDto>();
    }

    public async Task<object> GetDashboardClientesAsync(string fechaD, string fechaH, string agente)
    {
        var result = await _api.GetQueryAsync<object>(
            "api/Dashboard/GetDashboardClientes",
            new Dictionary<string, string>
            {
                ["fechaD"] = fechaD, ["fechaH"] = fechaH, ["agente"] = agente ?? ""
            });
        return result;
    }

    public async Task<object> GetDashboardExtendidoAsync(string fechaD, string fechaH, string agente, string pais)
    {
        var query = new Dictionary<string, string>
        {
            ["fechaD"] = fechaD,
            ["fechaH"] = fechaH,
            ["agente"] = agente ?? "",
            ["pais"]   = pais   ?? ""
        };
        var result = await _api.GetQueryAsync<object>("api/Dashboard/GetDashboardExtendido", query);
        return result;
    }

    public async Task<object> GetVentasMonedaExtendidoAsync(string fechaD, string fechaH, string agente, string pais)
    {
        var query = new Dictionary<string, string>
        {
            ["fechaD"] = fechaD,
            ["fechaH"] = fechaH,
            ["agente"] = agente ?? "",
            ["pais"]   = pais   ?? ""
        };
        var result = await _api.GetQueryAsync<object>("api/Dashboard/GetVentasMonedaExtendido", query);
        return result;
    }

    public async Task<object> GetChartsExtendidoAsync(string fechaD, string fechaH, string agente, string pais)
    {
        var query = new Dictionary<string, string>
        {
            ["fechaD"] = fechaD,
            ["fechaH"] = fechaH,
            ["agente"] = agente ?? "",
            ["pais"]   = pais   ?? ""
        };
        var result = await _api.GetQueryAsync<object>("api/Dashboard/GetChartsExtendido", query);
        return result;
    }

    public async Task<object> GetTablesExtendidoAsync(string fechaD, string fechaH, string agente, string pais)
    {
        var query = new Dictionary<string, string>
        {
            ["fechaD"] = fechaD,
            ["fechaH"] = fechaH,
            ["agente"] = agente ?? "",
            ["pais"]   = pais   ?? ""
        };
        var result = await _api.GetQueryAsync<object>("api/Dashboard/GetTablesExtendido", query);
        return result;
    }

    // ── Gaming / Endorphine (GCITReportes API — cliente independiente) ─────────
    private async Task<object> CallGamingApiAsync(string endpoint, string fechaInicio, string fechaFin, string agente)
    {
        var baseUrl = _config["GamingApi:BaseUrl"] ?? "https://localhost:44322";
        var qs = $"fechaInicio={Uri.EscapeDataString(fechaInicio)}" +
                 $"&fechaFin={Uri.EscapeDataString(fechaFin)}" +
                 $"&agente={Uri.EscapeDataString(agente ?? "")}";
        var url = $"{baseUrl.TrimEnd('/')}/{endpoint}?{qs}";

        var handler = new System.Net.Http.HttpClientHandler
        {
            ServerCertificateCustomValidationCallback =
                System.Net.Http.HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
        };
        using var http = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(120) };

        var response = await http.GetAsync(url);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<object>()
               ?? new List<object>();
    }

    public Task<object> GetCasino7777Async(string fechaInicio, string fechaFin, string agente)
        => CallGamingApiAsync("api/Casino/Gaming/Resumen", fechaInicio, fechaFin, agente);

    public Task<object> GetCasino7777ResumenGeneralAsync(string fechaInicio, string fechaFin, string agente)
        => CallGamingApiAsync("api/Casino/Gaming/ResumenGeneral", fechaInicio, fechaFin, agente);

    public Task<List<string>> GetCasino7777AgentesAsync()
        => GetEndorphineAgentesAsync(); // misma fuente: /api/Reportes/ListaAgentes

    public Task<object> GetEndorphineAsync(string fechaInicio, string fechaFin, string agente)
        => CallGamingApiAsync("api/Casino/Endorphine/Resumen", fechaInicio, fechaFin, agente);

    public Task<object> GetEndorphineResumenGeneralAsync(string fechaInicio, string fechaFin, string agente)
        => CallGamingApiAsync("api/Casino/Endorphine/ResumenGeneral", fechaInicio, fechaFin, agente);

    public Task<object> GetInOutGamingAsync(string fechaInicio, string fechaFin, string agente)
        => CallGamingApiAsync("api/Casino/InOutGaming/Resumen", fechaInicio, fechaFin, agente);

    public Task<object> GetInOutGamingResumenGeneralAsync(string fechaInicio, string fechaFin, string agente)
        => CallGamingApiAsync("api/Casino/InOutGaming/ResumenGeneral", fechaInicio, fechaFin, agente);

    public Task<List<string>> GetInOutGamingAgentesAsync()
        => GetEndorphineAgentesAsync(); // misma fuente: /api/Reportes/ListaAgentes

    public async Task<List<string>> GetEndorphineAgentesAsync()
    {
        var baseUrl = _config["GamingApi:BaseUrl"] ?? "https://localhost:44322";
        var url = $"{baseUrl.TrimEnd('/')}/api/Reportes/ListaAgentes";
        var handler = new System.Net.Http.HttpClientHandler
        {
            ServerCertificateCustomValidationCallback =
                System.Net.Http.HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
        };
        using var http = new System.Net.Http.HttpClient(handler) { Timeout = TimeSpan.FromSeconds(60) };
        var response = await http.GetAsync(url);
        response.EnsureSuccessStatusCode();
        var lista = await response.Content.ReadFromJsonAsync<List<System.Text.Json.JsonElement>>();
        return (lista ?? new List<System.Text.Json.JsonElement>())
            .Select(e => e.TryGetProperty("nombreAgente", out var p) ? p.GetString() ?? "" : "")
            .Where(s => !string.IsNullOrEmpty(s))
            .OrderBy(s => s)
            .ToList();
    }
}
