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
    public bool CerradoPorEmergencia { get; set; }
    public DateTime? FechaUltimoCierre { get; set; }

    public string ColorPrimario { get; set; } = "#1a1a1a";
    public string ColorSecundario { get; set; } = "#d4af37";
    public string EstiloSidebar { get; set; } = "DarkStyle";

    public bool TieneModuloHerramientas { get; set; } = false;

    // 🔥 La propiedad que conecta con la idea del analista
    public int TiempoEntreCitas { get; set; } = 0;

    public virtual ICollection<Barbero> Barberos { get; set; } = new List<Barbero>();
    public virtual ICollection<Cita> Cita { get; set; } = new List<Cita>();
    public virtual ICollection<Factura> Facturas { get; set; } = new List<Factura>();
    public virtual ICollection<Servicio> Servicios { get; set; } = new List<Servicio>();
    public virtual ICollection<Usuario> Usuarios { get; set; } = new List<Usuario>();
    public virtual ICollection<HorarioLaboral> HorariosLaborales { get; set; } = new List<HorarioLaboral>();
}