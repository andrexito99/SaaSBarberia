using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using SaaSBarberia.Data;
using System.Linq;
using System.Threading.Tasks;

namespace SaaSBarberia.Controllers;

[Authorize(Roles = "SuperAdmin")] // 🔒 Exclusivo para el SuperAdmin
public class AuditoriaController : Controller
{
    private readonly BarberSaaSDbContext _context;

    public AuditoriaController(BarberSaaSDbContext context)
    {
        _context = context;
    }

    // 🔍 INDEX CON FILTROS ENFOCADOS EN TU MODELO REAL
    public async Task<IActionResult> Index(int? tenantId, string entidad, string accion)
    {
        // 1. Cargamos datos complementarios para los combos de filtrado
        var tenants = await _context.Tenants.ToListAsync();
        ViewBag.TenantId = new SelectList(tenants, "Id", "NombreNegocio", tenantId);

        var entidadesExistentes = await _context.Auditorias.Select(a => a.Entidad).Distinct().ToListAsync();
        var accionesExistentes = await _context.Auditorias.Select(a => a.Accion).Distinct().ToListAsync();

        ViewBag.Entidades = new SelectList(entidadesExistentes, entidad);
        ViewBag.Acciones = new SelectList(accionesExistentes, accion);

        // 2. Traemos las listas de apoyo para mapear los nombres en la vista (Solución anti-CS1061)
        ViewBag.ListaUsuarios = await _context.Usuarios.ToDictionaryAsync(u => u.Id, u => u.Nombre);
        ViewBag.ListaTenants = tenants.ToDictionary(t => t.Id, t => t.NombreNegocio);

        // 3. Consulta base sobre tu modelo
        var query = _context.Auditorias.AsQueryable();

        // 4. Filtros
        if (tenantId.HasValue && tenantId.Value > 0)
        {
            query = query.Where(a => a.TenantId == tenantId.Value);
        }

        if (!string.IsNullOrEmpty(entidad))
        {
            query = query.Where(a => a.Entidad == entidad);
        }

        if (!string.IsNullOrEmpty(accion))
        {
            query = query.Where(a => a.Accion == accion);
        }

        // 5. Ordenamos por tu campo "Fecha" (¡Lo más nuevo arriba!)
        var historial = await query.OrderByDescending(a => a.Fecha).Take(500).ToListAsync();

        return View(historial);
    }
}