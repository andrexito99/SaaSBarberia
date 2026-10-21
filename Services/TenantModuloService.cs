using Microsoft.EntityFrameworkCore;
using SaaSBarberia.Data;
using SaaSBarberia.Models;

namespace SaaSBarberia.Services;

public static class ModulosTenant
{
    public const string Agenda = "Agenda";
    public const string Productos = "Productos";
    public const string Inventario = "Inventario";
    public const string Herramientas = "Herramientas";
    public const string Caja = "Caja";
    public const string Reportes = "Reportes";
    public const string Notificaciones = "Notificaciones";
    public const string WhatsApp = "WhatsApp";

    public static readonly string[] Todos = [Agenda, Productos, Inventario, Herramientas, Caja, Reportes, Notificaciones, WhatsApp];
}

public sealed class TenantModuloService
{
    private readonly BarberSaaSDbContext _context;
    public TenantModuloService(BarberSaaSDbContext context) => _context = context;

    public Task<bool> EstaHabilitadoAsync(int tenantId, string codigo, CancellationToken cancellationToken = default) =>
        _context.TenantModulos.IgnoreQueryFilters().AnyAsync(m => m.TenantId == tenantId && m.Codigo == codigo && m.Habilitado, cancellationToken);

    public Task<List<TenantModulo>> ObtenerAsync(int tenantId) =>
        _context.TenantModulos.IgnoreQueryFilters().Where(m => m.TenantId == tenantId).OrderBy(m => m.Codigo).ToListAsync();

    public async Task SincronizarAsync(int tenantId, IEnumerable<string> habilitados)
    {
        var seleccion = habilitados.Intersect(ModulosTenant.Todos, StringComparer.OrdinalIgnoreCase).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var actuales = await _context.TenantModulos.IgnoreQueryFilters().Where(m => m.TenantId == tenantId).ToListAsync();
        foreach (var codigo in ModulosTenant.Todos)
        {
            var actual = actuales.FirstOrDefault(m => m.Codigo == codigo);
            if (actual == null)
                _context.TenantModulos.Add(new TenantModulo { TenantId = tenantId, Codigo = codigo, Habilitado = seleccion.Contains(codigo), FechaActualizacion = DateTime.UtcNow });
            else { actual.Habilitado = seleccion.Contains(codigo); actual.FechaActualizacion = DateTime.UtcNow; }
        }
        await _context.SaveChangesAsync();
    }
}
