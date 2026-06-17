using System;
using System.Collections.Generic;

namespace SaaSBarberia.Models;

public partial class Tenant
{
    public int Id { get; set; }

    public string NombreNegocio { get; set; } = null!;

    public string Subdominio { get; set; } = null!;

    public string PlanSuscripcion { get; set; } = null!;

    public bool? Activo { get; set; }

    public DateTime? FechaRegistro { get; set; }

    public virtual ICollection<Barbero> Barberos { get; set; } = new List<Barbero>();

    public virtual ICollection<Cita> Cita { get; set; } = new List<Cita>();

    public virtual ICollection<Factura> Facturas { get; set; } = new List<Factura>();

    public virtual ICollection<Servicio> Servicios { get; set; } = new List<Servicio>();

    public virtual ICollection<Usuario> Usuarios { get; set; } = new List<Usuario>();
}
