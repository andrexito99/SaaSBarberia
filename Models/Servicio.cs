using System;
using System.Collections.Generic;

namespace SaaSBarberia.Models;

public partial class Servicio
{
    public int Id { get; set; }

    public int TenantId { get; set; }

    public string Nombre { get; set; } = null!;

    public decimal Precio { get; set; }

    public int DuracionMinutos { get; set; }

    public virtual ICollection<Cita> Cita { get; set; } = new List<Cita>();

    public virtual Tenant Tenant { get; set; } = null!;
}
