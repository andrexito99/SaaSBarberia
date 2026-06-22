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

namespace SaaSBarberia.Controllers;

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
            // 🌟 SI ES EL ADMINISTRADOR DE LA BARBERÍA, ARMAMOS SU PANEL DE CONTROL
            if (User.IsInRole("AdminBarberia"))
            {
                var inicioMes = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
                var hoy = DateTime.Today;

                // 1. Citas agendadas para el día de hoy (que no estén canceladas)
                var citasHoy = await _context.Citas
                    .CountAsync(c => c.FechaHora.Date == hoy && c.Estado != "Cancelada");

                // 2. Facturación total acumulada del mes en curso
                var ingresosMes = await _context.Facturas
                    .Where(f => f.FechaPago >= inicioMes)
                    .SumAsync(f => f.Total);

                // 3. Cantidad de servicios despachados (citas completadas con factura)
                var totalServicios = await _context.Facturas.CountAsync();

                // 4. Buscar el Barbero Estrella basándonos en facturas cobradas este mes
                var rankingBarberos = await _context.Facturas
                    .Include(f => f.Cita.Barbero.Usuario)
                    .Where(f => f.FechaPago >= inicioMes)
                    .GroupBy(f => f.Cita.Barbero.Usuario.Nombre)
                    .Select(g => new { Nombre = g.Key, Cantidad = g.Count() })
                    .OrderByDescending(x => x.Cantidad)
                    .FirstOrDefaultAsync();

                // 5. Agrupar ingresos de los últimos 7 días para el gráfico de barras
                var haceUnaSemana = hoy.AddDays(-6);
                var facturasSemanales = await _context.Facturas
                    .Where(f => f.FechaPago >= haceUnaSemana)
                    .ToListAsync(); // Traemos a memoria corta para agrupar por strings de fechas de forma segura

                var ventasPorDia = facturasSemanales
                    .GroupBy(f => f.FechaPago.Value.ToString("dd/MM"))
                    .ToDictionary(g => g.Key, g => g.Sum(f => f.Total));

                // 6. Servicios más solicitados para el gráfico de torta
                var facturasConServicio = await _context.Facturas
                    .Include(f => f.Cita.Servicio)
                    .ToListAsync();

                var topServicios = facturasConServicio
                    .GroupBy(f => f.Cita.Servicio.Nombre)
                    .ToDictionary(g => g.Key, g => g.Count());

                var dashboardData = new AdminDashboardViewModel
                {
                    CitasParaHoy = citasHoy,
                    IngresosMesActual = ingresosMes,
                    TotalServiciosRealizados = totalServicios,
                    BarberoEstrella = rankingBarberos?.Nombre ?? "Sin datos",
                    CortesBarberoEstrella = rankingBarberos?.Cantidad ?? 0,
                    VentasUltimosDias = ventasPorDia,
                    ServiciosMasPedidos = topServicios
                };

                // Enviamos el objeto con toda la data consolidada a la vista común Index
                return View("Index", dashboardData);
            }

            // Si es SuperAdmin u otro rol, mandamos la vista limpia sin este modelo específico
            return View("Index");
        }

        // Si no está logueado, le cargamos el Catálogo Público de Servicios con normalidad
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