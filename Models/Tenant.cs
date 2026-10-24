using System;
using System.Collections.Generic;

namespace SaaSBarberia.Models;

public partial class Tenant
{
    public int Id { get; set; }

    public string NombreNegocio { get; set; } = null!;

    public string Subdominio { get; set; } = null!;

    // Propiedad Slug amigable para URLs (ej: barberia-el-negro)
    public string? Slug { get; set; }

    public string PlanSuscripcion { get; set; } = null!;

    public bool? Activo { get; set; }

    public DateTime? FechaRegistro { get; set; }
    public bool CerradoPorEmergencia { get; set; }
    public DateTime? FechaUltimoCierre { get; set; }

    public string ColorPrimario { get; set; } = "#1a1a1a";
    public string ColorSecundario { get; set; } = "#d4af37";
    public string EstiloSidebar { get; set; } = "DarkStyle";

    public bool TieneModuloHerramientas { get; set; } = false;
    public bool TieneModuloWhatsApp { get; set; } = false;

    public int TiempoEntreCitas { get; set; } = 0;

    // 🔥 NUEVO: Se agrega la ruta para el logo de la barbería en formato texto
    public string? RutaLogo { get; set; }

    public virtual ICollection<Barbero> Barberos { get; set; } = new List<Barbero>();
    public virtual ICollection<Cita> Cita { get; set; } = new List<Cita>();
    public virtual ICollection<Factura> Facturas { get; set; } = new List<Factura>();
    public virtual ICollection<Servicio> Servicios { get; set; } = new List<Servicio>();
    public virtual ICollection<Usuario> Usuarios { get; set; } = new List<Usuario>();
    public virtual ICollection<HorarioLaboral> HorariosLaborales { get; set; } = new List<HorarioLaboral>();
    public virtual ICollection<TenantModulo> Modulos { get; set; } = new List<TenantModulo>();
}
