using System;

namespace SaaSBarberia.Models;

public partial class DetalleCaja
{
    public int Id { get; set; }
    public int MovimientoCajaId { get; set; }
    public decimal Monto { get; set; }
    public string Tipo { get; set; } = null!; // "Ingreso" o "Egreso"
    public string Descripcion { get; set; } = null!;
    public DateTime Fecha { get; set; } = DateTime.Now;

    public virtual MovimientoCaja MovimientoCaja { get; set; } = null!;
}