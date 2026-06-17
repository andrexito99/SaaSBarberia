using System;
using System.Collections.Generic;

namespace SaaSBarberia.Models;

public partial class Cita
{
    public int Id { get; set; }

    public int TenantId { get; set; }

    public int ClienteId { get; set; }

    public int BarberoId { get; set; }

    public int ServicioId { get; set; }

    public DateTime FechaHora { get; set; }

    public string? Estado { get; set; }

    public string? Notas { get; set; }

    public virtual Barbero Barbero { get; set; } = null!;

    public virtual Usuario Cliente { get; set; } = null!;

    public virtual ICollection<Factura> Facturas { get; set; } = new List<Factura>();

    public virtual Servicio Servicio { get; set; } = null!;

    public virtual Tenant Tenant { get; set; } = null!;
}
