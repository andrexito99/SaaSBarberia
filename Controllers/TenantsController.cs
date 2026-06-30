using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SaaSBarberia.Data;
using SaaSBarberia.Models;
using SaaSBarberia.Services;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;

namespace SaaSBarberia.Controllers
{
    public class TenantsController : Controller
    {
        private readonly TenantService _tenantService;
        private readonly BarberSaaSDbContext _context;
        private readonly IWebHostEnvironment _webHostEnvironment;

        public TenantsController(TenantService tenantService, BarberSaaSDbContext context, IWebHostEnvironment webHostEnvironment)
        {
            _tenantService = tenantService;
            _context = context;
            _webHostEnvironment = webHostEnvironment;
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
            var tenant = new Tenant();
            var horarios = new List<HorarioLaboral>();

            for (int i = 0; i <= 6; i++)
            {
                horarios.Add(new HorarioLaboral
                {
                    DiaSemana = i,
                    EstaCerrado = (i == 0),
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
        // 🔥 CORREGIDO: Cambiado 'LogoUrl' por 'RutaLogo' en el Bind para enganchar con la base de datos
        public async Task<IActionResult> Create([Bind("Id,NombreNegocio,Subdominio,PlanSuscripcion,Activo,FechaRegistro,ColorPrimario,ColorSecundario,EstiloSidebar,TieneModuloHerramientas,TiempoEntreCitas,RutaLogo")] Tenant tenant, List<HorarioLaboral> horarios, IFormFile? logoArchivo)
        {
            if (ModelState.IsValid)
            {
                tenant.FechaRegistro ??= DateTime.Now;

                if (logoArchivo != null && logoArchivo.Length > 0)
                {
                    string carpetaSubidas = Path.Combine(_webHostEnvironment.WebRootPath, "uploads", "tenants");
                    if (!Directory.Exists(carpetaSubidas)) Directory.CreateDirectory(carpetaSubidas);

                    string nombreUnico = Guid.NewGuid().ToString() + Path.GetExtension(logoArchivo.FileName);
                    string rutaCompleta = Path.Combine(carpetaSubidas, nombreUnico);

                    using (var stream = new FileStream(rutaCompleta, FileMode.Create))
                    {
                        await logoArchivo.CopyToAsync(stream);
                    }
                    tenant.RutaLogo = "/uploads/tenants/" + nombreUnico;
                }

                await _tenantService.GuardarAsync(tenant);

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
        // 🔥 CORREGIDO: Cambiado 'LogoUrl' por 'RutaLogo' en el Bind para capturar el input hidden de la vista
        public async Task<IActionResult> Edit(int id, [Bind("Id,NombreNegocio,Subdominio,PlanSuscripcion,Activo,FechaRegistro,CerradoPorEmergencia,FechaUltimoCierre,ColorPrimario,ColorSecundario,EstiloSidebar,TieneModuloHerramientas,TiempoEntreCitas,RutaLogo")] Tenant tenant, IFormFile? logoArchivo)
        {
            if (id != tenant.Id) return NotFound();

            if (ModelState.IsValid)
            {
                try
                {
                    if (logoArchivo != null && logoArchivo.Length > 0)
                    {
                        // 🗑️ Buscar ruta del logo viejo usando AsNoTracking de forma directa para romper el candado de memoria
                        var tenantExistente = await _context.Tenants.AsNoTracking().FirstOrDefaultAsync(t => t.Id == id);
                        if (tenantExistente != null && !string.IsNullOrEmpty(tenantExistente.RutaLogo))
                        {
                            string rutaVieja = Path.Combine(_webHostEnvironment.WebRootPath, tenantExistente.RutaLogo.TrimStart('/'));
                            if (System.IO.File.Exists(rutaVieja)) System.IO.File.Delete(rutaVieja);
                        }

                        // 💾 Almacenar nuevo archivo físico
                        string carpetaSubidas = Path.Combine(_webHostEnvironment.WebRootPath, "uploads", "tenants");
                        if (!Directory.Exists(carpetaSubidas)) Directory.CreateDirectory(carpetaSubidas);

                        string nombreUnico = Guid.NewGuid().ToString() + Path.GetExtension(logoArchivo.FileName);
                        string rutaCompleta = Path.Combine(carpetaSubidas, nombreUnico);

                        using (var stream = new FileStream(rutaCompleta, FileMode.Create))
                        {
                            await logoArchivo.CopyToAsync(stream);
                        }
                        tenant.RutaLogo = "/uploads/tenants/" + nombreUnico;
                    }
                    // Si 'logoArchivo' es nulo, 'tenant.RutaLogo' ya retiene el valor previo gracias al input hidden y al Bind corregido.

                    // 🔥 Actualización directa usando el contexto para evitar fallas de tracking de servicios acoplados
                    _context.Entry(tenant).State = EntityState.Modified;
                    await _context.SaveChangesAsync();
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
            // 🔥 CORREGIDO: Consulta limpia con AsNoTracking para evitar bloqueos antes del borrado
            var tenant = await _context.Tenants.AsNoTracking().FirstOrDefaultAsync(t => t.Id == id);
            if (tenant != null && !string.IsNullOrEmpty(tenant.RutaLogo))
            {
                string rutaArchivo = Path.Combine(_webHostEnvironment.WebRootPath, tenant.RutaLogo.TrimStart('/'));
                if (System.IO.File.Exists(rutaArchivo)) System.IO.File.Delete(rutaArchivo);
            }

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
                .Where(c => c.TenantId == miTenantId && c.FechaHora.Date == DateTime.Today && c.Estado != "Cancelada")
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