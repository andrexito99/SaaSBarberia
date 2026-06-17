using System;
using System.Collections.Generic;

namespace SaaSBarberia.Models;

public partial class Role
{
    public int Id { get; set; }

    public string NombreRole { get; set; } = null!;

    public virtual ICollection<Usuario> Usuarios { get; set; } = new List<Usuario>();
}
