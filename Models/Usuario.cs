    using System;
    using System.Collections.Generic;

    namespace SaaSBarberia.Models;

    public partial class Usuario
    {
        public int Id { get; set; }

        public int? TenantId { get; set; }

        public int RoleId { get; set; }

        public string Nombre { get; set; } = null!;

        public string Email { get; set; } = null!;

        public string PasswordHash { get; set; } = null!;

        public string? Telefono { get; set; }

        public bool? Activo { get; set; }

        public string? ResetToken { get; set; }

        public DateTime? TokenExpiracion { get; set; }

        public bool? TokenUsado { get; set; }

        public virtual ICollection<Barbero> Barberos { get; set; } = new List<Barbero>();

        public virtual ICollection<Cita> Cita { get; set; } = new List<Cita>();

        public virtual Role Role { get; set; } = null!;

        public virtual Tenant? Tenant { get; set; }
    }
