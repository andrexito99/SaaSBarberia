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

        public int ClientesAtendidosHoy { get; set; }
        public int CitasPendientesHoy { get; set; }
        public int CitasCanceladasHoy { get; set; }
        public decimal PorcentajeComparacionMesAnterior { get; set; }
        public bool EsIngresoMayorMesAnterior { get; set; }
        public string ServicioMasVendido { get; set; } = "Ninguno";
        public int CantidadServicioMasVendido { get; set; }
        public List<TopClienteDashboardDto> Top10Clientes { get; set; } = new List<TopClienteDashboardDto>();

        // Clase auxiliar al final del archivo para el listado
        public class TopClienteDashboardDto
        {
            public string NombreCliente { get; set; } = null!;
            public int TotalCitas { get; set; }
            public decimal TotalGastado { get; set; }
        }
    }
}