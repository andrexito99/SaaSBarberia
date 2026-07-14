using Microsoft.EntityFrameworkCore;
using SaaSBarberia.Data;
using SaaSBarberia.Models;

namespace SaaSBarberia.Services;

public interface INotificacionService
{
    Task CrearNotificacionAsync(Notificacion notificacion);
    Task<IReadOnlyList<Notificacion>> ObtenerNotificacionesUsuarioAsync(int tenantId, int usuarioId, int limite = 20);
    Task<IReadOnlyList<Notificacion>> ObtenerNoLeidasAsync(int tenantId, int usuarioId, int limite = 20);
    Task<int> ContarNoLeidasAsync(int tenantId, int usuarioId);
    Task<bool> MarcarComoLeidaAsync(int id, int tenantId, int usuarioId);
    Task MarcarTodasComoLeidasAsync(int tenantId, int usuarioId);
    Task<bool> EliminarAsync(int id, int tenantId, int usuarioId);
    Task EliminarTodasLeidasAsync(int tenantId, int usuarioId);
}

public class NotificacionService : INotificacionService
{
    private readonly BarberSaaSDbContext _context;
    public NotificacionService(BarberSaaSDbContext context) => _context = context;

    public async Task CrearNotificacionAsync(Notificacion notificacion)
    {
        var usuarioValido = await _context.Usuarios.IgnoreQueryFilters()
            .AnyAsync(u => u.Id == notificacion.UsuarioDestinoId && u.TenantId == notificacion.TenantId && u.Activo == true);
        if (!usuarioValido) return;
        notificacion.Id = 0;
        notificacion.Leida = false;
        notificacion.FechaCreacion = DateTime.Now;
        _context.Notificaciones.Add(notificacion);
        await _context.SaveChangesAsync();
    }

    public async Task<IReadOnlyList<Notificacion>> ObtenerNotificacionesUsuarioAsync(int tenantId, int usuarioId, int limite = 20) =>
        await ConsultaUsuario(tenantId, usuarioId).OrderByDescending(n => n.FechaCreacion).Take(NormalizarLimite(limite)).ToListAsync();

    public async Task<IReadOnlyList<Notificacion>> ObtenerNoLeidasAsync(int tenantId, int usuarioId, int limite = 20) =>
        await ConsultaUsuario(tenantId, usuarioId).Where(n => !n.Leida).OrderByDescending(n => n.FechaCreacion).Take(NormalizarLimite(limite)).ToListAsync();

    public Task<int> ContarNoLeidasAsync(int tenantId, int usuarioId) => ConsultaUsuario(tenantId, usuarioId).CountAsync(n => !n.Leida);

    public async Task<bool> MarcarComoLeidaAsync(int id, int tenantId, int usuarioId)
    {
        var notificacion = await ConsultaUsuario(tenantId, usuarioId).FirstOrDefaultAsync(n => n.Id == id);
        if (notificacion == null) return false;
        if (!notificacion.Leida) { notificacion.Leida = true; notificacion.FechaLectura = DateTime.Now; await _context.SaveChangesAsync(); }
        return true;
    }

    public async Task MarcarTodasComoLeidasAsync(int tenantId, int usuarioId)
    {
        var pendientes = await ConsultaUsuario(tenantId, usuarioId).Where(n => !n.Leida).ToListAsync();
        if (pendientes.Count == 0) return;
        foreach (var item in pendientes) { item.Leida = true; item.FechaLectura = DateTime.Now; }
        await _context.SaveChangesAsync();
    }

    public async Task<bool> EliminarAsync(int id, int tenantId, int usuarioId)
    {
        var notificacion = await ConsultaUsuario(tenantId, usuarioId).FirstOrDefaultAsync(n => n.Id == id);
        if (notificacion == null) return false;
        _context.Notificaciones.Remove(notificacion); await _context.SaveChangesAsync(); return true;
    }

    public async Task EliminarTodasLeidasAsync(int tenantId, int usuarioId)
    {
        var leidas = await ConsultaUsuario(tenantId, usuarioId).Where(n => n.Leida).ToListAsync();
        if (leidas.Count == 0) return;
        _context.Notificaciones.RemoveRange(leidas); await _context.SaveChangesAsync();
    }

    private IQueryable<Notificacion> ConsultaUsuario(int tenantId, int usuarioId) => _context.Notificaciones.Where(n => n.TenantId == tenantId && n.UsuarioDestinoId == usuarioId);
    private static int NormalizarLimite(int limite) => Math.Clamp(limite, 1, 100);
}
