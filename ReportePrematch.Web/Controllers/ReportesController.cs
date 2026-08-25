using Microsoft.AspNetCore.Mvc;
using ReportePrematch.Web.Services.Interfaces;

namespace ReportePrematch.Web.Controllers;

/// <summary>
/// Controlador de todos los reportes.
/// Patrón uniforme: GET carga la vista (verifica sesión), POST devuelve JSON para DataTables.
/// </summary>
public sealed class ReportesController(
    IReportesService reportes,
    ILogger<ReportesController> logger) : Controller
{
    // ═══════════════════════════════════════════════════════════════
    // HELPERS PRIVADOS
    // ═══════════════════════════════════════════════════════════════

    private bool SesionActiva() => HttpContext.Session.GetString("IDUusuario") != null;

    private IActionResult RedirectToLogin()
    {
        HttpContext.Session.Clear();
        return RedirectToAction("Login", "Home");
    }

    private void CargarViewBag(string nombrePagina, string? btnId = null, string? tableId = null)
    {
        ViewBag.Usuario = HttpContext.Session.GetString("usuario");
        ViewBag.Agente = HttpContext.Session.GetString("agente");
        ViewBag.Pais = HttpContext.Session.GetString("pais");
        ViewBag.Local = HttpContext.Session.GetString("IDLocal");
        ViewBag.TipoUsuario = HttpContext.Session.GetString("tipoUsuario");
        ViewBag.NombrePagina = nombrePagina;
        if (btnId != null) ViewBag.BtnId = btnId;
        if (tableId != null) ViewBag.TableId = tableId;
    }

    private string Agente => HttpContext.Session.GetString("agente") ?? string.Empty;
    private string Pais => HttpContext.Session.GetString("pais") ?? string.Empty;
    private string Local => HttpContext.Session.GetString("IDLocal") ?? "0";
    private string Role => HttpContext.Session.GetString("tipoUsuario") ?? string.Empty;

    // ═══════════════════════════════════════════════════════════════
    // VIA API (ReportePrematch.Api) — 3 BDs directas
    // ═══════════════════════════════════════════════════════════════

    [HttpGet]
    public IActionResult VentasPorAgente()
    {
        if (!SesionActiva()) return RedirectToLogin();
        CargarViewBag("Ventas por Agente", "btnVentasAgente", "tblVentasAgente");
        return View();
    }

    [HttpGet]
    public IActionResult VentasPorAgenteWeb()
    {
        if (!SesionActiva()) return RedirectToLogin();
        CargarViewBag("Ventas por Agente Web", "btnVentasAgenteWeb", "tblVentasAgenteWeb");
        return View();
    }
    [HttpPost]
    public async Task<IActionResult> GetVentasPorAgenteWeb(string fechaD, string fechaH, string agente, string pais = null)
        => Json(await reportes.GetVentasPorAgenteWebAsync(fechaD, fechaH, agente, pais ?? Pais, Role));

    [HttpGet]
    public IActionResult VentasPorAgenteTaq()
    {
        if (!SesionActiva()) return RedirectToLogin();
        CargarViewBag("Ventas por Agente Taquilla", "btnVentasAgenteTaq", "tblVentasAgenteTaq");
        return View();
    }
    [HttpPost]
    public async Task<IActionResult> GetVentasPorAgenteTaq(string fechaD, string fechaH, string agente, string pais = null)
        => Json(await reportes.GetVentasPorAgenteTaqAsync(fechaD, fechaH, agente, Local, pais ?? Pais, Role));

    [HttpGet]
    public IActionResult RepTransaccion()
    {
        if (!SesionActiva()) return RedirectToLogin();
        CargarViewBag("Reporte Transacciones", "btnRepTransaccion", "tblRepTransaccion");
        return View();
    }
    [HttpPost]
    public async Task<IActionResult> GetRepTransaccion(string fechaD, string fechaH, string agente, string webSite = "")
        => Json(await reportes.GetRepTransaccionesAsync(fechaD, fechaH, agente, webSite));

    [HttpGet]
    public IActionResult ReporteTransaccion()
    {
        if (!SesionActiva()) return RedirectToLogin();
        CargarViewBag("Reporte Transacción", "btnReporteTransaccion", "tblReporteTransaccion");
        return View();
    }
    [HttpPost]
    public async Task<IActionResult> GetReporteTransaccion(string fechaD, string fechaH, string agente, string webSite = "")
        => Json(await reportes.GetRepTransaccionesAsync(fechaD, fechaH, agente, webSite));

    [HttpGet]
    public IActionResult Loteria()
    {
        if (!SesionActiva()) return RedirectToLogin();
        CargarViewBag("Lotería", "btnLoteria", "tblLoteria");
        return View();
    }
    [HttpPost]
    public async Task<IActionResult> GetLoteria(string fechaD, string fechaH, string agente)
        => Json(await reportes.GetLoteriasAsync(fechaD, fechaH, agente, Role, Pais));

    [HttpGet]
    public IActionResult LoteriaTriple()
    {
        if (!SesionActiva()) return RedirectToLogin();
        CargarViewBag("Lotería Triple", "btnLoteriaTriple", "tblLoteriaTriple");
        return View();
    }
    [HttpPost]
    public async Task<IActionResult> GetLoteriaTriple(string fechaD, string fechaH, string agente)
        => Json(await reportes.GetLoteriasTripleAsync(fechaD, fechaH, agente, Role));

    [HttpGet]
    public IActionResult Aviatrix()
    {
        if (!SesionActiva()) return RedirectToLogin();
        CargarViewBag("Aviatrix", "btnAviatrix", "tblAviatrix");
        return View();
    }
    [HttpPost]
    public async Task<IActionResult> GetAviatrix(string fechaD, string fechaH, string agente)
        => Json(await reportes.GetAviatrixAsync(fechaD, fechaH, agente ?? "", Role, Pais));

    [HttpGet]
    public IActionResult Casino7777()
    {
        if (!SesionActiva()) return RedirectToLogin();
        CargarViewBag("Casino 7777", "btnCasino7777", "tblCasino7777");
        return View();
    }
    [HttpPost]
    public async Task<IActionResult> GetCasino7777(string fechaInicio, string fechaFin, string agente)
        => Json(await reportes.GetCasino7777Async(fechaInicio, fechaFin, agente ?? ""));

    [HttpPost]
    public async Task<IActionResult> GetCasino7777ResumenGeneral(string fechaInicio, string fechaFin, string agente)
        => Json(await reportes.GetCasino7777ResumenGeneralAsync(fechaInicio, fechaFin, agente ?? ""));

    [HttpPost]
    public async Task<IActionResult> GetCasino7777Agentes()
        => Json(await reportes.GetCasino7777AgentesAsync());

    [HttpGet]
    public IActionResult Endorphine()
    {
        if (!SesionActiva()) return RedirectToLogin();
        CargarViewBag("Endorphine", "btnEndorphine", "tblEndorphine");
        return View();
    }
    [HttpPost]
    public async Task<IActionResult> GetEndorphine(string fechaInicio, string fechaFin, string agente)
        => Json(await reportes.GetEndorphineAsync(fechaInicio, fechaFin, agente ?? ""));

    [HttpPost]
    public async Task<IActionResult> GetEndorphineAgentes()
        => Json(await reportes.GetEndorphineAgentesAsync());

    [HttpPost]
    public async Task<IActionResult> GetEndorphineResumenGeneral(string fechaInicio, string fechaFin, string agente)
        => Json(await reportes.GetEndorphineResumenGeneralAsync(fechaInicio, fechaFin, agente ?? ""));

    [HttpGet]
    public IActionResult InOutGaming()
    {
        if (!SesionActiva()) return RedirectToLogin();
        CargarViewBag("InOutGaming", "btnInOutGaming", "tblInOutGaming");
        return View();
    }
    [HttpPost]
    public async Task<IActionResult> GetInOutGaming(string fechaInicio, string fechaFin, string agente)
        => Json(await reportes.GetInOutGamingAsync(fechaInicio, fechaFin, agente ?? ""));

    [HttpPost]
    public async Task<IActionResult> GetInOutGamingAgentes()
        => Json(await reportes.GetInOutGamingAgentesAsync());

    [HttpPost]
    public async Task<IActionResult> GetInOutGamingResumenGeneral(string fechaInicio, string fechaFin, string agente)
        => Json(await reportes.GetInOutGamingResumenGeneralAsync(fechaInicio, fechaFin, agente ?? ""));

    /// <summary>Lista de agentes desde GCITReportes API — reemplaza GetListAgencias (SOAP) en todas las vistas.</summary>
    [HttpPost]
    public async Task<IActionResult> GetListAgenciasApi()
        => Json(await reportes.GetEndorphineAgentesAsync());

    [HttpGet]
    public IActionResult FantasyBsb()
    {
        if (!SesionActiva()) return RedirectToLogin();
        CargarViewBag("Fantasy Baseball", "btnFantasyBsb", "tblFantasyBsb");
        return View();
    }
    [HttpPost]
    public async Task<IActionResult> GetFantasyBsb(string fechaD, string fechaH, string agente)
        => Json(await reportes.GetFantasyBsbAsync(fechaD, fechaH, agente, Role, Pais));

    [HttpPost]
    public async Task<IActionResult> GetDashboardClientes(string fechaD, string fechaH, string agente)
        => Json(await reportes.GetDashboardClientesAsync(fechaD, fechaH, agente ?? Agente));

    [HttpPost]
    public async Task<IActionResult> GetDashboardExtendido(string fechaD, string fechaH)
    {
        try { return Json(await reportes.GetDashboardExtendidoAsync(fechaD, fechaH, Agente, Pais)); }
        catch (Exception ex) { logger.LogError(ex, "GetDashboardExtendido failed"); return Json(null); }
    }

    [HttpPost]
    public async Task<IActionResult> GetVentasMonedaExtendido(string fechaD, string fechaH)
    {
        try { return Json(await reportes.GetVentasMonedaExtendidoAsync(fechaD, fechaH, Agente, Pais)); }
        catch (Exception ex) { logger.LogError(ex, "GetVentasMonedaExtendido failed"); return Json(null); }
    }

    [HttpPost]
    public async Task<IActionResult> GetChartsExtendido(string fechaD, string fechaH)
    {
        try { return Json(await reportes.GetChartsExtendidoAsync(fechaD, fechaH, Agente, Pais)); }
        catch (Exception ex) { logger.LogError(ex, "GetChartsExtendido failed"); return Json(null); }
    }

    [HttpPost]
    public async Task<IActionResult> GetTablesExtendido(string fechaD, string fechaH)
    {
        try { return Json(await reportes.GetTablesExtendidoAsync(fechaD, fechaH, Agente, Pais)); }
        catch (Exception ex) { logger.LogError(ex, "GetTablesExtendido failed"); return Json(null); }
    }

    // ── Detalle de ticket vía apiTools ───────────────────────────────────────
    [HttpPost]
    public async Task<IActionResult> GetDetalleTicket(long idticket)
    {
        try
        {
            var baseUrl   = HttpContext.RequestServices
                                .GetRequiredService<IConfiguration>()["ApiTools:BaseUrl"]
                            ?? "https://allws.staging-gc.com/apiTools/";
            var secretKey = HttpContext.RequestServices
                                .GetRequiredService<IConfiguration>()["ApiTools:SecretKey"]
                            ?? string.Empty;

            using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
            client.DefaultRequestHeaders.Add("SecretKey", secretKey);
            client.DefaultRequestHeaders.Add("accept", "*/*");

            var body    = System.Text.Json.JsonSerializer.Serialize(new { idticket, condetalle = true });
            var content = new StringContent(body, System.Text.Encoding.UTF8, "application/json");
            var resp    = await client.PostAsync($"{baseUrl.TrimEnd('/')}/api/Producto/Ticket", content);
            resp.EnsureSuccessStatusCode();

            var json = await resp.Content.ReadAsStringAsync();
            return Content(json, "application/json");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "GetDetalleTicket failed for ticket {Ticket}", idticket);
            return Json(new { estatus = false, mensaje = "Error al obtener detalle del ticket." });
        }
    }

    [HttpGet]
    public IActionResult IconBet()
    {
        if (!SesionActiva()) return RedirectToLogin();
        CargarViewBag("IconBet", "btnIconBet", "tblIconBet");
        return View();
    }
    // TODO: GetIconBet — pendiente de conectar al nuevo API
    // [HttpPost] public async Task<IActionResult> GetIconBet(string fechaD, string fechaH, string agente)
    //     => Json(await reportes.GetIconBetAsync(fechaD, fechaH, agente, Pais, Role));

    // ═══════════════════════════════════════════════════════════════
    // VIA SOAP (wsReportes)
    // ═══════════════════════════════════════════════════════════════

    [HttpGet]
    public IActionResult DeportesMasVendidos()
    {
        if (!SesionActiva()) return RedirectToLogin();
        CargarViewBag("Deportes más Vendidos", "btnDeportesMasVendidos");
        ViewBag.TableId1 = "tblDeportesMasVendidos1";
        ViewBag.TableId2 = "tblDeportesMasVendidos2";
        return View();
    }
    [HttpPost]
    public async Task<IActionResult> GetDeportesMasVendidosPorAgente(string fechaD, string fechaH, string agente)
        => Json(await reportes.GetDeportesMasVendidosPorAgenteAsync(fechaD, fechaH, agente, Pais, Role));
    [HttpPost]
    public async Task<IActionResult> GetDeportesMasVendidosPorDeporte(string fechaD, string fechaH, string agente)
        => Json(await reportes.GetDeportesMasVendidosPorDeporteAsync(fechaD, fechaH, agente, Pais, Role));

    [HttpGet]
    public IActionResult TicketsDetallados()
    {
        if (!SesionActiva()) return RedirectToLogin();
        CargarViewBag("Tickets Detallados", "btnTicketsDetallados", "tblTicketsDetallados");
        return View();
    }

    [HttpGet]
    public IActionResult TicketsDetalladosWeb()
    {
        if (!SesionActiva()) return RedirectToLogin();
        CargarViewBag("Tickets Detallados Web", "btnTicketsDetalladosWeb", "tblTicketsDetalladosWeb");
        return View();
    }
    [HttpPost]
    public async Task<IActionResult> GetTicketsDetalladosWeb(string fechaD, string fechaH, string agente)
        => Json(await reportes.GetTicketsDetalladosWebAsync(fechaD, fechaH, agente, Pais, Role));

    [HttpGet]
    public IActionResult TicketsDetalladosTaq()
    {
        if (!SesionActiva()) return RedirectToLogin();
        CargarViewBag("Tickets Detallados Taquilla", "btnTicketsDetalladosTaq", "tblTicketsDetalladosTaq");
        return View();
    }
    [HttpPost]
    public async Task<IActionResult> GetTicketsDetalladosTaq(string fechaD, string fechaH, string agente)
        => Json(await reportes.GetTicketsDetalladosTaqAsync(fechaD, fechaH, agente, Pais, Role));

    [HttpGet]
    public IActionResult VentasPorPais()
    {
        if (!SesionActiva()) return RedirectToLogin();
        CargarViewBag("Ventas por País", "btnVentasPorPais", "tblVentasPorPais");
        return View();
    }

    [HttpGet]
    public IActionResult VentasPorPaisWeb()
    {
        if (!SesionActiva()) return RedirectToLogin();
        CargarViewBag("Ventas por País Web", "btnVentasPaisWeb", "tblVentasPaisWeb");
        return View();
    }
    [HttpPost]
    public async Task<IActionResult> GetVentasPorPaisWeb(string fechaD, string fechaH, string pais)
        => Json(await reportes.GetVentasPorPaisWebAsync(fechaD, fechaH, pais));

    [HttpGet]
    public IActionResult VentasPorPaisTaq()
    {
        if (!SesionActiva()) return RedirectToLogin();
        CargarViewBag("Ventas por País Taquilla", "btnVentasPaisTaq", "tblVentasPaisTaq");
        return View();
    }
    [HttpPost]
    public async Task<IActionResult> GetVentasPorPaisTaq(string fechaD, string fechaH, string pais)
        => Json(await reportes.GetVentasPorPaisTaqAsync(fechaD, fechaH, pais));

    [HttpGet]
    public IActionResult RetirosRecargas()
    {
        if (!SesionActiva()) return RedirectToLogin();
        CargarViewBag("Retiros y Recargas", "btnRetirosRecargas", "tblRetirosRecargas");
        return View();
    }
    [HttpPost]
    public async Task<IActionResult> GetRetirosRecargas(string fechaD, string fechaH, string agente)
        => Json(await reportes.GetRetirosRecargasAsync(fechaD, fechaH, agente, Pais, Role));

    [HttpGet]
    public IActionResult SaldosDisponibles()
    {
        if (!SesionActiva()) return RedirectToLogin();
        CargarViewBag("Saldos Disponibles", "btnSaldosDisponibles", "tblSaldosDisponibles");
        return View();
    }
    [HttpPost]
    public async Task<IActionResult> GetSaldosDisponibles(string agente)
        => Json(await reportes.GetSaldosDisponiblesAsync(agente ?? Agente));

    [HttpGet]
    public IActionResult VentasPorAgenteGameID()
    {
        if (!SesionActiva()) return RedirectToLogin();
        CargarViewBag("Ticket por GameID", "btnVentasGameID", "tblVentasGameID");
        return View();
    }
    [HttpPost]
    public async Task<IActionResult> GetVentasPorAgenteGameID(string agente, string gameId, string filtApuesta)
        => Json(await reportes.GetVentasAgenteGameIDAsync(
            string.IsNullOrEmpty(agente) ? Agente : agente, Pais, Role, gameId, filtApuesta));

    [HttpGet]
    public IActionResult CasinoMaquinitas()
    {
        if (!SesionActiva()) return RedirectToLogin();
        CargarViewBag("Casino Maquinitas", "btnCasinoMaquinitas", "tblCasinoMaquinitas");
        return View();
    }
    [HttpPost]
    public async Task<IActionResult> GetCasinoMaquinitas(string fechaD, string fechaH, string agente)
        => Json(await reportes.GetCasinoMaquinitasAsync(fechaD, fechaH, agente, Pais, Role));

    [HttpGet]
    public IActionResult CasinoSlotGrecia()
    {
        if (!SesionActiva()) return RedirectToLogin();
        CargarViewBag("Casino Slot Grecia", "btnCasinoSlotGrecia", "tblCasinoSlotGrecia");
        return View();
    }
    [HttpPost]
    public async Task<IActionResult> GetCasinoSlotGrecia(string fechaD, string fechaH, string agente)
        => Json(await reportes.GetCasinoSlotGreciaAsync(fechaD, fechaH, agente, Pais, Role));

    [HttpGet]
    public IActionResult CasinoSlotRusia()
    {
        if (!SesionActiva()) return RedirectToLogin();
        CargarViewBag("Casino Slot Rusia", "btnCasinoSlotRusia", "tblCasinoSlotRusia");
        return View();
    }
    [HttpPost]
    public async Task<IActionResult> GetCasinoSlotRusia(string fechaD, string fechaH, string agente)
        => Json(await reportes.GetCasinoSlotRussiaAsync(fechaD, fechaH, agente, Pais, Role));

    [HttpGet]
    public IActionResult IconSlot()
    {
        if (!SesionActiva()) return RedirectToLogin();
        CargarViewBag("IconBet Slots", "btnIconSlot", "tblIconSlot");
        return View();
    }
    [HttpPost]
    public async Task<IActionResult> GetIconSlot(string fechaD, string fechaH, string agente)
        => Json(await reportes.GetIconSlotAsync(fechaD, fechaH, agente, Pais, Role));

    [HttpGet]
    public IActionResult Props()
    {
        if (!SesionActiva()) return RedirectToLogin();
        CargarViewBag("Props", "btnProps", "tblProps");
        return View();
    }
    [HttpPost]
    public async Task<IActionResult> GetProps(string fechaD, string fechaH, string agente)
        => Json(await reportes.GetPropsAsync(fechaD, fechaH, agente, Pais, Role));

    [HttpGet]
    public IActionResult Poker()
    {
        if (!SesionActiva()) return RedirectToLogin();
        CargarViewBag("Poker", "btnPoker", "tblPoker");
        return View();
    }
    [HttpPost]
    public async Task<IActionResult> GetPoker(string fechaD, string fechaH, string agente)
        => Json(await reportes.GetPokerAsync(fechaD, fechaH, agente, Pais, Role));

    [HttpGet]
    public IActionResult Pragmatic()
    {
        if (!SesionActiva()) return RedirectToLogin();
        CargarViewBag("Pragmatic Play", "btnPragmatic", "tblPragmatic");
        return View();
    }
    [HttpPost]
    public async Task<IActionResult> GetPragmatic(string fechaD, string fechaH, string agente)
        => Json(await reportes.GetPragmaticAsync(fechaD, fechaH, agente, Pais, Role));

    [HttpGet]
    public IActionResult VirtualesBR()
    {
        if (!SesionActiva()) return RedirectToLogin();
        CargarViewBag("Virtuales BR", "btnVirtualesBR", "tblVirtualesBR");
        return View();
    }
    [HttpPost]
    public async Task<IActionResult> GetVirtualesBR(string fechaD, string fechaH, string agente)
        => Json(await reportes.GetVirtualesBRAsync(fechaD, fechaH, agente, Pais, Role));

    [HttpGet]
    public IActionResult VirtualesCaribe()
    {
        if (!SesionActiva()) return RedirectToLogin();
        CargarViewBag("Virtuales Caribe", "btnVirtualesCaribe", "tblVirtualesCaribe");
        return View();
    }
    [HttpPost]
    public async Task<IActionResult> GetVirtualesCaribe(string fechaD, string fechaH, string agente)
        => Json(await reportes.GetVirtualesCarAsync(fechaD, fechaH, agente, Pais, Role));

    [HttpGet]
    public IActionResult PromoTicket()
    {
        if (!SesionActiva()) return RedirectToLogin();
        CargarViewBag("PromoTicket", "btnPromoTicket", "tblPromoTicket");
        return View();
    }
    [HttpPost]
    public async Task<IActionResult> GetPromoTicket(string fechaD, string fechaH, string agente, string webSite = "")
        => Json(await reportes.GetTicketsPromoAsync(fechaD, fechaH, agente, webSite));

    [HttpGet]
    public IActionResult ClienteReg()
    {
        if (!SesionActiva()) return RedirectToLogin();
        CargarViewBag("Clientes Registrados", "btnClienteReg", "tblClienteReg");
        return View();
    }
    [HttpPost]
    public async Task<IActionResult> GetClienteReg(string fechaD, string fechaH, string agente)
        => Json(await reportes.GetClientesRegAsync(fechaD, fechaH, agente, Role, Pais));

    // ── Vista unificada BusquedaTickets (consume GCITReportes REST API) ──────
    [HttpGet]
    public IActionResult BusquedaTickets()
    {
        if (!SesionActiva()) return RedirectToLogin();
        CargarViewBag("Búsqueda de Tickets", "btnBusquedaTickets", "tblBusquedaTickets");
        return View();
    }
    [HttpPost]
    public async Task<IActionResult> GetBusquedaTicketsTaqApi(string fecha, string agente, long ticket, string operacion)
        => Json(await reportes.GetBusquedaTicketsTaqApiAsync(fecha, agente, ticket, operacion));
    [HttpPost]
    public async Task<IActionResult> GetBusquedaTicketsWebApi(string fecha, string agente, long ticket, string operacion)
        => Json(await reportes.GetBusquedaTicketsWebApiAsync(fecha, agente, ticket, operacion));

    // ── Legacy SOAP (mantenidas para compatibilidad) ─────────────────────────
    [HttpGet]
    public IActionResult BusquedaTicketsWeb()
    {
        if (!SesionActiva()) return RedirectToLogin();
        CargarViewBag("Búsqueda Tickets Web", "btnBusquedaTicketsWeb", "tblBusquedaTicketsWeb");
        return View();
    }
    [HttpPost]
    public async Task<IActionResult> GetBusquedaTicketsWeb(string fechaD, string fechaH, string agente, string operacion, string ticket)
        => Json(await reportes.GetBusquedaTicketsWebAsync(fechaD, fechaH, agente, operacion, ticket, Pais, Role));

    [HttpGet]
    public IActionResult BusquedaTicketsTaq()
    {
        if (!SesionActiva()) return RedirectToLogin();
        CargarViewBag("Búsqueda Tickets Taquilla", "btnBusquedaTicketsTaq", "tblBusquedaTicketsTaq");
        return View();
    }
    [HttpPost]
    public async Task<IActionResult> GetBusquedaTicketsTaq(string fechaD, string fechaH, string agente, string operacion, string ticket)
        => Json(await reportes.GetBusquedaTicketsTaqAsync(fechaD, fechaH, agente, operacion, ticket, Pais, Role));

    [HttpGet]
    public IActionResult VerCierreTaquilla()
    {
        if (!SesionActiva()) return RedirectToLogin();
        CargarViewBag("Cierre de Taquilla", "btnCierreTaquilla", "tblCierreTaquilla");
        return View();
    }
    [HttpPost]
    public async Task<IActionResult> GetCierreTaquilla(string agente, string agencia, string usuario, string fecha)
        => Json(await reportes.GetCierreTaquillaAsync(agente, agencia, usuario, fecha));

    [HttpGet]
    public IActionResult VentasDetalladasLive()
    {
        if (!SesionActiva()) return RedirectToLogin();
        CargarViewBag("Ventas Detalladas Live", "btnVentasDetalladasLive", "tblVentasDetalladasLive");
        return View();
    }
    [HttpPost]
    public async Task<IActionResult> GetVentasDetalladasLive(string fechaD, string fechaH, string agente)
        => Json(await reportes.GetVentasDetalladasLiveAsync(fechaD, fechaH, agente, Pais, Role));

    [HttpGet]
    public IActionResult TicketsEnJuego()
    {
        if (!SesionActiva()) return RedirectToLogin();
        CargarViewBag("Tickets en Juego", "btnTicketsEnJuego", "tblTicketsEnJuego");
        return View();
    }

    [HttpPost]
    public async Task<IActionResult> GetTicketsEnJuegoPorPagarWeb(string fechaD, string fechaH, string agente)
        => Json(await reportes.GetTicketsEnJuegoPorPagarWebAsync(fechaD, fechaH, agente));
    [HttpPost]
    public async Task<IActionResult> GetTicketsEnJuegoPorCobrarWeb(string fechaD, string fechaH, string agente)
        => Json(await reportes.GetTicketsEnJuegoPorCobrarWebAsync(fechaD, fechaH, agente));

    [HttpGet]
    public IActionResult TicketsEnJuegoWeb()
    {
        if (!SesionActiva()) return RedirectToLogin();
        CargarViewBag("Tickets en Juego Web");
        ViewBag.TableId1 = "tblTicketsEnJuegoPorPagar";
        ViewBag.TableId2 = "tblTicketsEnJuegoPorCobrar";
        return View();
    }

    [HttpGet]
    public IActionResult TicketsEnJuegoTaq()
    {
        if (!SesionActiva()) return RedirectToLogin();
        CargarViewBag("Tickets en Juego Taquilla");
        ViewBag.TableId1 = "tblTicketsEnJuegoPorPagarTaq";
        ViewBag.TableId2 = "tblTicketsEnJuegoPorCobrarTaq";
        return View();
    }
    [HttpPost]
    public async Task<IActionResult> GetTicketsEnJuegoPorPagarTaq(string fechaD, string fechaH, string agente)
        => Json(await reportes.GetTicketsEnJuegoPorPagarTaqAsync(fechaD, fechaH, agente));
    [HttpPost]
    public async Task<IActionResult> GetTicketsEnJuegoPorCobrarTaq(string fechaD, string fechaH, string agente)
        => Json(await reportes.GetTicketsEnJuegoPorCobrarTaqAsync(fechaD, fechaH, agente));

    [HttpGet]
    public IActionResult VentasPorAgenteNikols()
    {
        if (!SesionActiva()) return RedirectToLogin();
        CargarViewBag("Ventas por Agente Nikols", "btnVentasAgenteNikols", "tblVentasAgenteNikols");
        return View("~/Views/Reportes/VentasPorAgenteNikols.cshtml");
    }

    [HttpGet]
    public IActionResult VentasPorAgenteNikolsWeb()
    {
        if (!SesionActiva()) return RedirectToLogin();
        CargarViewBag("Ventas por Agente Nikols Web", "btnVentasNikolsWeb", "tblVentasNikolsWeb");
        return View();
    }
    [HttpPost]
    public async Task<IActionResult> GetVentasPorAgenteNikolsWeb(string fechaD, string fechaH, string agente)
        => Json(await reportes.GetVentasPorAgenteNikolsWebAsync(fechaD, fechaH, agente, Pais, Role));

    [HttpGet]
    public IActionResult VentasPorAgenteNikolsTaq()
    {
        if (!SesionActiva()) return RedirectToLogin();
        CargarViewBag("Ventas por Agente Nikols Taquilla", "btnVentasNikolsTaq", "tblVentasNikolsTaq");
        return View();
    }
    [HttpPost]
    public async Task<IActionResult> GetVentasPorAgenteNikolsTaq(string fechaD, string fechaH, string agente)
        => Json(await reportes.GetVentasPorAgenteNikolsTaqAsync(fechaD, fechaH, agente, Local, Pais, Role));

    [HttpGet]
    public IActionResult Estadisticas()
    {
        if (!SesionActiva()) return RedirectToLogin();
        CargarViewBag("Estadísticas", "btnEstadisticas", "tblEstadisticas");
        return View();
    }

    [HttpGet]
    public IActionResult EstadisticasWeb()
    {
        if (!SesionActiva()) return RedirectToLogin();
        CargarViewBag("Estadísticas Web", "btnEstadisticasWeb", "tblEstadisticasWeb");
        return View();
    }
    [HttpPost]
    public async Task<IActionResult> GetEstadisticasWeb(string fechaD, string fechaH, string agente)
        => Json(await reportes.GetEstadisticasWebAsync(fechaD, fechaH, agente, Pais, Role));

    [HttpGet]
    public IActionResult EstadisticasTaq()
    {
        if (!SesionActiva()) return RedirectToLogin();
        CargarViewBag("Estadísticas Taquilla", "btnEstadisticasTaq", "tblEstadisticasTaq");
        return View();
    }
    [HttpPost]
    public async Task<IActionResult> GetEstadisticasTaq(string fechaD, string fechaH, string agente)
        => Json(await reportes.GetEstadisticasTaqAsync(fechaD, fechaH, agente, Local, Pais, Role));

    [HttpGet]
    public IActionResult Hipismo()
    {
        if (!SesionActiva()) return RedirectToLogin();
        CargarViewBag("Hipismo", "btnHipismo", "tblHipismo");
        return View();
    }
    [HttpPost]
    public async Task<IActionResult> GetHipismo(string fechaD, string fechaH, string agente)
        => Json(await reportes.GetHipismoAsync(fechaD, fechaH, agente, Pais, Role));

    [HttpPost]
    public async Task<IActionResult> GetDetallesHipismo(string fechaD, string fechaH, string usuario)
        => Json(await reportes.GetDetallesHipismoAsync(fechaD, fechaH, usuario));

    [HttpGet]
    public IActionResult DashbrdExtendido()
    {
        if (!SesionActiva()) return RedirectToLogin();
        CargarViewBag("Dashboard Extendido", "btnDashboardExtendido", "tblDashboardExtendido");
        return View();
    }

    [HttpPost]
    public async Task<IActionResult> GetListAgencias(string agente)
        => Json(await reportes.GetListAgenciasAsync(agente, Pais, Role));

    /// <summary>
    /// Diagnóstico de conectividad con wsReportes.
    /// Navega a /Reportes/DiagAgencias para ver la respuesta cruda del servidor SOAP.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> DiagAgencias()
    {
        if (!SesionActiva()) return RedirectToLogin();
        try
        {
            var config = HttpContext.RequestServices.GetRequiredService<IConfiguration>();
            var wsUrl = config["SoapServices:WsReportesUrl"] ?? "⚠ SoapServices:WsReportesUrl NO CONFIGURADO";
            var clave = config["SoapServices:ClavePropia"] ?? "⚠ SoapServices:ClavePropia NO CONFIGURADO";

            var sql = "SELECT DISTINCT UPPER(nombreagente) AS NombreAgente FROM Agentes ORDER BY NombreAgente";
            var escapedClave = System.Security.SecurityElement.Escape(clave);
            var escapedSql = System.Security.SecurityElement.Escape(sql);

            var envelope = $@"<?xml version=""1.0"" encoding=""utf-8""?>
<soap:Envelope xmlns:xsi=""http://www.w3.org/2001/XMLSchema-instance""
               xmlns:xsd=""http://www.w3.org/2001/XMLSchema""
               xmlns:soap=""http://schemas.xmlsoap.org/soap/envelope/"">
  <soap:Body>
    <ejecutarQuerys xmlns=""http://tempuri.org/"">
      <clave>{escapedClave}</clave>
      <query>{escapedSql}</query>
      <db>2</db>
    </ejecutarQuerys>
  </soap:Body>
</soap:Envelope>";

            using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
            client.DefaultRequestHeaders.Add("SOAPAction", "\"http://tempuri.org/ejecutarQuerys\"");
            var body = new StringContent(envelope, System.Text.Encoding.UTF8, "text/xml");
            var response = await client.PostAsync(wsUrl, body);
            var xml = await response.Content.ReadAsStringAsync();

            return Content(
                $"URL    : {wsUrl}\n" +
                $"HTTP   : {(int)response.StatusCode} {response.StatusCode}\n" +
                $"Sesión : agente={Agente}  pais={Pais}  role={Role}\n" +
                $"\n--- RESPUESTA SOAP ---\n{xml}",
                "text/plain; charset=utf-8");
        }
        catch (Exception ex)
        {
            return Content($"EXCEPCIÓN: {ex.GetType().Name}\n{ex.Message}\n\n{ex.StackTrace}", "text/plain; charset=utf-8");
        }
    }
}