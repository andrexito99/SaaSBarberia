using System;
using System.Collections.Generic;

namespace SaaSBarberia.Models;

public class AdminDashboardViewModel
{
    // KPIs en Tarjetas
    public int CitasParaHoy { get; set; }
    public decimal IngresosMesActual { get; set; }
    public int TotalServiciosRealizados { get; set; }

    // Barbero estrella del mes
    public string BarberoEstrella { get; set; } = "Ninguno todavía";
    public int CortesBarberoEstrella { get; set; }

    // Data en diccionarios para que Chart.js los dibuje rápido
    public Dictionary<string, decimal> VentasUltimosDias { get; set; } = new(); // Fecha -> Total Ventas
    public Dictionary<string, int> ServiciosMasPedidos { get; set; } = new();   // Nombre Servicio -> Cantidad
}