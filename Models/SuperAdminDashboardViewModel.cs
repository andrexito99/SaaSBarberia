using System.Collections.Generic;

namespace SaaSBarberia.Models;

public class SuperAdminDashboardViewModel
{
    // KPIs (Tarjetas de totales)
    public int TotalTenants { get; set; }
    public int TenantsActivos { get; set; }
    public int TotalUsuarios { get; set; }
    public decimal RecaudacionEstimada { get; set; }

    // Listados rápidos para las tablas del dashboard
    public List<Tenant> UltimosTenants { get; set; }

    // Diccionarios listos para que Chart.js los procese en JavaScript
    public Dictionary<string, int> DatosPlanes { get; set; } // Plan -> Cantidad
}