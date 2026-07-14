using Microsoft.EntityFrameworkCore;
using SaaSBarberia.Data;
using SaaSBarberia.Models;

namespace SaaSBarberia.Services;

public class BarberoAusenciaService
{
    private readonly BarberSaaSDbContext _context;
    private readonly AgendaInteligenteService _agenda;
    private readonly IAuditoriaService _auditoria;
    private readonly IEventosSistemaService _eventos;
    public BarberoAusenciaService(BarberSaaSDbContext context, AgendaInteligenteService agenda, IAuditoriaService auditoria, IEventosSistemaService eventos) => (_context, _agenda, _auditoria, _eventos) = (context, agenda, auditoria, eventos);

    public Task<List<BarberoAusencia>> ObtenerAsync(int tenantId) => _context.BarberoAusencias.Include(a => a.Barbero).ThenInclude(b => b.Usuario).Where(a => a.TenantId == tenantId).OrderByDescending(a => a.FechaInicio).ToListAsync();
    public Task<BarberoAusencia?> ObtenerPorIdAsync(int id, int tenantId) => _context.BarberoAusencias.Include(a => a.Barbero).ThenInclude(b => b.Usuario).FirstOrDefaultAsync(a => a.Id == id && a.TenantId == tenantId);
    public Task<List<Barbero>> ObtenerBarberosAsync(int tenantId) => _context.Barberos.Include(b => b.Usuario).Where(b => b.TenantId == tenantId && b.Usuario.Activo == true).OrderBy(b => b.Usuario.Nombre).ToListAsync();

    public async Task CrearAsync(BarberoAusencia ausencia, int tenantId, int usuarioId, string ip)
    {
        await ValidarAsync(ausencia, tenantId);
        ausencia.TenantId = tenantId; ausencia.CreadoPorUsuarioId = usuarioId.ToString(); ausencia.Activo = true; ausencia.FechaRegistro = DateTime.Now;
        _context.BarberoAusencias.Add(ausencia); await _context.SaveChangesAsync();
        await _auditoria.RegistrarAsync(tenantId, usuarioId, "BarberoAusencia", ausencia.Id.ToString(), "Crear", $"Se registró ausencia {ausencia.Tipo} para el barbero {ausencia.BarberoId}, desde {ausencia.FechaInicio:dd/MM/yyyy HH:mm} hasta {ausencia.FechaFin:dd/MM/yyyy HH:mm}.", ip);
        var tipo = ausencia.Tipo == TipoAusencia.Vacaciones ? TipoEventoSistema.Vacaciones : TipoEventoSistema.NuevaAusencia;
        await _eventos.PublicarAsync(new EventoSistema(tipo, tenantId, "Ausencia registrada", $"Se bloqueó la agenda del barbero para {ausencia.Tipo}.", "/BarberoAusencias/Index"));
    }

    public async Task ActualizarAsync(BarberoAusencia cambios, int tenantId, int usuarioId, string ip)
    {
        await ValidarAsync(cambios, tenantId);
        var actual = await _context.BarberoAusencias.FirstOrDefaultAsync(a => a.Id == cambios.Id && a.TenantId == tenantId) ?? throw new KeyNotFoundException();
        actual.BarberoId = cambios.BarberoId; actual.FechaInicio = cambios.FechaInicio; actual.FechaFin = cambios.FechaFin; actual.Tipo = cambios.Tipo; actual.Motivo = cambios.Motivo; actual.Observaciones = cambios.Observaciones; actual.Activo = cambios.Activo;
        await _context.SaveChangesAsync();
        await _auditoria.RegistrarAsync(tenantId, usuarioId, "BarberoAusencia", actual.Id.ToString(), "Editar", "Se actualizó una ausencia de barbero.", ip);
    }

    public async Task EliminarAsync(int id, int tenantId, int usuarioId, string ip)
    {
        var actual = await _context.BarberoAusencias.FirstOrDefaultAsync(a => a.Id == id && a.TenantId == tenantId);
        if (actual == null) return; _context.BarberoAusencias.Remove(actual); await _context.SaveChangesAsync();
        await _auditoria.RegistrarAsync(tenantId, usuarioId, "BarberoAusencia", id.ToString(), "Eliminar", "Se eliminó una ausencia de barbero.", ip);
    }

    private async Task ValidarAsync(BarberoAusencia ausencia, int tenantId)
    {
        if (ausencia.FechaInicio >= ausencia.FechaFin) throw new InvalidOperationException("La fecha de inicio debe ser anterior a la fecha de finalización.");
        if (!await _context.Barberos.AnyAsync(b => b.Id == ausencia.BarberoId && b.TenantId == tenantId)) throw new UnauthorizedAccessException("El barbero no pertenece a la barbería actual.");
        if (ausencia.Activo && await _agenda.TieneCitasEnRangoAsync(tenantId, ausencia.BarberoId, ausencia.FechaInicio, ausencia.FechaFin)) throw new InvalidOperationException("El barbero tiene citas activas dentro del rango indicado.");
    }
}
