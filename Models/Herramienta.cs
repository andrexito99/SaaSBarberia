using System.ComponentModel.DataAnnotations;

namespace SaaSBarberia.Models
{
    public class Herramienta
    {
        public int Id { get; set; }
        public int TenantId { get; set; }

        [Required(ErrorMessage = "El nombre de la herramienta es obligatorio.")]
        [StringLength(100)]
        public string Nombre { get; set; } = string.Empty;

        [StringLength(50)]
        public string? Marca { get; set; }

        [StringLength(100)]
        public string? NumeroSerie { get; set; }

        [Required]
        public string Estado { get; set; } = "Disponible"; // Disponible, Alquilado, Mantenimiento, Dañado

        // Propiedad de navegación
        public virtual Tenant? Tenant { get; set; }
    }
}