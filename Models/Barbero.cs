using System;
using System.Collections.Generic;

namespace SaaSBarberia.Models;

public partial class Barbero
{
    public int Id { get; set; }

    public int TenantId { get; set; }

    // 🔥 CORREGIDO: Ya no es byte[], ahora guarda la ruta física de la foto en texto
    public string? RutaFoto { get; set; }

    public int UsuarioId { get; set; }

    public string? Especialidad { get; set; }

    public decimal? ComisionPorcentaje { get; set; }

    public virtual ICollection<Cita> Cita { get; set; } = new List<Cita>();

    public virtual Tenant Tenant { get; set; } = null!;

    public virtual Usuario Usuario { get; set; } = null!;
}