    using System;
    using System.Collections.Generic;

    namespace SaaSBarberia.Models;

    public partial class Factura
    {
        public int Id { get; set; }

        public int TenantId { get; set; }

        public int CitaId { get; set; }

        public decimal Total { get; set; }

        public string MetodoPago { get; set; } = null!;

        public DateTime? FechaPago { get; set; }

        public virtual Cita Cita { get; set; } = null!;

        public virtual Tenant Tenant { get; set; } = null!;
    }
