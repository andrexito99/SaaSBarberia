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
        private readonly HorarioLaboralService _horarioService; // 🔥 Inyectado
        private readonly BarberSaaSDbContext _context;
        private readonly IWebHostEnvironment _webHostEnvironment;

        public TenantsController(TenantService tenantService, HorarioLaboralService horarioService, BarberSaaSDbContext context, IWebHostEnvironment webHostEnvironment)
        {
            _tenantService = tenantService;
            _horarioService = horarioService;
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

        // GET: Tenants/Create
        public IActionResult Create()
        {
            var tenant = new Tenant();
            return View(tenant);
        }

        // POST: Tenants/Create (Paso 1: Guarda solo Tenant)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("Id,NombreNegocio,Subdominio,PlanSuscripcion,Activo,FechaRegistro,ColorPrimario,ColorSecundario,EstiloSidebar,TieneModuloHerramientas,TiempoEntreCitas,RutaLogo")] Tenant tenant, IFormFile? logoArchivo)
        {
            // Forzamos la limpieza de la navegación si viene jodiendo el modelo
            ModelState.Remove("HorariosLaborales");

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

                // Guardamos el Tenant base
                await _tenantService.GuardarAsync(tenant);

                // 🔥 Redireccionamos al Paso 2 pasando el ID recién creado de la DB
                await _horarioService.ObtenerHorarioSemanaAsync(tenant.Id);
                return RedirectToAction(nameof(Index));
            }

            return View(tenant);
        }

        // GET: Tenants/ConfigurarHorarios?tenantId=5 (Paso 2)
        public IActionResult ConfigurarHorarios(int tenantId)
        {
            if (tenantId <= 0) return NotFound("ID de barbería inválido.");

            ViewBag.TenantId = tenantId;

            // Inicializamos la lista de 7 días con los valores por defecto
            var horarios = new List<HorarioLaboral>();
            for (int i = 0; i <= 6; i++)
            {
                horarios.Add(new HorarioLaboral
                {
                    TenantId = tenantId,
                    DiaSemana = i,
                    EstaCerrado = (i == 0),
                    HoraInicio = new TimeSpan(8, 0, 0),
                    HoraFin = new TimeSpan(12, 0, 0),
                    HoraInicio2 = new TimeSpan(14, 0, 0),
                    HoraFin2 = new TimeSpan(19, 0, 0)
                });
            }

            return View(horarios);
        }

        [Authorize(Roles = "SuperAdmin")]
        public async Task<IActionResult> EditarHorarios(int tenantId)
        {
            if (tenantId <= 0) return NotFound("ID de barbería inválido.");

            var tenant = await _context.Tenants
                .IgnoreQueryFilters()
                .AsNoTracking()
                .FirstOrDefaultAsync(t => t.Id == tenantId);

            if (tenant == null) return NotFound("Barbería no encontrada.");

            var horarios = await _horarioService.ObtenerHorarioSemanaAsync(tenantId);

            ViewBag.TenantId = tenantId;
            ViewBag.NombreNegocio = tenant.NombreNegocio;
            ViewBag.SelectorJornada = horarios.Any(h => h.HoraInicio2.HasValue && h.HoraFin2.HasValue) ? "2" : "1";

            return View(horarios);
        }

        [HttpPost]
        [Authorize(Roles = "SuperAdmin")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditarHorarios(int tenantId, List<HorarioLaboral> horarios, string selectorJornada)
        {
            if (tenantId <= 0) return NotFound("ID de barbería inválido.");

            var tenant = await _context.Tenants
                .IgnoreQueryFilters()
                .AsNoTracking()
                .FirstOrDefaultAsync(t => t.Id == tenantId);

            if (tenant == null) return NotFound("Barbería no encontrada.");

            LimpiarModelStateHorarios(horarios);

            if (ModelState.IsValid && horarios != null)
            {
                try
                {
                    await _horarioService.GuardarHorariosSemanaActualizandoAsync(tenantId, horarios, selectorJornada);
                    TempData["Mensaje"] = "Horarios actualizados correctamente.";
                    return RedirectToAction(nameof(Index));
                }
                catch (Exception ex)
                {
                    ModelState.AddModelError("", ex.Message);
                }
            }

            ViewBag.TenantId = tenantId;
            ViewBag.NombreNegocio = tenant.NombreNegocio;
            ViewBag.SelectorJornada = selectorJornada;
            return View(horarios ?? new List<HorarioLaboral>());
        }

        // POST: Tenants/ConfigurarHorarios
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ConfigurarHorarios(int tenantId, List<HorarioLaboral> horarios, string selectorJornada)
        {
            LimpiarModelStateHorarios(horarios);

            if (ModelState.IsValid && horarios != null)
            {
                await _horarioService.GuardarHorariosSemanaActualizandoAsync(tenantId, horarios, selectorJornada);
                return RedirectToAction(nameof(Index));
            }

            ViewBag.TenantId = tenantId;
            return View(horarios);

            ModelState.Remove("Tenant");

            if (horarios != null)
            {
                for (int i = 0; i < horarios.Count; i++)
                {
                    horarios[i].TenantId = tenantId;

                    if (horarios[i].EstaCerrado)
                    {
                        horarios[i].HoraInicio = null;
                        horarios[i].HoraFin = null;
                        horarios[i].HoraInicio2 = null;
                        horarios[i].HoraFin2 = null;
                    }
                    else
                    {
                        // 🔥 INTELIGENCIA COGNITIVA ANTI-BRUTOS:
                        // Recolectamos todas las horas enviadas en la fila, las ordenamos cronológicamente
                        var horasEnviadas = new List<TimeSpan>();
                        if (horarios[i].HoraInicio.HasValue) horasEnviadas.Add(horarios[i].HoraInicio.Value);
                        if (horarios[i].HoraFin.HasValue) horasEnviadas.Add(horarios[i].HoraFin.Value);

                        if (selectorJornada == "2")
                        {
                            if (horarios[i].HoraInicio2.HasValue) horasEnviadas.Add(horarios[i].HoraInicio2.Value);
                            if (horarios[i].HoraFin2.HasValue) horasEnviadas.Add(horarios[i].HoraFin2.Value);
                        }

                        var horasOrdenadas = horasEnviadas.OrderBy(h => h).ToList();

                        // Re-asignamos de forma coherente de menor a mayor
                        if (horasOrdenadas.Count >= 2)
                        {
                            horarios[i].HoraInicio = horasOrdenadas[0];
                            horarios[i].HoraFin = horasOrdenadas[1];
                        }

                        if (selectorJornada == "2" && horasOrdenadas.Count == 4)
                        {
                            horarios[i].HoraInicio2 = horasOrdenadas[2];
                            horarios[i].HoraFin2 = horasOrdenadas[3];
                        }
                        else
                        {
                            horarios[i].HoraInicio2 = null;
                            horarios[i].HoraFin2 = null;
                        }
                    }

                    // Removemos validaciones nativas viejas para que no joda el ModelState
                    ModelState.Remove($"horarios[{i}].HoraInicio");
                    ModelState.Remove($"horarios[{i}].HoraFin");
                    ModelState.Remove($"horarios[{i}].HoraInicio2");
                    ModelState.Remove($"horarios[{i}].HoraFin2");
                }
            }

            if (ModelState.IsValid && horarios != null)
            {
                await _horarioService.GuardarHorariosSemanaMasivoAsync(tenantId, horarios);
                return RedirectToAction(nameof(Index));
            }

            ViewBag.TenantId = tenantId;
            return View(horarios);
        }

        private void LimpiarModelStateHorarios(List<HorarioLaboral>? horarios)
        {
            ModelState.Remove("Tenant");

            if (horarios == null) return;

            for (int i = 0; i < horarios.Count; i++)
            {
                ModelState.Remove($"horarios[{i}].Tenant");
                ModelState.Remove($"horarios[{i}].HoraInicio");
                ModelState.Remove($"horarios[{i}].HoraFin");
                ModelState.Remove($"horarios[{i}].HoraInicio2");
                ModelState.Remove($"horarios[{i}].HoraFin2");
            }
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
        public async Task<IActionResult> Edit(int id, [Bind("Id,NombreNegocio,Subdominio,PlanSuscripcion,Activo,FechaRegistro,CerradoPorEmergencia,FechaUltimoCierre,ColorPrimario,ColorSecundario,EstiloSidebar,TieneModuloHerramientas,TiempoEntreCitas,RutaLogo")] Tenant tenant, IFormFile? logoArchivo)
        {
            if (id != tenant.Id) return NotFound();

            if (ModelState.IsValid)
            {
                try
                {
                    if (logoArchivo != null && logoArchivo.Length > 0)
                    {
                        var tenantExistente = await _context.Tenants.AsNoTracking().FirstOrDefaultAsync(t => t.Id == id);
                        if (tenantExistente != null && !string.IsNullOrEmpty(tenantExistente.RutaLogo))
                        {
                            string rutaVieja = Path.Combine(_webHostEnvironment.WebRootPath, tenantExistente.RutaLogo.TrimStart('/'));
                            if (System.IO.File.Exists(rutaVieja)) System.IO.File.Delete(rutaVieja);
                        }

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

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
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
                .Include(c => c.Barbero)
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
