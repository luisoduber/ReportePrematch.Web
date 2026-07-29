using ReportePrematch.Web.Models;
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
public class ReportesServiceOld : IReportesService
{
    private readonly IApiClient _api;
    private readonly IConfiguration _config;
    private readonly ILogger<ReportesServiceOld> _logger;

    private readonly string _wsReportesUrl;
    private readonly string _brXmlUrl;
    private readonly string _licenciasUrl;
    private readonly string _clavePropia;

    public ReportesServiceOld(IApiClient api, IConfiguration config, ILogger<ReportesServiceOld> logger)
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
            "USUARIO REGULAR" => $"SELECT DISTINCT UPPER(nombreagente) FROM Agentes WHERE nombreagente = '{S(agente)}'",
            "USUARIO ADMINISTRADOR" => $"SELECT DISTINCT UPPER(nombreagente) FROM Agentes WHERE Paisdependencia = '{S(pais)}'",
            _ => "SELECT DISTINCT UPPER(nombreagente) FROM Agentes"  // suAdmin / default
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
            var escapedSql = System.Security.SecurityElement.Escape(sql);

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

            using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(120) };
            client.DefaultRequestHeaders.Add("SOAPAction", "\"http://tempuri.org/ejecutarQuerys\"");

            var content = new StringContent(envelope, System.Text.Encoding.UTF8, "text/xml");
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
        var desde = $"{fechaD} 00:00:00";
        var hasta = $"{fechaH} 23:59:59";
        var esAdmin = role.ToUpper() == "USUARIO SUADMIN";

        var filtro = esAdmin
            ? ""
            : !string.IsNullOrEmpty(agente)
                ? $"AND a.nombreagente = '{S(agente)}' "
                : $"AND a.nombreagente IN ({SubqAgentes(agente, pais, role)}) ";

        var sql = $@"SELECT UPPER(LTRIM(RTRIM(a.nombreagente))) AS nombreagente,
       UPPER(LTRIM(RTRIM(b.deporte))) AS Deporte,
       COUNT(DISTINCT a.ticket) AS cantApuestas,
       SUM(a.montototal) AS monto
FROM apuestas a, apuestaequipo b
WHERE a.ticket = b.ticket
  AND a.tipoap <> 6
  AND a.fecha BETWEEN '{desde}' AND '{hasta}'
  {filtro}
GROUP BY a.nombreagente, b.deporte
ORDER BY 3 DESC";

        return await EjecutarQuerysAsync(sql, 2);
    }

    public async Task<object> GetDeportesMasVendidosPorDeporteAsync(
        string fechaD, string fechaH, string agente, string pais, string role)
    {
        var desde = $"{fechaD} 00:00:00";
        var hasta = $"{fechaH} 23:59:59";
        var esAdmin = role.ToUpper() == "USUARIO SUADMIN";

        var filtro = esAdmin
            ? ""
            : !string.IsNullOrEmpty(agente)
                ? $"AND a.nombreagente = '{S(agente)}' "
                : $"AND a.nombreagente IN ({SubqAgentes(agente, pais, role)}) ";

        var sql = $@"SELECT UPPER(LTRIM(RTRIM(b.deporte))) AS Deporte,
       COUNT(DISTINCT a.ticket) AS cantApuestas,
       SUM(a.montototal) AS monto
FROM apuestas a, apuestaequipo b
WHERE a.ticket = b.ticket
  AND a.tipoap <> 6
  AND a.fecha BETWEEN '{desde}' AND '{hasta}'
  {filtro}
GROUP BY b.deporte
ORDER BY 3 DESC";

        return await EjecutarQuerysAsync(sql, 2);
    }

    /* ══════════════════════════════════════════════════════════
       TICKETS DETALLADOS
    ══════════════════════════════════════════════════════════ */

    public async Task<object> GetTicketsDetalladosWebAsync(
        string fechaD, string fechaH, string agente, string pais, string role)
    {
        var desde = $"{fechaD} 00:00:00";
        var hasta = $"{fechaH} 23:59:59";
        var esAdmin = role.ToUpper() == "USUARIO SUADMIN";

        var filtro = esAdmin
            ? ""
            : !string.IsNullOrEmpty(agente)
                ? $"AND a.AgenteCliente = '{S(agente)}' "
                : $"AND a.nombreagente IN ({SubqAgentes(agente, pais, role)}) ";

        var sql = $@"SELECT a.nombreagente, c.id, c.usuario, a.agencia, a.ticket, a.tipoAp,
       a.tiposaldo, a.montoTotal, a.arriesgando, a.ganando,
       a.operacion, a.fecha, a.fechaCierre
FROM apuestas a, clientes c
WHERE a.idcliente = c.id
  AND a.idCliente != 0
  AND a.fecha BETWEEN '{desde}' AND '{hasta}'
  {filtro}
ORDER BY 1,2,3";

        return await EjecutarQuerysAsync(sql, 2);
    }

    public async Task<object> GetTicketsDetalladosTaqAsync(
        string fechaD, string fechaH, string agente, string pais, string role)
    {
        var desde = $"{fechaD} 00:00:00";
        var hasta = $"{fechaH} 23:59:59";
        var esAdmin = role.ToUpper() == "USUARIO SUADMIN";

        var filtro = esAdmin
            ? ""
            : !string.IsNullOrEmpty(agente)
                ? $"AND AgenteCliente = '{S(agente)}' "
                : $"AND nombreagente IN ({SubqAgentes(agente, pais, role)}) ";

        var sql = $@"SELECT nombreagente, usuario, agencia, ticket, tipoAp, tiposaldo,
       montoTotal, arriesgando, ganando, operacion, fecha, fechaCierre
FROM apuestas
WHERE idCliente = 0
  AND fecha BETWEEN '{desde}' AND '{hasta}'
  {filtro}
ORDER BY 1,2,3";

        return await EjecutarQuerysAsync(sql, 2);
    }

    /* ══════════════════════════════════════════════════════════
       VENTAS POR PAÍS
    ══════════════════════════════════════════════════════════ */

    public async Task<object> GetVentasPorPaisWebAsync(string fechaD, string fechaH, string pais)
    {
        var desde = $"{fechaD} 00:00:00";
        var hasta = $"{fechaH} 23:59:59";
        var filtroPais = string.IsNullOrEmpty(pais) ? "" : $"AND a.paisdependencia = '{S(pais)}' ";

        var sql = $@"SELECT UPPER(LTRIM(RTRIM(a.paisdependencia))) AS paisdependencia,
       UPPER(LTRIM(RTRIM(a.nombreagente))) AS nombreagente,
       a.Agencia, a.montoTotal, a.ganando, a.tipoAp, a.tipoSaldo,
       a.operacion, a.fecha, a.fechapagado, a.fechacierre, a.ticket,
       IIF(a.operacion=3 AND a.tipoap=1 AND a.tipoSaldo=0,
           (SELECT SUM(x.arriesgando+x.monto) FROM apuestaEquipo x WHERE a.ticket=x.ticket AND gano=1), 0) AS preDir,
       IIF(a.operacion=3 AND a.tipoap=1 AND a.tipoSaldo=1,
           (SELECT SUM(x.monto) FROM apuestaEquipo x WHERE a.ticket=x.ticket AND gano=1), 0) AS preDirBono
FROM Apuestas a
WHERE a.usuario = '0'
  AND (a.fecha BETWEEN '{desde}' AND '{hasta}'
       OR a.fechacierre BETWEEN '{desde}' AND '{hasta}'
       OR a.fechapagado BETWEEN '{desde}' AND '{hasta}')
  {filtroPais}
ORDER BY 1,2";

        return await EjecutarQuerysAsync(sql, 2);
    }

    public async Task<object> GetVentasPorPaisTaqAsync(string fechaD, string fechaH, string pais)
    {
        var desde = $"{fechaD} 00:00:00";
        var hasta = $"{fechaH} 23:59:59";
        var filtroPais = string.IsNullOrEmpty(pais) ? "" : $"AND a.paisdependencia = '{S(pais)}' ";

        var sql = $@"SELECT UPPER(LTRIM(RTRIM(a.paisdependencia))) AS paisdependencia,
       UPPER(LTRIM(RTRIM(a.nombreagente))) AS nombreagente,
       a.Agencia, a.montoTotal, a.ganando, a.tipoAp, a.tipoSaldo,
       a.operacion, a.fecha, a.fechapagado, a.fechacierre, a.ticket,
       IIF((a.operacion=3 OR a.operacion=1) AND a.tipoap=1 AND a.tipoSaldo=0,
           (SELECT SUM(x.arriesgando+x.monto) FROM apuestaEquipo x WHERE a.ticket=x.ticket AND gano=1), 0) AS preDir
FROM Apuestas a
WHERE a.usuario <> '0'
  AND (a.fecha BETWEEN '{desde}' AND '{hasta}'
       OR a.fechacierre BETWEEN '{desde}' AND '{hasta}'
       OR a.fechapagado BETWEEN '{desde}' AND '{hasta}')
  {filtroPais}
ORDER BY 1,2";

        return await EjecutarQuerysAsync(sql, 2);
    }

    /* ══════════════════════════════════════════════════════════
       RETIROS Y RECARGAS (db=3)
    ══════════════════════════════════════════════════════════ */

    public async Task<object> GetRetirosRecargasAsync(
        string fechaD, string fechaH, string agente, string pais, string role)
    {
        var desde = $"{fechaD} 00:00:00";
        var hasta = $"{fechaH} 23:59:59";
        var esAdmin = role.ToUpper() == "USUARIO SUADMIN";

        var filtro = esAdmin
            ? ""
            : !string.IsNullOrEmpty(agente)
                ? $"AND a.subagente = '{S(agente)}' "
                : $"AND a.subagente IN ({SubqAgentes(agente, pais, role)}) ";

        var sql = $@"SELECT a.nombreuser, c.monto, c.accion, a.subagente, c.hora AS fecha
FROM Usuarios AS a
INNER JOIN Transacciones AS c ON c.idUsuario = a.iduser
WHERE c.fecha BETWEEN '{desde}' AND '{hasta}'
{filtro}
ORDER BY 4,1";

        return await EjecutarQuerysAsync(sql, 3);
    }

    /* ══════════════════════════════════════════════════════════
       SALDOS DISPONIBLES
    ══════════════════════════════════════════════════════════ */

    public async Task<object> GetSaldosDisponiblesAsync(string agente)
    {
        var filtro = string.IsNullOrEmpty(agente)
            ? ""
            : $"AND c.nombreagente = '{S(agente)}'";

        var sql = $@"SELECT c.nombreagente, b.usuario, a.disponible
FROM clientesaldos a, clientes b, clientesid c
WHERE a.idcliente = b.id
  AND a.idcliente = c.idcliente
  AND a.disponible > 0
  {filtro}
ORDER BY 1,2";

        return await EjecutarQuerysAsync(sql, 2);
    }

    /* ══════════════════════════════════════════════════════════
       VENTAS POR GAME ID
    ══════════════════════════════════════════════════════════ */

    public async Task<object> GetVentasAgenteGameIDAsync(
        string agente, string pais, string role, string gameId, string filtApuesta)
    {
        var esAdmin = role.ToUpper() == "USUARIO SUADMIN";

        var opcionApuesta = filtApuesta switch
        {
            "Parlay" => "AND ap.tipoAp=1 ",
            "Directo" => "AND ap.tipoAp=4 ",
            _ => ""
        };

        var filtro = esAdmin
            ? ""
            : !string.IsNullOrEmpty(agente)
                ? $"AND ap.NombreAgente = '{S(agente)}' "
                : $"AND ap.NombreAgente IN ({SubqAgentes(agente, pais, role)}) ";

        var sql = $@"SELECT TOP 10000
    ap.ticket, ae.gameid, ae.arriesgando, ae.CategoriaAp, ap.tipoap, ae.equipo,
    ae.FechaJuego, ap.operacion, ae.gano, ap.fecha AS fechaTicket, ap.NombreAgente,
    (SELECT Usuario FROM Clientes WHERE id = ap.idcliente) AS Usuario,
    ae.monto, ae.deporte, ae.logro, ap.montoTotal, ap.ganando
FROM Apuestas AS ap
INNER JOIN ApuestaEquipo AS ae ON CONVERT(varchar, ap.ticket) = ae.ticket
WHERE ae.gameid = {S(gameId)}
  AND ap.idtaquilla = 0
  {opcionApuesta}
  {filtro}";

        return await EjecutarQuerysAsync(sql, 2);
    }

    /* ══════════════════════════════════════════════════════════
       CASINO MAQUINITAS (db=3)
    ══════════════════════════════════════════════════════════ */

    public async Task<object> GetCasinoMaquinitasAsync(
        string fechaD, string fechaH, string agente, string pais, string role)
    {
        var desde = $"{fechaD} 00:00:00";
        var hasta = $"{fechaH} 23:59:59";
        var esAdmin = role.ToUpper() == "USUARIO SUADMIN";

        var filtro = esAdmin
            ? ""
            : !string.IsNullOrEmpty(agente)
                ? $"AND a.NombreAgente = '{S(agente)}' "
                : $"AND a.NombreAgente IN ({SubqAgentes(agente, pais, role)}) ";

        var sql = $@"SELECT a.NombreAgente, b.[User],
  (SELECT SUM(c.amount) FROM [Dotsprin].[dbo].[Transactions] c
   WHERE c.playerid = b.playerid AND c.Amount IS NOT NULL
     AND c.Action = 'DEBIT' AND c.[Date] BETWEEN '{desde}' AND '{hasta}') AS ventas,
  (SELECT SUM(d.amount) FROM [Dotsprin].[dbo].[Transactions] d
   WHERE d.playerid = b.playerid AND d.Amount IS NOT NULL
     AND d.Action = 'CREDIT' AND d.[Date] BETWEEN '{desde}' AND '{hasta}') AS premios
FROM ClientesID a, [Dotsprin].[dbo].[Transactions] b
WHERE b.playerId = a.idCliente
  AND b.Amount IS NOT NULL
  {filtro}
GROUP BY b.playerid, a.NombreAgente, b.[User]
ORDER BY 1,2";

        return await EjecutarQuerysAsync(sql, 3);
    }

    /* ══════════════════════════════════════════════════════════
       CASINO SLOT GRECIA (db=3)
    ══════════════════════════════════════════════════════════ */

    public async Task<object> GetCasinoSlotGreciaAsync(
        string fechaD, string fechaH, string agente, string pais, string role)
    {
        var desde = $"{fechaD} 00:00:00";
        var hasta = $"{fechaH} 23:59:59";
        var esAdmin = role.ToUpper() == "USUARIO SUADMIN";

        var filtro = esAdmin
            ? ""
            : !string.IsNullOrEmpty(agente)
                ? $"AND c.Agente = '{S(agente)}' "
                : $"AND c.Agente IN ({SubqAgentes(agente, pais, role)}) ";

        var sql = $@"SELECT c.Agente AS NombreAgente, c.Usuario,
       SUM(c.Entrada) AS ventas, SUM(c.Salida) AS premios
FROM [SlotGrecia].dbo.UserBet c
WHERE c.Fecha BETWEEN '{desde}' AND '{hasta}'
{filtro}
GROUP BY c.Agente, c.Usuario
ORDER BY c.Agente, c.Usuario";

        return await EjecutarQuerysAsync(sql, 3);
    }

    /* ══════════════════════════════════════════════════════════
       ICON SLOT (db=3)
    ══════════════════════════════════════════════════════════ */

    public async Task<object> GetIconSlotAsync(
        string fechaD, string fechaH, string agente, string pais, string role)
    {
        var desde = $"{fechaD} 00:00:00";
        var hasta = $"{fechaH} 23:59:59";
        var esAdmin = role.ToUpper() == "USUARIO SUADMIN";

        var filtro = esAdmin
            ? ""
            : !string.IsNullOrEmpty(agente)
                ? $"AND t.Agent = '{S(agente)}' "
                : $"AND t.Agent IN ({SubqAgentes(agente, pais, role)}) ";

        var sql = $@"SELECT t.Agent AS NombreAgente, t.UserName AS Usuario,
       SUM(CASE WHEN t.Type = 'Debit'  THEN t.Amount ELSE 0 END) AS ventas,
       SUM(CASE WHEN t.Type = 'Credit' THEN t.Amount ELSE 0 END) AS premios,
       SUM(CASE WHEN t.Type = 'Debit'  THEN t.Amount ELSE 0 END) -
       SUM(CASE WHEN t.Type = 'Credit' THEN t.Amount ELSE 0 END) AS Utilidad
FROM [IconBetSlotsGCIT].dbo.[Transaction] t
WHERE t.Amount IS NOT NULL
  AND t.Date BETWEEN '{desde}' AND '{hasta}'
  {filtro}
GROUP BY t.Agent, t.UserName
ORDER BY t.Agent, t.UserName";

        return await EjecutarQuerysAsync(sql, 3);
    }

    /* ══════════════════════════════════════════════════════════
       CASINO SLOT RUSSIA (db=3)
    ══════════════════════════════════════════════════════════ */

    public async Task<object> GetCasinoSlotRussiaAsync(
        string fechaD, string fechaH, string agente, string pais, string role)
    {
        var desde = $"{fechaD} 00:00:00";
        var hasta = $"{fechaH} 23:59:59";
        var esAdmin = role.ToUpper() == "USUARIO SUADMIN";

        var filtro = esAdmin
            ? ""
            : !string.IsNullOrEmpty(agente)
                ? $"AND ci.NombreAgente = '{S(agente)}' "
                : $"AND ci.NombreAgente IN ({SubqAgentes(agente, pais, role)}) ";

        var sql = $@"SELECT ci.NombreAgente, c.Usuario,
       SUM(c.bet) AS ventas, SUM(c.win) AS premios
FROM [VirtualSlots].dbo.Statistic c
INNER JOIN [GCITBR].[dbo].[ClientesID] ci ON ci.idCliente = c.id_customer
WHERE c.date BETWEEN '{desde}' AND '{hasta}'
{filtro}
GROUP BY ci.NombreAgente, c.Usuario
ORDER BY ci.NombreAgente, c.Usuario";

        return await EjecutarQuerysAsync(sql, 3);
    }

    /* ══════════════════════════════════════════════════════════
       PROPS (db=3)
    ══════════════════════════════════════════════════════════ */

    public async Task<object> GetPropsAsync(
        string fechaD, string fechaH, string agente, string pais, string role)
    {
        var desde = $"{fechaD} 00:00:00";
        var hasta = $"{fechaH} 23:59:59";
        var esAdmin = role.ToUpper() == "USUARIO SUADMIN";

        var filtro = esAdmin
            ? ""
            : !string.IsNullOrEmpty(agente)
                ? $"AND a.Agente = '{S(agente)}' "
                : $"AND a.Agente IN ({SubqAgentes(agente, pais, role)}) ";

        var sql = $@"SELECT a.Agente AS NombreAgente, a.Username AS Usuario,
  (SELECT SUM(b.amount) FROM [Props].[dbo].[Transaction] AS b
   WHERE b.Username=a.Username AND b.Type='debit' AND b.amount IS NOT NULL
     AND b.Fecha BETWEEN '{desde}' AND '{hasta}') AS ventas,
  (SELECT SUM(b.amount) FROM [Props].[dbo].[Transaction] AS b
   WHERE b.Username=a.Username AND b.Type='credit' AND b.amount IS NOT NULL
     AND b.Fecha BETWEEN '{desde}' AND '{hasta}') AS premios,
  (SELECT SUM(CONVERT(money,b.amount)) FROM [Props].[dbo].[LoseUndo] AS b
   WHERE b.Username=a.Username AND b.Type='void'
     AND b.Fecha BETWEEN '{desde}' AND '{hasta}') AS retorno
FROM [Props].[dbo].[Transaction] AS a
WHERE a.amount IS NOT NULL
  AND a.Fecha BETWEEN '{desde}' AND '{hasta}'
{filtro}
GROUP BY a.Agente, a.Username
ORDER BY a.Agente, a.Username";

        return await EjecutarQuerysAsync(sql, 3);
    }

    /* ══════════════════════════════════════════════════════════
       POKER (db=2)
    ══════════════════════════════════════════════════════════ */

    public async Task<object> GetPokerAsync(
        string fechaD, string fechaH, string agente, string pais, string role)
    {
        var desde = $"{fechaD} 00:00:00";
        var hasta = $"{fechaH} 23:59:59";
        var esAdmin = role.ToUpper() == "USUARIO SUADMIN";

        var filtro = esAdmin
            ? ""
            : !string.IsNullOrEmpty(agente)
                ? $"AND a.Agente = '{S(agente)}' "
                : $"AND a.Agente IN ({SubqAgentes(agente, pais, role)}) ";

        var sql = $@"SELECT a.Agente AS NombreAgente, a.Usuario,
  (SELECT SUM(b.Monto) FROM [PokerDB].[dbo].[Transacciones] AS b
   WHERE b.Usuario=a.Usuario AND b.Deposit=0 AND [Status]=1
     AND b.Fecha BETWEEN '{desde}' AND '{hasta}') AS ventas,
  (SELECT SUM(b.Monto) FROM [PokerDB].[dbo].[Transacciones] AS b
   WHERE b.Usuario=a.Usuario AND b.Deposit=1 AND [Status]=1
     AND b.Fecha BETWEEN '{desde}' AND '{hasta}') AS premios
FROM [PokerDB].[dbo].[Transacciones] AS a
WHERE a.Fecha BETWEEN '{desde}' AND '{hasta}'
{filtro}
GROUP BY a.Agente, a.Usuario
ORDER BY a.Agente, a.Usuario";

        return await EjecutarQuerysAsync(sql, 2);
    }

    /* ══════════════════════════════════════════════════════════
       PRAGMATIC (db=3)
    ══════════════════════════════════════════════════════════ */

    public async Task<object> GetPragmaticAsync(
        string fechaD, string fechaH, string agente, string pais, string role)
    {
        var desde = $"{fechaD} 00:00:00";
        var hasta = $"{fechaH} 23:59:59";
        var esAdmin = role.ToUpper() == "USUARIO SUADMIN";

        var filtro = esAdmin
            ? ""
            : !string.IsNullOrEmpty(agente)
                ? $"AND a.Agent = '{S(agente)}' "
                : $"AND a.Agent IN ({SubqAgentes(agente, pais, role)}) ";

        var sql = $@"SELECT a.Agent AS NombreAgente, a.UserId AS Usuario,
  (SELECT SUM(b.Amount) FROM [Pragmatic].[dbo].[Transaction] AS b
   WHERE b.UserId=a.UserId AND b.[Type]='Debit'
     AND b.[Date] BETWEEN '{desde}' AND '{hasta}') AS ventas,
  (SELECT SUM(b.Amount) FROM [Pragmatic].[dbo].[Transaction] AS b
   WHERE b.UserId=a.UserId
     AND (b.[Type]='Credit' OR b.[Type]='Refund' OR b.[Type]='Bonus')
     AND b.[Date] BETWEEN '{desde}' AND '{hasta}') AS premios
FROM [Pragmatic].[dbo].[Transaction] AS a
WHERE a.[Date] BETWEEN '{desde}' AND '{hasta}'
{filtro}
GROUP BY a.Agent, a.UserId
ORDER BY a.Agent, a.UserId";

        return await EjecutarQuerysAsync(sql, 3);
    }

    /* ══════════════════════════════════════════════════════════
       VIRTUALES BR (db=3)
    ══════════════════════════════════════════════════════════ */

    public async Task<object> GetVirtualesBRAsync(
        string fechaD, string fechaH, string agente, string pais, string role)
    {
        var desde = $"{fechaD} 00:00:00";
        var hasta = $"{fechaH} 23:59:59";
        var esAdmin = role.ToUpper() == "USUARIO SUADMIN";

        var filtro = esAdmin
            ? ""
            : !string.IsNullOrEmpty(agente)
                ? $"AND a.Agente = '{S(agente)}' "
                : $"AND a.Agente IN ({SubqAgentes(agente, pais, role)}) ";

        var sql = $@"SELECT a.Agente AS NombreAgente, a.Usuario,
  (SELECT SUM(b.Monto) FROM [BetradarWallet].[dbo].[Transacciones] AS b
   WHERE b.Usuario=a.Usuario AND b.Accion='approve'
     AND b.Fecha BETWEEN '{desde}' AND '{hasta}') AS ventas,
  (SELECT SUM(b.Monto) FROM [BetradarWallet].[dbo].[Transacciones] AS b
   WHERE b.Usuario=a.Usuario AND b.Accion='payment'
     AND b.Fecha BETWEEN '{desde}' AND '{hasta}') AS premios,
  (SELECT SUM(b.Monto) FROM [BetradarWallet].[dbo].[Transacciones] AS b
   WHERE b.Usuario=a.Usuario AND b.Accion='reserveFunds'
     AND b.Fecha BETWEEN '{desde}' AND '{hasta}') AS retorno
FROM [BetradarWallet].[dbo].[Transacciones] AS a
WHERE a.Fecha BETWEEN '{desde}' AND '{hasta}'
  AND a.Usuario != '' AND a.Agente != ''
{filtro}
GROUP BY a.Agente, a.Usuario
ORDER BY a.Agente, a.Usuario";

        return await EjecutarQuerysAsync(sql, 3);
    }

    /* ══════════════════════════════════════════════════════════
       VIRTUALES CARIBE (db=3)
    ══════════════════════════════════════════════════════════ */

    public async Task<object> GetVirtualesCarAsync(
        string fechaD, string fechaH, string agente, string pais, string role)
    {
        var desde = $"{fechaD} 00:00:00";
        var hasta = $"{fechaH} 23:59:59";
        var esAdmin = role.ToUpper() == "USUARIO SUADMIN";

        var filtro = esAdmin
            ? ""
            : !string.IsNullOrEmpty(agente)
                ? $"AND a.Agent = '{S(agente)}' "
                : $"AND a.Agent IN ({SubqAgentes(agente, pais, role)}) ";

        var sql = $@"SELECT a.Agent AS NombreAgente, a.UserName AS Usuario,
  (SELECT SUM(b.Amount) FROM [GlobalSportRoostersGCIT].[dbo].[Transaction] AS b
   WHERE b.UserName=a.UserName AND b.[Type]='InsertBet'
     AND b.[Date] BETWEEN '{desde}' AND '{hasta}') AS ventas,
  (SELECT SUM(b.Amount) FROM [GlobalSportRoostersGCIT].[dbo].[Transaction] AS b
   WHERE b.UserName=a.UserName AND b.[Type]='BetResult'
     AND b.[Date] BETWEEN '{desde}' AND '{hasta}') AS premios
FROM [GlobalSportRoostersGCIT].[dbo].[Transaction] AS a
WHERE a.[Date] BETWEEN '{desde}' AND '{hasta}'
{filtro}
GROUP BY a.Agent, a.UserName
ORDER BY a.Agent, a.UserName";

        return await EjecutarQuerysAsync(sql, 3);
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
        var filtroAgente = string.IsNullOrEmpty(agente) ? "" : $"AND pt.Agente='{S(agente)}'";

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
        var hasta = string.IsNullOrEmpty(fechaH) ? "CONVERT(varchar, GETDATE(), 111) + ' 23:59:59'" : $"'{fechaH} 23:59:59'";
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
        var hasta = string.IsNullOrEmpty(fechaH) ? "CONVERT(varchar, GETDATE(), 111) + ' 23:59:59'" : $"'{fechaH} 23:59:59'";
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
       VENTAS DETALLADAS LIVE
    ══════════════════════════════════════════════════════════ */

    public async Task<object> GetVentasDetalladasLiveAsync(
        string fechaD, string fechaH, string agente, string pais, string role)
    {
        var desde = $"{fechaD} 00:00:00";
        var hasta = $"{fechaH} 23:59:59";
        var esAdmin = role.ToUpper() == "USUARIO SUADMIN";

        var filtro = esAdmin
            ? ""
            : !string.IsNullOrEmpty(agente)
                ? $"AND li.agenteid = '{S(agente)}' "
                : $"AND li.agenteid IN ({SubqAgentes(agente, pais, role)}) ";

        var sql = $@"SELECT UPPER(LTRIM(RTRIM(li.agenteid))) AS nombreagente,
       UPPER(LTRIM(RTRIM(li.Usuario))) AS Usuario,
       SUM(li.monto) AS riesgo,
       IIF(li.cerrado = 1, SUM(li.montoaganar), 0) AS ganando,
       li.cerrado
FROM Tickets li
WHERE li.fecha BETWEEN '{desde}' AND '{hasta}'
{filtro}
GROUP BY li.agenteid, li.Usuario, li.cerrado
ORDER BY 1,2";

        return await EjecutarQuerysAsync(sql, 2);
    }

    /* ══════════════════════════════════════════════════════════
       TICKETS EN JUEGO
    ══════════════════════════════════════════════════════════ */

    public async Task<object> GetTicketsEnJuegoWebAsync(string fechaD, string fechaH, string agente)
    {
        var desde = $"{fechaD} 00:00:00";
        var hasta = $"{fechaH} 23:59:59";
        var filtro = string.IsNullOrEmpty(agente) ? "" : $"AND a.NombreAgente = '{S(agente)}'";

        var sql = $@"SELECT a.nombreagente, b.usuario, a.tipoap,
       a.ganando, a.montoTotal, (a.ganando + a.montoTotal) AS posible_premio
FROM apuestas a, clientes b
WHERE a.idCliente = b.id
  AND a.tipoSaldo = 0
  AND a.operacion = 10
  AND a.fecha BETWEEN '{desde}' AND '{hasta}'
  {filtro}
ORDER BY 1,2";

        return await EjecutarQuerysAsync(sql, 2);
    }

    public async Task<object> GetTicketsEnJuegoTaqAsync(string fechaD, string fechaH, string agente)
    {
        var desde = $"{fechaD} 00:00:00";
        var hasta = $"{fechaH} 23:59:59";
        var filtro = string.IsNullOrEmpty(agente) ? "" : $"AND NombreAgente = '{S(agente)}'";

        var sql = $@"SELECT nombreagente, usuario, tipoap,
       ganando, montoTotal, (ganando + montoTotal) AS posible_premio
FROM apuestas
WHERE operacion = 10
  AND usuario != '0'
  AND fecha BETWEEN '{desde}' AND '{hasta}'
  {filtro}
ORDER BY 1,2";

        return await EjecutarQuerysAsync(sql, 2);
    }

    /* ══════════════════════════════════════════════════════════
       VENTAS POR AGENTE NIKOLS
    ══════════════════════════════════════════════════════════ */

    public async Task<object> GetVentasPorAgenteNikolsWebAsync(
        string fechaD, string fechaH, string agente, string pais, string role)
    {
        var desde = $"{fechaD} 00:00:00";
        var hasta = $"{fechaH} 23:59:59";
        var esAdmin = role.ToUpper() == "USUARIO SUADMIN";

        var filtro = esAdmin
            ? ""
            : !string.IsNullOrEmpty(agente)
                ? $"AND a.nombreagente = '{S(agente)}' "
                : $"AND a.nombreagente IN ({SubqAgentes(agente, pais, role)}) ";

        var sql = $@"SELECT UPPER(LTRIM(RTRIM(a.nombreagente))) AS nombreagente,
       UPPER(LTRIM(RTRIM(b.usuario))) AS usuario,
       a.Agencia, a.montoTotal, a.ganando, a.tipoAp, a.tipoSaldo,
       a.operacion, a.fecha, a.fechapagado, a.fechacierre, a.ticket,
       IIF(a.operacion=3 AND a.tipoap=1 AND a.tipoSaldo=0,
           (SELECT SUM(x.arriesgando+x.monto) FROM apuestaEquipo x WHERE a.ticket=x.ticket AND gano=1), 0) AS preDir,
       IIF(a.operacion=3 AND a.tipoap=1 AND a.tipoSaldo=1,
           (SELECT SUM(x.monto) FROM apuestaEquipo x WHERE a.ticket=x.ticket AND gano=1), 0) AS preDirBono
FROM Apuestas a, Clientes b
WHERE a.idCliente = b.id
  AND (a.fecha BETWEEN '{desde}' AND '{hasta}'
       OR a.fechacierre BETWEEN '{desde}' AND '{hasta}'
       OR a.fechapagado BETWEEN '{desde}' AND '{hasta}')
  {filtro}
ORDER BY 1,2";

        return await EjecutarQuerysAsync(sql, 2);
    }

    public async Task<object> GetVentasPorAgenteNikolsTaqAsync(
        string fechaD, string fechaH, string agente, string local, string pais, string role)
    {
        var desde = $"{fechaD} 00:00:00";
        var hasta = $"{fechaH} 23:59:59";
        var esAdmin = role.ToUpper() == "USUARIO SUADMIN";
        var filtroLocal = (!string.IsNullOrEmpty(local) && local != "0") ? $"AND a.agencia = '{S(local)}' " : "";

        var filtro = esAdmin
            ? ""
            : !string.IsNullOrEmpty(agente)
                ? $"AND a.nombreagente = '{S(agente)}' "
                : $"AND a.nombreagente IN ({SubqAgentes(agente, pais, role)}) ";

        var sql = $@"SELECT UPPER(LTRIM(RTRIM(a.nombreagente))) AS nombreagente,
       UPPER(LTRIM(RTRIM(a.usuario))) AS usuario,
       a.Agencia, a.montoTotal, a.ganando, a.tipoAp, a.tipoSaldo,
       a.operacion, a.fecha, a.fechapagado, a.fechacierre, a.ticket,
       IIF(a.operacion=3 AND a.tipoap=1 AND a.tipoSaldo=0,
           (SELECT SUM(x.arriesgando+x.monto) FROM apuestaEquipo x WHERE a.ticket=x.ticket AND gano=1), 0) AS preDir
FROM Apuestas a
WHERE a.idCliente = 0
  {filtroLocal}
  AND (a.fecha BETWEEN '{desde}' AND '{hasta}'
       OR (a.fechapagado BETWEEN '{desde}' AND '{hasta}' AND a.pagado=1))
  {filtro}
ORDER BY 1,2";

        return await EjecutarQuerysAsync(sql, 2);
    }

    /* ══════════════════════════════════════════════════════════
       ESTADÍSTICAS
    ══════════════════════════════════════════════════════════ */

    public async Task<object> GetEstadisticasWebAsync(
        string fechaD, string fechaH, string agente, string pais, string role)
    {
        var desde = $"{fechaD} 00:00:00";
        var hasta = $"{fechaH} 23:59:59";
        var esAdmin = role.ToUpper() == "USUARIO SUADMIN";

        var filtro = esAdmin
            ? ""
            : !string.IsNullOrEmpty(agente)
                ? $"AND a.nombreagente = '{S(agente)}' "
                : $"AND a.nombreagente IN ({SubqAgentes(agente, pais, role)}) ";

        var sql = $@"SELECT DISTINCT UPPER(LTRIM(RTRIM(a.nombreagente))) AS nombreagente,
       UPPER(LTRIM(RTRIM(c.deporte))) AS Deporte,
       COUNT(a.ticket) AS cnt,
       (SELECT COUNT(x.ticket) FROM ApuestaEquipo x WHERE x.ticket = a.ticket) AS sec_cnt,
       a.ticket, a.Agencia, a.montoTotal, a.ganando, a.tipoAp, a.tipoSaldo,
       a.operacion, a.fecha, a.fechapagado, a.fechacierre,
       IIF(a.operacion=3 AND a.tipoap=1 AND a.tipoSaldo=0,
           (SELECT SUM(x.arriesgando+x.monto) FROM apuestaEquipo x WHERE a.ticket=x.ticket AND gano=1), 0) AS preDir,
       IIF(a.operacion=3 AND a.tipoap=1 AND a.tipoSaldo=1,
           (SELECT SUM(x.monto) FROM apuestaEquipo x WHERE a.ticket=x.ticket AND gano=1), 0) AS preDirBono
FROM Apuestas a, ApuestaEquipo c
WHERE a.ticket = c.ticket
  AND a.Agencia = 'web'
  AND (a.fecha BETWEEN '{desde}' AND '{hasta}'
       OR a.fechacierre BETWEEN '{desde}' AND '{hasta}'
       OR a.fechapagado BETWEEN '{desde}' AND '{hasta}')
  {filtro}
GROUP BY a.NombreAgente, c.deporte, a.ticket, a.Agencia, a.montoTotal, a.ganando,
         a.tipoAp, a.tipoSaldo, a.operacion, a.fecha, a.fechapagado, a.fechacierre
ORDER BY 1,2";

        return await EjecutarQuerysAsync(sql, 2);
    }

    public async Task<object> GetEstadisticasTaqAsync(
        string fechaD, string fechaH, string agente, string local, string pais, string role)
    {
        var desde = $"{fechaD} 00:00:00";
        var hasta = $"{fechaH} 23:59:59";
        var esAdmin = role.ToUpper() == "USUARIO SUADMIN";
        var filtroLocal = (!string.IsNullOrEmpty(local) && local != "0") ? $"AND a.agencia = '{S(local)}' " : "";

        var filtro = esAdmin
            ? ""
            : !string.IsNullOrEmpty(agente)
                ? $"AND a.nombreagente = '{S(agente)}' "
                : $"AND a.nombreagente IN ({SubqAgentes(agente, pais, role)}) ";

        var sql = $@"SELECT DISTINCT UPPER(LTRIM(RTRIM(a.nombreagente))) AS nombreagente,
       UPPER(LTRIM(RTRIM(c.deporte))) AS Deporte,
       COUNT(a.ticket) AS cnt,
       (SELECT COUNT(x.ticket) FROM ApuestaEquipo x WHERE x.ticket = a.ticket) AS sec_cnt,
       a.ticket, a.Agencia, a.montoTotal, a.ganando, a.tipoAp, a.tipoSaldo,
       a.operacion, a.fecha, a.fechapagado, a.fechacierre,
       IIF(a.operacion=3 AND a.tipoap=1 AND a.tipoSaldo=0,
           (SELECT SUM(x.arriesgando+x.monto) FROM apuestaEquipo x WHERE a.ticket=x.ticket AND gano=1), 0) AS preDir
FROM Apuestas a, ApuestaEquipo c
WHERE a.ticket = c.ticket
  AND a.usuario != '0'
  {filtroLocal}
  AND (a.fecha BETWEEN '{desde}' AND '{hasta}'
       OR a.fechacierre BETWEEN '{desde}' AND '{hasta}'
       OR a.fechapagado BETWEEN '{desde}' AND '{hasta}')
  {filtro}
GROUP BY a.NombreAgente, c.deporte, a.ticket, a.Agencia, a.montoTotal, a.ganando,
         a.tipoAp, a.tipoSaldo, a.operacion, a.fecha, a.fechapagado, a.fechacierre
ORDER BY 1,2";

        return await EjecutarQuerysAsync(sql, 2);
    }

    /* ══════════════════════════════════════════════════════════
       HIPISMO
    ══════════════════════════════════════════════════════════ */

    public async Task<object> GetHipismoAsync(
        string fechaD, string fechaH, string agente, string pais, string role)
    {
        var desde = $"{fechaD} 00:00:00";
        var hasta = $"{fechaH} 23:59:59";
        var esAdmin = role.ToUpper() == "USUARIO SUADMIN";

        var filtro = esAdmin
            ? ""
            : !string.IsNullOrEmpty(agente)
                ? $"AND h.Agente = '{S(agente)}' "
                : $"AND h.Agente IN ({SubqAgentes(agente, pais, role)}) ";

        var sql = $@"SELECT h.Agente, c.Usuario,
  (SELECT SUM(th.Monto) FROM TransaccionHipismo AS th
   WHERE th.idCliente=h.idCliente AND th.operacion=2
     AND th.Fecha BETWEEN '{desde}' AND '{hasta}') AS Ventas,
  (SELECT SUM(th.Monto) FROM TransaccionHipismo AS th
   WHERE th.idCliente=h.idCliente AND th.operacion=1
     AND th.Fecha BETWEEN '{desde}' AND '{hasta}') AS Premios
FROM TransaccionHipismo AS h
INNER JOIN Clientes AS c ON h.idCliente = c.id
INNER JOIN ClientesID AS ci ON ci.idCliente = c.id
WHERE h.Fecha BETWEEN '{desde}' AND '{hasta}'
{filtro}
GROUP BY h.Agente, h.idCliente, c.Usuario
ORDER BY h.Agente, c.Usuario";

        return await EjecutarQuerysAsync(sql, 2);
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
            var escapedClave = System.Security.SecurityElement.Escape(_clavePropia);
            var escapedAgente = System.Security.SecurityElement.Escape(agente);
            var escapedAgencia = System.Security.SecurityElement.Escape(agencia);
            var escapedUsuario = System.Security.SecurityElement.Escape(usuario);
            var escapedFecha = System.Security.SecurityElement.Escape(fecha);

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

            var content = new StringContent(envelope, System.Text.Encoding.UTF8, "text/xml");
            var response = await client.PostAsync(_licenciasUrl, content);
            response.EnsureSuccessStatusCode();

            var xml = await response.Content.ReadAsStringAsync();
            var doc = new XmlDocument();
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
            ["fechaD"] = fechaD,
            ["fechaH"] = fechaH,
            ["agente"] = agente
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
            ["fechaD"] = fechaD,
            ["fechaH"] = fechaH,
            ["agente"] = agente
        };
        if (!string.IsNullOrEmpty(local)) query["local"] = local;
        if (!string.IsNullOrEmpty(pais)) query["pais"] = pais;
        if (!string.IsNullOrEmpty(role)) query["role"] = role;

        var result = await _api.GetQueryAsync<List<VentasPorAgenteTaqDto>>(
            "api/Reportes/VentasPorAgenteTaq", query);
        return result ?? new List<VentasPorAgenteTaqDto>();
    }

    public async Task<object> GetRepTransaccionesAsync(
        string fechaD, string fechaH, string agente, string webSite)
    {
        var query = new Dictionary<string, string>
        {
            ["fechaD"] = fechaD,
            ["fechaH"] = fechaH,
            ["agente"] = agente
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
                ["fechaD"] = fechaD,
                ["fechaH"] = fechaH,
                ["usuario"] = usuario
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
            ["fechaD"] = fechaD,
            ["fechaH"] = fechaH,
            ["agente"] = agente
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
        var result = await _api.PostAsync<object>(
            "api/loterias/triple",
            new { fechaD, fechaH, agente, role });
        return result ?? new List<object>();
    }

    public async Task<object> GetAviatrixAsync(
        string fechaD, string fechaH, string agente, string role = "", string pais = "")
    {
        var query = new Dictionary<string, string>
        {
            ["fechaD"] = fechaD,
            ["fechaH"] = fechaH,
            ["agente"] = agente
        };
        if (!string.IsNullOrEmpty(pais)) query["pais"] = pais;
        if (!string.IsNullOrEmpty(role)) query["role"] = role;

        var result = await _api.GetQueryAsync<List<AviatrixDto>>(
            "api/Reportes/Aviatrix", query);
        return result ?? new List<AviatrixDto>();
    }

    public async Task<object> GetFantasyBsbAsync(
        string fechaD, string fechaH, string agente, string role = "", string pais = "")
    {
        var query = new Dictionary<string, string>
        {
            ["fechaD"] = fechaD,
            ["fechaH"] = fechaH,
            ["agente"] = agente
        };
        if (!string.IsNullOrEmpty(pais)) query["pais"] = pais;
        if (!string.IsNullOrEmpty(role)) query["role"] = role;

        var result = await _api.GetQueryAsync<List<FantasyBsbDto>>(
            "api/Reportes/FantasyBsb", query);
        return result ?? new List<FantasyBsbDto>();
    }
}