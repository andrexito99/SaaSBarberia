using Microsoft.EntityFrameworkCore;
using SaaSBarberia.Data;
using SaaSBarberia.Models;

namespace SaaSBarberia.Services;

public interface IEventosSistemaService { Task PublicarAsync(EventoSistema eventoSistema); }

public sealed class EventosSistemaService : IEventosSistemaService
{
    private readonly BarberSaaSDbContext _context;
    private readonly INotificacionService _notificaciones;
    public EventosSistemaService(BarberSaaSDbContext context, INotificacionService notificaciones) => (_context, _notificaciones) = (context, notificaciones);

    public async Task PublicarAsync(EventoSistema eventoSistema)
    {
        if (eventoSistema.TenantId <= 0) return;
        var destinos = eventoSistema.UsuariosDestinoIds?.Distinct().ToArray();
        if (destinos == null || destinos.Length == 0)
            destinos = await _context.Usuarios.IgnoreQueryFilters().Where(u => u.TenantId == eventoSistema.TenantId && u.Activo == true && (u.Role.NombreRole == "Administrador" || u.Role.NombreRole == "SuperAdmin")).Select(u => u.Id).ToArrayAsync();

        var presentacion = ObtenerPresentacion(eventoSistema.Tipo);
        foreach (var usuarioId in destinos)
            await _notificaciones.CrearNotificacionAsync(new Notificacion { TenantId = eventoSistema.TenantId, UsuarioDestinoId = usuarioId, Titulo = eventoSistema.Titulo, Mensaje = eventoSistema.Mensaje, Tipo = presentacion.Tipo, Icono = presentacion.Icono, Color = presentacion.Color, UrlDestino = eventoSistema.UrlDestino });
    }

    private static (string Tipo, string Icono, string Color) ObtenerPresentacion(TipoEventoSistema tipo) => tipo switch
    {
        TipoEventoSistema.CitaCancelada => ("Advertencia", "bi-calendar-x", "warning"),
        TipoEventoSistema.ProductoConPocoInventario => ("Advertencia", "bi-box-seam", "warning"),
        TipoEventoSistema.CajaCerrada or TipoEventoSistema.CambioImportanteSistema => ("Alerta", "bi-exclamation-triangle", "danger"),
        _ => ("Informacion", "bi-bell", "primary")
    };
}
