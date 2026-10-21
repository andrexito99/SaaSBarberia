using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SaaSBarberia.Data;
using SaaSBarberia.Models;

namespace SaaSBarberia.Services;

public interface IWhatsAppService
{
    Task EnviarConfirmacionAsync(Cita cita);
    Task EnviarCancelacionAsync(Cita cita);
    Task EnviarReprogramacionAsync(Cita cita);
    Task EnviarRecordatorio24HorasAsync(Cita cita);
    Task EnviarRecordatorio2HorasAsync(Cita cita);
    Task EnviarMensajePruebaAsync(int tenantId, string telefono);
    Task EnviarMensajePersonalizadoAsync(int tenantId, int? clienteId, string telefono, string mensaje);
    Task EnviarEventoCitaAsync(Cita cita, string tipo);
    Task ProcesarRecordatoriosAsync(DateTime fechaActual, CancellationToken cancellationToken = default);
}

public class WhatsAppService : IWhatsAppService
{
    private readonly HttpClient _httpClient;
    private readonly BarberSaaSDbContext _context;
    private readonly WhatsAppConfiguracionService _configuraciones;
    private readonly WhatsAppPlantillaService _plantillas;
    private readonly IConfiguration _configuration;

    public WhatsAppService(HttpClient httpClient, BarberSaaSDbContext context, WhatsAppConfiguracionService configuraciones, WhatsAppPlantillaService plantillas, IConfiguration configuration)
        => (_httpClient, _context, _configuraciones, _plantillas, _configuration) = (httpClient, context, configuraciones, plantillas, configuration);

    public async Task EnviarEventoCitaAsync(Cita cita, string tipo)
    {
        try
        {
            var completa = await _context.Citas.IgnoreQueryFilters().Include(c => c.Cliente).Include(c => c.Barbero).ThenInclude(b => b.Usuario).Include(c => c.Servicio).Include(c => c.Tenant)
                .FirstOrDefaultAsync(c => c.Id == cita.Id && c.TenantId == cita.TenantId);
            if (completa != null) await EnviarCitaAsync(completa, tipo, CancellationToken.None);
        }
        catch
        {
            // WhatsApp nunca debe impedir crear, cancelar o reprogramar una cita.
        }
    }

    public Task EnviarConfirmacionAsync(Cita cita) => EnviarEventoCitaAsync(cita, "Confirmacion");
    public Task EnviarCancelacionAsync(Cita cita) => EnviarEventoCitaAsync(cita, "Cancelacion");
    public Task EnviarReprogramacionAsync(Cita cita) => EnviarEventoCitaAsync(cita, "Reprogramacion");
    public Task EnviarRecordatorio24HorasAsync(Cita cita) => EnviarEventoCitaAsync(cita, "Recordatorio24Horas");
    public Task EnviarRecordatorio2HorasAsync(Cita cita) => EnviarEventoCitaAsync(cita, "Recordatorio2Horas");

    public Task EnviarMensajePruebaAsync(int tenantId, string telefono) => EnviarMensajePersonalizadoAsync(tenantId, null, telefono, "Mensaje de prueba enviado desde la configuración de WhatsApp Business.");

    public async Task EnviarMensajePersonalizadoAsync(int tenantId, int? clienteId, string telefono, string mensaje)
    {
        if (!await _configuraciones.ModuloHabilitadoAsync(tenantId)) return;
        var historial = new WhatsAppMensajeHistorial { TenantId = tenantId, ClienteId = clienteId, Telefono = NormalizarTelefono(telefono), Tipo = "Personalizado", Mensaje = mensaje, Fecha = DateTime.Now, Estado = "Pendiente" };
        var configuracion = await _configuraciones.ObtenerActivaAsync(tenantId);
        if (configuracion == null)
        {
            historial.Estado = "Error";
            historial.Error = "WhatsApp no está conectado para esta barbería.";
            _context.WhatsAppMensajesHistorial.Add(historial);
            await _context.SaveChangesAsync();
            return;
        }
        _context.WhatsAppMensajesHistorial.Add(historial);
        await _context.SaveChangesAsync();
        await EnviarPorMetaAsync(historial, configuracion, CancellationToken.None);
    }

    public async Task ProcesarRecordatoriosAsync(DateTime fechaActual, CancellationToken cancellationToken = default)
    {
        var citas = await _context.Citas.IgnoreQueryFilters().Include(c => c.Cliente).Include(c => c.Barbero).ThenInclude(b => b.Usuario).Include(c => c.Servicio).Include(c => c.Tenant)
            .Where(c => c.Estado != "Cancelada" && c.FechaHora >= fechaActual && c.FechaHora <= fechaActual.AddHours(25)).ToListAsync(cancellationToken);

        foreach (var cita in citas)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var faltante = cita.FechaHora - fechaActual;
            if (faltante >= TimeSpan.FromHours(23.9) && faltante <= TimeSpan.FromHours(24.1)) await EnviarCitaAsync(cita, "Recordatorio24Horas", cancellationToken);
            if (faltante >= TimeSpan.FromHours(1.9) && faltante <= TimeSpan.FromHours(2.1)) await EnviarCitaAsync(cita, "Recordatorio2Horas", cancellationToken);
        }
    }

    private async Task EnviarCitaAsync(Cita cita, string tipo, CancellationToken cancellationToken)
    {
        if (!await _configuraciones.ModuloHabilitadoAsync(cita.TenantId)) return;
        if (await _context.WhatsAppMensajesHistorial.IgnoreQueryFilters().AnyAsync(h => h.TenantId == cita.TenantId && h.CitaId == cita.Id && h.Tipo == tipo, cancellationToken)) return;

        var contenido = await _plantillas.ObtenerContenidoAsync(cita.TenantId, tipo);
        var mensaje = ReemplazarVariables(contenido, cita);
        var historial = new WhatsAppMensajeHistorial { TenantId = cita.TenantId, CitaId = cita.Id, ClienteId = cita.ClienteId, BarberoId = cita.BarberoId, Telefono = NormalizarTelefono(cita.Cliente?.Telefono ?? string.Empty), Tipo = tipo, Mensaje = mensaje, Fecha = DateTime.UtcNow, Estado = "Pendiente" };
        var configuracion = await _configuraciones.ObtenerActivaAsync(cita.TenantId);
        if (configuracion == null || !DebeEnviar(configuracion, tipo))
        {
            historial.Estado = "Error";
            historial.Error = configuracion == null ? "WhatsApp no está conectado para esta barbería." : "El envío de este tipo de mensaje está desactivado.";
            _context.WhatsAppMensajesHistorial.Add(historial);
            await _context.SaveChangesAsync(cancellationToken);
            return;
        }
        if (string.IsNullOrWhiteSpace(historial.Telefono))
        {
            historial.Estado = "Error";
            historial.Error = "El cliente no tiene un teléfono válido para WhatsApp.";
            _context.WhatsAppMensajesHistorial.Add(historial);
            await _context.SaveChangesAsync(cancellationToken);
            return;
        }
        _context.WhatsAppMensajesHistorial.Add(historial);
        try { await _context.SaveChangesAsync(cancellationToken); }
        catch (DbUpdateException)
        {
            _context.Entry(historial).State = EntityState.Detached;
            if (await _context.WhatsAppMensajesHistorial.IgnoreQueryFilters().AnyAsync(h => h.TenantId == cita.TenantId && h.CitaId == cita.Id && h.Tipo == tipo, cancellationToken)) return;
            throw;
        }

        await EnviarPorMetaAsync(historial, configuracion, cancellationToken);
    }

    private async Task EnviarPorMetaAsync(
    WhatsAppMensajeHistorial historial,
    WhatsAppConfiguracion configuracion,
    CancellationToken cancellationToken)
    {
        try
        {
            var version = _configuration["MetaWhatsApp:GraphApiVersion"] ?? "v23.0";

            var request = new HttpRequestMessage(
                HttpMethod.Post,
                $"https://graph.facebook.com/{version}/{configuracion.PhoneNumberId}/messages");

            request.Headers.Authorization =
                new AuthenticationHeaderValue("Bearer", configuracion.AccessTokenProtegido);

            var payload = new
            {
                messaging_product = "whatsapp",
                to = historial.Telefono,
                type = "text",
                text = new
                {
                    body = historial.Mensaje
                }
            };

            request.Content = new StringContent(
                JsonSerializer.Serialize(payload),
                Encoding.UTF8,
                "application/json");

            using var response = await _httpClient.SendAsync(request, cancellationToken);

            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
            historial.RespuestaMeta = ResumenError(responseBody);

            if (response.IsSuccessStatusCode)
            {
                using var json = JsonDocument.Parse(responseBody);

                if (json.RootElement.TryGetProperty("messages", out var mensajes) &&
                    mensajes.GetArrayLength() > 0 &&
                    mensajes[0].TryGetProperty("id", out var id))
                {
                    historial.MetaMessageId = id.GetString();
                }

                historial.Estado = "Enviado";
                historial.Error = null;
            }
            else
            {
                historial.Estado = "Error";
                historial.Error = ResumenError(responseBody);
            }
        }
        catch (Exception ex)
        {
            historial.Estado = "Error";
            historial.Error = ex.Message.Length > 2000
                ? ex.Message[..2000]
                : ex.Message;
        }

        await _context.SaveChangesAsync(cancellationToken);
    }
    private static bool DebeEnviar(WhatsAppConfiguracion c, string tipo) => tipo switch
    {
        "Confirmacion" => c.EnviarConfirmaciones,
        "Recordatorio24Horas" => c.EnviarRecordatorio24Horas,
        "Recordatorio2Horas" => c.EnviarRecordatorio2Horas,
        "Cancelacion" => c.EnviarCancelaciones,
        "Reprogramacion" => c.EnviarReprogramaciones,
        _ => c.PermitirMensajesPersonalizados
    };
    private static string NormalizarTelefono(string telefono) => new string(telefono.Where(char.IsDigit).ToArray());
    private static string ReemplazarVariables(string mensaje, Cita cita) => mensaje.Replace("{Cliente}", cita.Cliente?.Nombre ?? string.Empty).Replace("{Barbero}", cita.Barbero?.Usuario?.Nombre ?? string.Empty).Replace("{Servicio}", cita.Servicio?.Nombre ?? string.Empty).Replace("{Fecha}", cita.FechaHora.ToString("dd/MM/yyyy")).Replace("{Hora}", cita.FechaHora.ToString("hh:mm tt")).Replace("{Barberia}", cita.Tenant?.NombreNegocio ?? string.Empty);
    private static string ResumenError(string contenido) => contenido.Length > 2000 ? contenido[..2000] : contenido;
}
