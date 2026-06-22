using System;
using System.Collections.Generic;

namespace SaaSBarberia.Models;

public partial class MovimientoCaja
{
    public int Id { get; set; }
    public int TenantId { get; set; }
    public int UsuarioId { get; set; }
    public DateTime FechaApertura { get; set; }
    public DateTime? FechaCierre { get; set; }
    public decimal MontoInicial { get; set; }
    public decimal MontoFinalCalculado { get; set; }
    public decimal? MontoFinalReal { get; set; }
    public string Estado { get; set; } = "Abierta";

    public virtual Usuario Usuario { get; set; } = null!;
    public virtual Tenant Tenant { get; set; } = null!;
    public virtual ICollection<DetalleCaja> DetallesCaja { get; set; } = new List<DetalleCaja>();
}