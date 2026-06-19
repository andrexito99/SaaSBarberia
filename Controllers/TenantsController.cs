using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SaaSBarberia.Data; // ⚠️ Asegúrate de que apunte a tu carpeta Data
using SaaSBarberia.Models;
using SaaSBarberia.Services;
using System;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;

public class TenantsController : Controller
{
    private readonly TenantService _tenantService;
    private readonly BarberSaaSDbContext _context; // ⚡ Inyectamos el contexto directo para consultas avanzadas

    public TenantsController(TenantService tenantService, BarberSaaSDbContext context)
    {
        _tenantService = tenantService;
        _context = context;
    }

    // GET: Tenants
    public async Task<IActionResult> Index()
    {
        var lista = await _tenantService.ObtenerTodosAsync();
        return View(lista);
    }

    // GET: Tenants/Details/5
    public async Task<IActionResult> Details(int? id)
    {
        if (id == null) return NotFound();

        var tenant = await _tenantService.ObtenerPorIdAsync(id.Value);
        if (tenant == null) return NotFound();

        return View(tenant);
    }

    // GET: Tenants/Create
    public IActionResult Create()
    {
        return View();
    }

    // POST: Tenants/Create
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create([Bind("Id,NombreNegocio,Subdominio,PlanSuscripcion,Activo,FechaRegistro,ColorPrimario,ColorSecundario,EstiloSidebar")] Tenant tenant)
    {
        if (ModelState.IsValid)
        {
            tenant.FechaRegistro ??= DateTime.Now;
            await _tenantService.GuardarAsync(tenant);
            return RedirectToAction(nameof(Index));
        }
        return View(tenant);
    }

    // GET: Tenants/Edit/5
    public async Task<IActionResult> Edit(int? id)
    {
        if (id == null) return NotFound();

        var tenant = await _tenantService.ObtenerPorIdAsync(id.Value);
        if (tenant == null) return NotFound();

        return View(tenant);
    }

    // POST: Tenants/Edit/5
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, [Bind("Id,NombreNegocio,Subdominio,PlanSuscripcion,Activo,FechaRegistro,CerradoPorEmergencia,FechaUltimoCierre,ColorPrimario,ColorSecundario,EstiloSidebar")] Tenant tenant)
    {
        if (id != tenant.Id) return NotFound();

        if (ModelState.IsValid)
        {
            try
            {
                await _tenantService.GuardarAsync(tenant);
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!_tenantService.Existe(tenant.Id)) return NotFound();
                else throw;
            }
            return RedirectToAction(nameof(Index));
        }
        return View(tenant);
    }

    // GET: Tenants/Delete/5
    public async Task<IActionResult> Delete(int? id)
    {
        if (id == null) return NotFound();

        var tenant = await _tenantService.ObtenerPorIdAsync(id.Value);
        if (tenant == null) return NotFound();

        return View(tenant);
    }

    // POST: Tenants/Delete/5
    [HttpPost, ActionName("Delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(int id)
    {
        await _tenantService.EliminarAsync(id);
        return RedirectToAction(nameof(Index));
    }


    // =======================================================
    // 🚨 MÉTODOS DE CONTINGENCIA: CIERRE DE EMERGENCIA (SaaS)
    // =======================================================

    // GET: Tenants/DashboardSede
    [Authorize(Roles = "AdminBarberia")]
    public async Task<IActionResult> DashboardSede()
    {
        // 1. Conseguir el TenantId del Administrador logueado
        var usuarioIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(usuarioIdStr)) return RedirectToAction("Login", "Account");
        int usuarioId = int.Parse(usuarioIdStr);

        var usuarioDb = await _context.Usuarios.FindAsync(usuarioId);
        if (usuarioDb == null || usuarioDb.TenantId == null) return NotFound("No tienes una sede asignada, marica.");

        int miTenantId = usuarioDb.TenantId.Value;
        var tenant = await _context.Tenants.FindAsync(miTenantId);

        // 2. Control inteligente: Si el cierre fue ayer o antes, auto-reabrir la agenda hoy
        if (tenant.CerradoPorEmergencia && tenant.FechaUltimoCierre.HasValue && tenant.FechaUltimoCierre.Value.Date < DateTime.Today)
        {
            tenant.CerradoPorEmergencia = false;
            tenant.FechaUltimoCierre = null;
            _context.Update(tenant);
            await _context.SaveChangesAsync();
        }

        // 3. Buscar las citas puestas para HOY que se van a joder por el cierre
        var citasAfectadasHoy = await _context.Citas
            .Where(c => c.TenantId == miTenantId
                     && c.FechaHora.Date == DateTime.Today
                     && c.Estado != "Cancelada")
            .Include(c => c.Cliente)
            .Include(c => c.Barbero.Usuario)
            .OrderBy(c => c.FechaHora)
            .ToListAsync();

        ViewBag.CitasAfectadas = citasAfectadasHoy;

        return View(tenant);
    }

    // POST: Tenants/CambiarEstadoEmergencia
    [HttpPost]
    [Authorize(Roles = "AdminBarberia")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CambiarEstadoEmergencia(int id, bool estadoCierre)
    {
        var tenant = await _context.Tenants.FindAsync(id);
        if (tenant == null) return NotFound();

        tenant.CerradoPorEmergencia = estadoCierre;
        tenant.FechaUltimoCierre = estadoCierre ? DateTime.Now : null;

        _context.Update(tenant);
        await _context.SaveChangesAsync();

        return RedirectToAction(nameof(DashboardSede));
    }
}