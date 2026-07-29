using ReportePrematch.Web.Models;
using ReportePrematch.Web.Services.Interfaces;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Xml;

namespace ReportePrematch.Web.Services.Clients;

/// <summary>
/// Autenticación vía SOAP ADWS (adws.asmx) + validación Cloudflare Turnstile.
/// Implementación raw HTTP — sin WCF ni svcutil.
/// </summary>
public sealed class AuthService(
    IConfiguration configuration,
    IHttpClientFactory httpClientFactory,
    ILogger<AuthService> logger) : IAuthService
{
    private readonly string _claveWs = configuration["SoapServices:ClavePropia"]
                                       ?? throw new InvalidOperationException("ClavePropia no configurada.");
    private readonly string _adwsUrl = configuration["SoapServices:AdwsUrl"]
                                       ?? throw new InvalidOperationException("AdwsUrl no configurada.");
    private readonly string _turnstileSecret = configuration["Cloudflare:TurnstileSecretKey"]
                                               ?? throw new InvalidOperationException("TurnstileSecretKey no configurada.");

    // ─────────────────────────────────────────────────────────────────────────
    // Login
    // ─────────────────────────────────────────────────────────────────────────
    public async Task<SessionData?> ValidateUserAsync(string usuario, string password)
    {
        // ── Bypass de desarrollo ─────────────────────────────────────────────
        var bypass = configuration.GetSection("DevBypass");
        if (bypass.GetValue<bool>("Enabled"))
        {
            logger.LogWarning("DevBypass activo — autenticación omitida para usuario '{U}'", usuario);
            return new SessionData
            {
                Usuario = usuario,
                IdUsuario = bypass["IdUsuario"] ?? "1",
                Agente = bypass["Agente"] ?? string.Empty,
                IdAgente = bypass["IdAgente"] ?? "0",
                Pais = bypass["Pais"] ?? string.Empty,
                TipoUsuario = bypass["TipoUsuario"] ?? "Usuario suAdmin",
                IdLocal = bypass["IdLocal"] ?? "0",
                IsValidated = true
            };
        }

        // ── Producción: SOAP adws.asmx → Login(clave_ws, entidad, 1) ─────────
        try
        {
            // Escapar caracteres XML en credenciales
            var usuarioXml = System.Security.SecurityElement.Escape(usuario);
            var passwordXml = System.Security.SecurityElement.Escape(password);
            var claveXml = System.Security.SecurityElement.Escape(_claveWs);

            var envelope = $@"<?xml version=""1.0"" encoding=""utf-8""?>
<soap:Envelope xmlns:xsi=""http://www.w3.org/2001/XMLSchema-instance""
               xmlns:xsd=""http://www.w3.org/2001/XMLSchema""
               xmlns:soap=""http://schemas.xmlsoap.org/soap/envelope/"">
  <soap:Body>
    <Login xmlns=""http://gcit.org/"">
      <clave>{claveXml}</clave>
      <Usuario>
        <usuario>{usuarioXml}</usuario>
        <contrasena>{passwordXml}</contrasena>
        <compania>cordialito</compania>
      </Usuario>
      <verificando>1</verificando>
    </Login>
  </soap:Body>
</soap:Envelope>";

            using var client = httpClientFactory.CreateClient();
            client.Timeout = TimeSpan.FromSeconds(30);
            var content = new StringContent(envelope, Encoding.UTF8, "text/xml");
            content.Headers.Add("SOAPAction", "\"http://gcit.org/Login\"");

            var httpResponse = await client.PostAsync(_adwsUrl, content);

            if (!httpResponse.IsSuccessStatusCode)
            {
                logger.LogWarning("ADWS respondió HTTP {Code} para usuario {U}",
                    (int)httpResponse.StatusCode, usuario);
                return null;
            }

            var xml = await httpResponse.Content.ReadAsStringAsync();
            logger.LogDebug("ADWS Login respuesta: {Xml}", xml);

            return ParseLoginResponse(xml, usuario);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error llamando ADWS Login para usuario {U}", usuario);
            return null;
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Parsear XML de respuesta del Login
    // ─────────────────────────────────────────────────────────────────────────
    private SessionData? ParseLoginResponse(string xml, string usuarioInput)
    {
        try
        {
            var doc = new XmlDocument();
            doc.LoadXml(xml);

            var ns = new XmlNamespaceManager(doc.NameTable);
            ns.AddNamespace("soap", "http://schemas.xmlsoap.org/soap/envelope/");
            ns.AddNamespace("t", "http://tempuri.org/");

            // Verificar Exitoso
            var exitosoNode = doc.SelectSingleNode("//Exitoso", ns)
                           ?? doc.SelectSingleNode("//*[local-name()='Exitoso']");
            if (exitosoNode == null || !bool.TryParse(exitosoNode.InnerText, out var exitoso) || !exitoso)
            {
                logger.LogWarning("ADWS Login: Exitoso=false para usuario {U}", usuarioInput);
                return null;
            }

            // Verificar Mensaje (-1 = usuario no existe, -3 = clave incorrecta)
            var mensajeNode = doc.SelectSingleNode("//*[local-name()='Mensaje']");
            var mensaje = mensajeNode?.InnerText ?? string.Empty;
            if (mensaje is "-1" or "-3")
            {
                logger.LogWarning("ADWS Login: credenciales inválidas (Mensaje={M}) para {U}", mensaje, usuarioInput);
                return null;
            }

            // Extraer datos de ClaseUsuario
            var usuarioNode = doc.SelectSingleNode("//*[local-name()='ClaseUsuario']/*[local-name()='usuario']");
            var idAgenteNode = doc.SelectSingleNode("//*[local-name()='ClaseUsuario']/*[local-name()='idAgente']");
            // WSDL define el campo como "idlocal" (todo minúsculas)
            var idLocalNode = doc.SelectSingleNode("//*[local-name()='ClaseUsuario']/*[local-name()='idlocal']")
                            ?? doc.SelectSingleNode("//*[local-name()='ClaseUsuario']/*[local-name()='idLocal']");

            // Extraer datos de ClaseUsuarioAcceso
            var idUsuarioNode = doc.SelectSingleNode("//*[local-name()='ClaseUsuarioAcceso']/*[local-name()='idusuario']");
            var agenteNode = doc.SelectSingleNode("//*[local-name()='ClaseUsuarioAcceso']/*[local-name()='accesosoloAgente']");
            var paisNode = doc.SelectSingleNode("//*[local-name()='ClaseUsuarioAcceso']/*[local-name()='accesosolopais']");

            var agente = agenteNode?.InnerText ?? string.Empty;
            var pais = paisNode?.InnerText ?? string.Empty;

            return new SessionData
            {
                Usuario = usuarioNode?.InnerText ?? usuarioInput,
                IdUsuario = idUsuarioNode?.InnerText ?? "0",
                Agente = agente,
                IdAgente = idAgenteNode?.InnerText ?? "0",
                Pais = pais,
                IdLocal = idLocalNode?.InnerText ?? "0",
                TipoUsuario = DeterminarTipoUsuario(agente, pais),
                IsValidated = true
            };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error parseando respuesta XML del Login ADWS");
            return null;
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Cloudflare Turnstile
    // ─────────────────────────────────────────────────────────────────────────
    public async Task<bool> ValidateTurnstileAsync(string token, string remoteIp)
    {
        try
        {
            using var client = httpClientFactory.CreateClient();
            var payload = new Dictionary<string, string>
            {
                ["secret"] = _turnstileSecret,
                ["response"] = token,
                ["remoteip"] = remoteIp
            };

            var response = await client.PostAsync(
                "https://challenges.cloudflare.com/turnstile/v0/siteverify",
                new FormUrlEncodedContent(payload));

            if (!response.IsSuccessStatusCode) return false;

            var json = await response.Content.ReadFromJsonAsync<JsonElement>();
            return json.TryGetProperty("success", out var success) && success.GetBoolean();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error validando Turnstile");
            return false;
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Helpers
    // ─────────────────────────────────────────────────────────────────────────
    private static string DeterminarTipoUsuario(string agente, string pais)
    {
        if (!string.IsNullOrEmpty(agente)) return "Usuario Regular";
        if (!string.IsNullOrEmpty(pais)) return "Usuario Administrador";
        return "Usuario suAdmin";
    }
}