using System.ComponentModel.DataAnnotations;

namespace SaaSBarberia.Models
{
    public class AlquilerHerramienta
    {
        public int Id { get; set; }
        public int TenantId { get; set; }

        [Required]
        public int HerramientaId { get; set; }

        [Required]
        public int BarberoId { get; set; }

        [Required]
        public DateTime FechaPrestamo { get; set; } = DateTime.Now;

        public DateTime? FechaDevolucion { get; set; }

        [Required]
        public decimal CostoAlquiler { get; set; } = 0;

        // Propiedades de navegación
        public virtual Tenant? Tenant { get; set; }
        public virtual Herramienta? Herramienta { get; set; }
        public virtual Barbero? Barbero { get; set; }
    }
}