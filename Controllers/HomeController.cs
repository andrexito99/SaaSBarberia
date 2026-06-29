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
using static SaaSBarberia.Models.AdminDashboardViewModel;

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
                if (User.IsInRole("AdminBarberia"))
                {
                    var usuarioIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                    if (string.IsNullOrEmpty(usuarioIdStr)) return RedirectToAction("Login", "Account");
                    int usuarioId = int.Parse(usuarioIdStr);

                    var usuarioDb = await _context.Usuarios.FirstOrDefaultAsync(u => u.Id == usuarioId);
                    if (usuarioDb == null || usuarioDb.TenantId == null) return NotFound("El administrador no tiene una barbería asignada.");
                    int tenantIdAdmin = usuarioDb.TenantId.Value;

                    var hoy = DateTime.Today;
                    var inicioMes = new DateTime(hoy.Year, hoy.Month, 1);

                    // 📅 Fechas para comparación de meses
                    var inicioMesAnterior = inicioMes.AddMonths(-1);
                    var finMesAnterior = inicioMes.AddDays(-1);

                    // 1. KPIs de turnos para HOY
                    var citasHoyTotales = await _context.Citas
                        .Where(c => c.TenantId == tenantIdAdmin && c.FechaHora.Date == hoy)
                        .ToListAsync();

                    int citasHoy = citasHoyTotales.Count(c => c.Estado != "Cancelada");
                    int atendidosHoy = citasHoyTotales.Count(c => c.Estado == "Completada" || c.Estado == "Realizada");
                    int pendientesHoy = citasHoyTotales.Count(c => c.Estado == "Pendiente");
                    int canceladasHoy = citasHoyTotales.Count(c => c.Estado == "Cancelada");

                    // Básicos de la sede
                    var totalBarberos = await _context.Barberos.CountAsync(b => b.TenantId == tenantIdAdmin);
                    var totalServicios = await _context.Servicios.CountAsync(s => s.TenantId == tenantIdAdmin);
                    var totalClientes = await _context.Usuarios.CountAsync(u => u.TenantId == tenantIdAdmin && u.Role.NombreRole == "Cliente");

                    // 2. Cálculos financieros y comparación con mes anterior
                    var ingresosHoy = await _context.Facturas
                        .Where(f => f.TenantId == tenantIdAdmin && f.FechaPago.HasValue && f.FechaPago.Value.Date == hoy)
                        .SumAsync(f => f.Total);

                    var ingresosMes = await _context.Facturas
                        .Where(f => f.TenantId == tenantIdAdmin && f.FechaPago.HasValue && f.FechaPago.Value >= inicioMes)
                        .SumAsync(f => f.Total);

                    var ingresosMesAnterior = await _context.Facturas
                        .Where(f => f.TenantId == tenantIdAdmin && f.FechaPago.HasValue && f.FechaPago.Value >= inicioMesAnterior && f.FechaPago.Value <= finMesAnterior)
                        .SumAsync(f => f.Total);

                    decimal porcentajeComparacion = 0;
                    bool esMayor = true;
                    if (ingresosMesAnterior > 0)
                    {
                        porcentajeComparacion = ((ingresosMes - ingresosMesAnterior) / ingresosMesAnterior) * 100;
                        esMayor = ingresosMes >= ingresosMesAnterior;
                    }

                    // 3. Servicio más vendido del mes actual
                    var topServicio = await _context.Citas
                        .Include(c => c.Servicio)
                        .Where(c => c.TenantId == tenantIdAdmin && c.FechaHora >= inicioMes && c.Estado != "Cancelada")
                        .GroupBy(c => c.Servicio.Nombre)
                        .Select(g => new { Nombre = g.Key, Cantidad = g.Count() })
                        .OrderByDescending(x => x.Cantidad)
                        .FirstOrDefaultAsync();

                    // 4. Barbero Estrella del mes
                    var rankingBarberos = await _context.Citas
                        .Include(c => c.Barbero.Usuario)
                        .Where(c => c.TenantId == tenantIdAdmin && c.FechaHora >= inicioMes && c.Estado != "Cancelada" && c.Barbero.Usuario != null)
                        .GroupBy(c => new { c.BarberoId, c.Barbero.Usuario.Nombre })
                        .Select(g => new { g.Key.BarberoId, g.Key.Nombre, Cantidad = g.Count() })
                        .OrderByDescending(x => x.Cantidad)
                        .FirstOrDefaultAsync();

                    decimal gananciasEstrella = 0;
                    if (rankingBarberos != null)
                    {
                        gananciasEstrella = await _context.Facturas
                            .Include(f => f.Cita)
                            .Where(f => f.TenantId == tenantIdAdmin && f.Cita.BarberoId == rankingBarberos.BarberoId && f.FechaPago.HasValue && f.FechaPago.Value >= inicioMes)
                            .SumAsync(f => f.Total);
                    }

                    // 5. Gráficos (Mantenemos tu lógica intacta)
                    var citasDelMes = await _context.Citas.Where(c => c.TenantId == tenantIdAdmin && c.FechaHora >= inicioMes).ToListAsync();
                    var citasPorEstado = citasDelMes.GroupBy(c => c.Estado).ToDictionary(g => g.Key ?? "Pendiente", g => g.Count());
                    if (!citasPorEstado.ContainsKey("Pendiente")) citasPorEstado["Pendiente"] = 0;
                    if (!citasPorEstado.ContainsKey("Completada")) citasPorEstado["Completada"] = 0;
                    if (!citasPorEstado.ContainsKey("Cancelada")) citasPorEstado["Cancelada"] = 0;

                    var citasConBarbero = await _context.Citas.Include(c => c.Barbero.Usuario).Where(c => c.TenantId == tenantIdAdmin && c.Estado != "Cancelada" && c.Barbero.Usuario != null).ToListAsync();
                    var controlBarberos = citasConBarbero.GroupBy(c => c.Barbero.Usuario.Nombre).ToDictionary(g => g.Key, g => g.Count());

                    // 🏆 6. TOP 10 CLIENTES (Agrupados por el Usuario que saca la cita)
                    var topClientesDb = await _context.Citas
                        .Include(c => c.Cliente)
                        .Where(c => c.TenantId == tenantIdAdmin && c.Estado == "Completada")
                        .GroupBy(c => c.Cliente.Nombre)
                        .Select(g => new TopClienteDashboardDto
                        {
                            NombreCliente = g.Key ?? "Cliente General",
                            TotalCitas = g.Count(),
                            TotalGastado = _context.Facturas.Where(f => f.Cita.Cliente.Nombre == g.Key && f.FechaPago.HasValue).Sum(f => f.Total)
                        })
                        .OrderByDescending(x => x.TotalGastado)
                        .Take(10)
                        .ToListAsync();

                    var dashboardData = new AdminDashboardViewModel
                    {
                        CitasParaHoy = citasHoy,
                        ClientesAtendidosHoy = atendidosHoy,
                        CitasPendientesHoy = pendientesHoy,
                        CitasCanceladasHoy = canceladasHoy,
                        TotalBarberos = totalBarberos,
                        TotalServiciosOfrecidos = totalServicios,
                        TotalClientesRegistrados = totalClientes,
                        IngresosHoy = ingresosHoy,
                        IngresosMes = ingresosMes,
                        PorcentajeComparacionMesAnterior = porcentajeComparacion,
                        EsIngresoMayorMesAnterior = esMayor,
                        ServicioMasVendido = topServicio?.Nombre ?? "Ninguno",
                        CantidadServicioMasVendido = topServicio?.Cantidad ?? 0,
                        BarberoEstrella = rankingBarberos?.Nombre ?? "Sin datos",
                        CortesBarberoEstrella = rankingBarberos?.Cantidad ?? 0,
                        GananciaBarberoEstrella = gananciasEstrella,
                        ResumenCitasPorEstado = citasPorEstado,
                        ClientesPorBarbero = controlBarberos,
                        Top10Clientes = topClientesDb
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