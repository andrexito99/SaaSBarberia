using System.Collections.Generic;

namespace SaaSBarberia.Models
{
    public class AdminDashboardViewModel
    {
        public int CitasParaHoy { get; set; }
        public int TotalBarberos { get; set; }
        public int TotalServiciosOfrecidos { get; set; }
        public int TotalClientesRegistrados { get; set; }

        // 🌟 NUEVOS CAMPOS FINANCIEROS
        public decimal IngresosHoy { get; set; }
        public decimal IngresosMes { get; set; }
        public decimal GananciaBarberoEstrella { get; set; }

        public string BarberoEstrella { get; set; } = null!;
        public int CortesBarberoEstrella { get; set; }

        public Dictionary<string, int> ResumenCitasPorEstado { get; set; } = new Dictionary<string, int>();
        public Dictionary<string, int> ClientesPorBarbero { get; set; } = new Dictionary<string, int>();
    }
}