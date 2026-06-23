using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SaaSBarberia.Data;
using SaaSBarberia.Models;
using System;
using System.Diagnostics;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;

namespace SaaSBarberia.Controllers
{
    public class HomeController : Controller
    {
        private readonly BarberSaaSDbContext _context;

        public HomeController(BarberSaaSDbContext context)
        {
            _context = context;
        }

        [AllowAnonymous]
        public async Task<IActionResult> Index()
        {
            if (User.Identity != null && User.Identity.IsAuthenticated)
            {
                // 🌟 PANEL DE CONTROL OPERACIONAL DE LA BARBERÍA
                if (User.IsInRole("AdminBarberia"))
                {
                    var usuarioIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                    if (string.IsNullOrEmpty(usuarioIdStr)) return RedirectToAction("Login", "Account");
                    int usuarioId = int.Parse(usuarioIdStr);

                    // 🏢 Buscamos el TenantId que le pertenece a este Administrador
                    var usuarioDb = await _context.Usuarios.FirstOrDefaultAsync(u => u.Id == usuarioId);
                    if (usuarioDb == null || usuarioDb.TenantId == null) return NotFound("El administrador no tiene una barbería asignada.");
                    int tenantIdAdmin = usuarioDb.TenantId.Value;

                    var hoy = DateTime.Today;
                    var inicioMes = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);

                    // 1. Citas agendadas para HOY filtradas por su Barbería
                    var citasHoy = await _context.Citas
                        .CountAsync(c => c.TenantId == tenantIdAdmin && c.FechaHora.Date == hoy && c.Estado != "Cancelada");

                    // 2. Total de Barberos contratados en esta sede
                    var totalBarberos = await _context.Barberos
                        .CountAsync(b => b.TenantId == tenantIdAdmin);

                    // 3. Total de Servicios configurados en el catálogo de la sede
                    var totalServicios = await _context.Servicios
                        .CountAsync(s => s.TenantId == tenantIdAdmin);

                    // 4. Total de Clientes vinculados a esta sede (Usuarios con rol Cliente)
                    var totalClientes = await _context.Usuarios
                        .CountAsync(u => u.TenantId == tenantIdAdmin && u.Role.NombreRole == "Cliente");

                    // 💰 5. CÁLCULOS FINANCIEROS REALES (Usa la tabla Factura)
                    var ingresosHoy = await _context.Facturas
                        .Where(f => f.TenantId == tenantIdAdmin && f.FechaPago.HasValue && f.FechaPago.Value.Date == hoy)
                        .SumAsync(f => f.Total);

                    var ingresosMes = await _context.Facturas
                        .Where(f => f.TenantId == tenantIdAdmin && f.FechaPago.HasValue && f.FechaPago.Value >= inicioMes)
                        .SumAsync(f => f.Total);

                    // 6. El Barbero que más citas tiene agendadas este mes
                    var rankingBarberos = await _context.Citas
                        .Include(c => c.Barbero.Usuario)
                        .Where(c => c.TenantId == tenantIdAdmin && c.FechaHora >= inicioMes && c.Estado != "Cancelada" && c.Barbero.Usuario != null)
                        .GroupBy(c => new { c.BarberoId, c.Barbero.Usuario.Nombre })
                        .Select(g => new {
                            BarberoId = g.Key.BarberoId,
                            Nombre = g.Key.Nombre,
                            Cantidad = g.Count()
                        })
                        .OrderByDescending(x => x.Cantidad)
                        .FirstOrDefaultAsync();

                    // 💰 7. Calcular cuánto dinero ha generado ese Barbero Estrella en el mes
                    decimal gananciasEstrella = 0;
                    if (rankingBarberos != null)
                    {
                        gananciasEstrella = await _context.Facturas
                            .Include(f => f.Cita)
                            .Where(f => f.TenantId == tenantIdAdmin
                                     && f.Cita.BarberoId == rankingBarberos.BarberoId
                                     && f.FechaPago.HasValue
                                     && f.FechaPago.Value >= inicioMes)
                            .SumAsync(f => f.Total);
                    }

                    // 8. Gráfico de Torta: Estado de todas las citas del mes
                    var citasDelMes = await _context.Citas
                        .Where(c => c.TenantId == tenantIdAdmin && c.FechaHora >= inicioMes)
                        .ToListAsync();

                    var citasPorEstado = citasDelMes
                        .GroupBy(c => c.Estado)
                        .ToDictionary(g => g.Key ?? "Pendiente", g => g.Count());

                    // Asegurar estados principales
                    if (!citasPorEstado.ContainsKey("Pendiente")) citasPorEstado["Pendiente"] = 0;
                    if (!citasPorEstado.ContainsKey("Completada")) citasPorEstado["Completada"] = 0;
                    if (!citasPorEstado.ContainsKey("Cancelada")) citasPorEstado["Cancelada"] = 0;

                    // 9. Gráfico de Barras: Citas totales asignadas a cada barbero de la sede
                    var citasConBarbero = await _context.Citas
                        .Include(c => c.Barbero.Usuario)
                        .Where(c => c.TenantId == tenantIdAdmin && c.Estado != "Cancelada" && c.Barbero.Usuario != null)
                        .ToListAsync();

                    var controlBarberos = citasConBarbero
                        .GroupBy(c => c.Barbero.Usuario.Nombre)
                        .ToDictionary(g => g.Key, g => g.Count());

                    // 🎯 CONSOLIDAMOS TODA LA DATA
                    var dashboardData = new AdminDashboardViewModel
                    {
                        CitasParaHoy = citasHoy,
                        TotalBarberos = totalBarberos,
                        TotalServiciosOfrecidos = totalServicios,
                        TotalClientesRegistrados = totalClientes,
                        IngresosHoy = ingresosHoy,
                        IngresosMes = ingresosMes,
                        BarberoEstrella = rankingBarberos?.Nombre ?? "Sin datos",
                        CortesBarberoEstrella = rankingBarberos?.Cantidad ?? 0,
                        GananciaBarberoEstrella = gananciasEstrella,
                        ResumenCitasPorEstado = citasPorEstado,
                        ClientesPorBarbero = controlBarberos
                    };

                    return View("Index", dashboardData);
                }

                return View("Index");
            }

            var serviciosPublicos = await _context.Servicios.Include(s => s.Tenant).ToListAsync();
            return View("IndexPublico", serviciosPublicos);
        }

        [AllowAnonymous]
        public IActionResult Privacy()
        {
            return View();
        }

        [AllowAnonymous]
        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}