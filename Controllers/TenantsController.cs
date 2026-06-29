using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SaaSBarberia.Data;
using SaaSBarberia.Models;
using SaaSBarberia.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;

namespace SaaSBarberia.Controllers
{
    public class TenantsController : Controller
    {
        private readonly TenantService _tenantService;
        private readonly BarberSaaSDbContext _context;

        public TenantsController(TenantService tenantService, BarberSaaSDbContext context)
        {
            _tenantService = tenantService;
            _context = context;
        }

        public async Task<IActionResult> Index()
        {
            var lista = await _tenantService.ObtenerTodosAsync();
            return View(lista);
        }

        public async Task<IActionResult> Details(int? id)
        {
            if (id == null) return NotFound();
            var tenant = await _tenantService.ObtenerPorIdAsync(id.Value);
            if (tenant == null) return NotFound();
            return View(tenant);
        }

        public IActionResult Create()
        {
            // Inicializamos la barbería con sus 7 horarios por defecto para la vista
            var tenant = new Tenant();
            var horarios = new List<HorarioLaboral>();

            for (int i = 0; i <= 6; i++)
            {
                horarios.Add(new HorarioLaboral
                {
                    DiaSemana = i,
                    EstaCerrado = (i == 0), // Domingo cerrado por defecto
                    HoraInicio = new TimeSpan(8, 0, 0),
                    HoraFin = new TimeSpan(12, 0, 0),
                    HoraInicio2 = new TimeSpan(14, 0, 0),
                    HoraFin2 = new TimeSpan(19, 0, 0)
                });
            }

            ViewBag.HorariosIniciales = horarios;
            return View(tenant);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        // 🔥 CORREGIDO: Se añade 'TiempoEntreCitas' al Bind para mapear el campo de la optimización de tiempos
        public async Task<IActionResult> Create([Bind("Id,NombreNegocio,Subdominio,PlanSuscripcion,Activo,FechaRegistro,ColorPrimario,ColorSecundario,EstiloSidebar,TieneModuloHerramientas,TiempoEntreCitas")] Tenant tenant, List<HorarioLaboral> horarios)
        {
            if (ModelState.IsValid)
            {
                tenant.FechaRegistro ??= DateTime.Now;

                // 1. Guardamos la barbería para generar el Id
                await _tenantService.GuardarAsync(tenant);

                // 2. Le asignamos el TenantId a los horarios modificados en la vista y los agregamos
                if (horarios != null && horarios.Any())
                {
                    foreach (var horario in horarios)
                    {
                        horario.TenantId = tenant.Id;
                        _context.HorariosLaborales.Add(horario);
                    }
                    await _context.SaveChangesAsync();
                }

                return RedirectToAction(nameof(Index));
            }

            ViewBag.HorariosIniciales = horarios;
            return View(tenant);
        }

        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();
            var tenant = await _tenantService.ObtenerPorIdAsync(id.Value);
            if (tenant == null) return NotFound();
            return View(tenant);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        // 🔥 CORREGIDO: También añadimos 'TiempoEntreCitas' al Bind del Edit por si cambian la configuración después
        public async Task<IActionResult> Edit(int id, [Bind("Id,NombreNegocio,Subdominio,PlanSuscripcion,Activo,FechaRegistro,CerradoPorEmergencia,FechaUltimoCierre,ColorPrimario,ColorSecundario,EstiloSidebar,TieneModuloHerramientas,TiempoEntreCitas")] Tenant tenant)
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

        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null) return NotFound();
            var tenant = await _tenantService.ObtenerPorIdAsync(id.Value);
            if (tenant == null) return NotFound();
            return View(tenant);
        }

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            await _tenantService.EliminarAsync(id);
            return RedirectToAction(nameof(Index));
        }

        [Authorize(Roles = "AdminBarberia")]
        public async Task<IActionResult> DashboardSede()
        {
            var usuarioIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(usuarioIdStr)) return RedirectToAction("Login", "Account");
            int usuarioId = int.Parse(usuarioIdStr);

            var usuarioDb = await _context.Usuarios.FindAsync(usuarioId);
            if (usuarioDb == null || usuarioDb.TenantId == null) return NotFound("No tienes una sede asignada, marica.");

            int miTenantId = usuarioDb.TenantId.Value;
            var tenant = await _context.Tenants.FindAsync(miTenantId);

            if (tenant.CerradoPorEmergencia && tenant.FechaUltimoCierre.HasValue && tenant.FechaUltimoCierre.Value.Date < DateTime.Today)
            {
                tenant.CerradoPorEmergencia = false;
                tenant.FechaUltimoCierre = null;
                _context.Update(tenant);
                await _context.SaveChangesAsync();
            }

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

        [Authorize(Roles = "SuperAdmin")]
        public async Task<IActionResult> SuperDashboard()
        {
            var todosLosTenants = await _context.Tenants.IgnoreQueryFilters().ToListAsync();
            var todosLosUsuarios = await _context.Usuarios.IgnoreQueryFilters().CountAsync();

            decimal totalRecaudado = todosLosTenants.Where(t => t.Activo == true).Sum(t =>
                t.PlanSuscripcion == "Oro" ? 150000m :
                t.PlanSuscripcion == "Plata" ? 100000m : 50000m
            );

            var distribucionPlanes = todosLosTenants
                .GroupBy(t => t.PlanSuscripcion)
                .ToDictionary(g => g.Key ?? "Sin Plan", g => g.Count());

            var viewModel = new SuperAdminDashboardViewModel
            {
                TotalTenants = todosLosTenants.Count,
                TenantsActivos = todosLosTenants.Count(t => t.Activo == true),
                TotalUsuarios = todosLosUsuarios,
                RecaudacionEstimada = totalRecaudado,
                UltimosTenants = todosLosTenants.OrderByDescending(t => t.FechaRegistro).Take(5).ToList(),
                DatosPlanes = distribucionPlanes
            };

            return View(viewModel);
        }
    }
}