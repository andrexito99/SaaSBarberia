using System.ComponentModel.DataAnnotations;

namespace SaaSBarberia.Models
{
    public class Producto
    {
        public int Id { get; set; }
        public int TenantId { get; set; }

        [Required(ErrorMessage = "El nombre del producto es obligatorio.")]
        [StringLength(100)]
        public string Nombre { get; set; } = string.Empty;

        [StringLength(255)]
        public string? Descripcion { get; set; }

        [Required]
        [Range(0, 99999999, ErrorMessage = "El precio debe ser un valor positivo.")]
        public decimal PrecioVenta { get; set; }

        [Required]
        [Range(0, 9999, ErrorMessage = "El stock no puede ser negativo.")]
        public int Stock { get; set; }

        // Propiedad de navegación
        public virtual Tenant? Tenant { get; set; }
    }
}