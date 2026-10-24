using System;
using System.Collections.Generic;
using System.Security.Claims;
using Microsoft.AspNetCore.Http; // 🌟 Necesario para acceder a HttpContext
using Microsoft.EntityFrameworkCore;
using SaaSBarberia.Models;
using SaaSBarberia.Services;

namespace SaaSBarberia.Data;

public partial class BarberSaaSDbContext : DbContext
{
    private readonly IHttpContextAccessor? _httpContextAccessor;

    // Constructor por defecto obligatorio para migraciones o herramientas de EF
    public BarberSaaSDbContext()
    {
    }

    // 🌟 Inyectamos IHttpContextAccessor junto con las opciones normales del contexto
    public BarberSaaSDbContext(DbContextOptions<BarberSaaSDbContext> options, IHttpContextAccessor? httpContextAccessor = null)
        : base(options)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    // 🔑 PROPIEDAD INTELIGENTE: Obtiene el TenantId del usuario logueado en este milisegundo
    public int? TenantIdActual
    {
        get
        {
            var user = _httpContextAccessor?.HttpContext?.User;
            var tenantClaim = user?.FindFirst("TenantId")?.Value;

            if (int.TryParse(tenantClaim, out int tenantId))
            {
                return tenantId;
            }
            return null;
        }
    }

    // 👑 PROPIEDAD INTELIGENTE: Evalúa si el usuario en sesión es SuperAdmin
    public bool EsSuperAdmin => _httpContextAccessor?.HttpContext?.User?.IsInRole("SuperAdmin") ?? false;

    public virtual DbSet<Barbero> Barberos { get; set; }
    public virtual DbSet<Cita> Citas { get; set; }
    public virtual DbSet<Factura> Facturas { get; set; }
    public virtual DbSet<Role> Roles { get; set; }
    public virtual DbSet<Servicio> Servicios { get; set; }
    public virtual DbSet<Tenant> Tenants { get; set; }
    public DbSet<Auditoria> Auditorias { get; set; }
    public virtual DbSet<Usuario> Usuarios { get; set; }
    public DbSet<HorarioLaboral> HorariosLaborales { get; set; }

    public DbSet<Producto> Productos { get; set; }
    public DbSet<Herramienta> Herramientas { get; set; }
    public DbSet<AlquilerHerramienta> AlquileresHerramientas { get; set; }

    public virtual DbSet<MovimientoCaja> MovimientosCaja { get; set; }
    public virtual DbSet<DetalleCaja> DetallesCaja { get; set; }
    public DbSet<BarberoAusencia> BarberoAusencias { get; set; }
    public DbSet<Notificacion> Notificaciones { get; set; }
    public DbSet<WhatsAppConfiguracion> WhatsAppConfiguraciones { get; set; }
    public DbSet<WhatsAppPlantilla> WhatsAppPlantillas { get; set; }
    public DbSet<WhatsAppMensajeHistorial> WhatsAppMensajesHistorial { get; set; }
    public DbSet<TenantModulo> TenantModulos { get; set; }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder) { }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Servicio>().HasQueryFilter(s => EsSuperAdmin || s.TenantId == TenantIdActual);
        modelBuilder.Entity<Cita>().HasQueryFilter(c => EsSuperAdmin || c.TenantId == TenantIdActual);
        modelBuilder.Entity<Barbero>().HasQueryFilter(b => EsSuperAdmin || b.TenantId == TenantIdActual);
        modelBuilder.Entity<Factura>().HasQueryFilter(f => EsSuperAdmin || f.TenantId == TenantIdActual);
        modelBuilder.Entity<Usuario>().HasQueryFilter(u => EsSuperAdmin || u.TenantId == TenantIdActual);
        modelBuilder.Entity<MovimientoCaja>().HasQueryFilter(mc => EsSuperAdmin || mc.TenantId == TenantIdActual);
        modelBuilder.Entity<BarberoAusencia>().HasQueryFilter(a => EsSuperAdmin || a.TenantId == TenantIdActual);
        modelBuilder.Entity<Notificacion>().HasQueryFilter(n => EsSuperAdmin || n.TenantId == TenantIdActual);
        modelBuilder.Entity<Producto>().HasQueryFilter(p => EsSuperAdmin || p.TenantId == TenantIdActual);
        modelBuilder.Entity<Herramienta>().HasQueryFilter(h => EsSuperAdmin || h.TenantId == TenantIdActual);
        modelBuilder.Entity<AlquilerHerramienta>().HasQueryFilter(a => EsSuperAdmin || a.TenantId == TenantIdActual);

        modelBuilder.Entity<HorarioLaboral>(entity =>
        {
            entity.HasKey(e => e.Id);

            entity.HasOne(d => d.Tenant)
                  .WithMany(p => p.HorariosLaborales)
                  .HasForeignKey(d => d.TenantId);

            // Filtro global para aislar los datos por barbería automáticamente
            entity.HasQueryFilter(e => EsSuperAdmin || e.TenantId == TenantIdActual);
        });

        modelBuilder.Entity<Producto>(entity =>
        {
            entity.ToTable("Productos");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.PrecioVenta).HasColumnType("decimal(18, 2)");
        });

        modelBuilder.Entity<Herramienta>(entity =>
        {
            entity.ToTable("Herramientas");
            entity.HasKey(e => e.Id);
        });

        modelBuilder.Entity<AlquilerHerramienta>(entity =>
        {
            entity.ToTable("AlquileresHerramientas");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.CostoAlquiler).HasColumnType("decimal(18, 2)");

            entity.HasOne(a => a.Herramienta)
                  .WithMany()
                  .HasForeignKey(a => a.HerramientaId);

            entity.HasOne(a => a.Barbero)
                  .WithMany()
                  .HasForeignKey(a => a.BarberoId);
        });

        modelBuilder.Entity<MovimientoCaja>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK_MovimientosCaja");
            entity.ToTable("MovimientosCaja");

            entity.Property(e => e.FechaApertura).HasColumnType("datetime").HasDefaultValueSql("(getdate())");
            entity.Property(e => e.FechaCierre).HasColumnType("datetime");
            entity.Property(e => e.MontoInicial).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.MontoFinalCalculado).HasColumnType("decimal(18, 2)").HasDefaultValue(0.00m);
            entity.Property(e => e.MontoFinalReal).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.Estado).HasMaxLength(50).IsUnicode(false).HasDefaultValue("Abierta");

            entity.HasOne(d => d.Tenant).WithMany()
                .HasForeignKey(d => d.TenantId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_MovimientosCaja_Tenants");

            entity.HasOne(d => d.Usuario).WithMany()
                .HasForeignKey(d => d.UsuarioId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_MovimientosCaja_Usuarios");
        });

        modelBuilder.Entity<DetalleCaja>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK_DetallesCaja");
            entity.ToTable("DetallesCaja");

            entity.Property(e => e.Monto).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.Tipo).HasMaxLength(50).IsUnicode(false);
            entity.Property(e => e.Descripcion).HasMaxLength(255).IsUnicode(false);
            entity.Property(e => e.Fecha).HasColumnType("datetime").HasDefaultValueSql("(getdate())");

            entity.HasOne(d => d.MovimientoCaja).WithMany(p => p.DetallesCaja)
                .HasForeignKey(d => d.MovimientoCajaId)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("FK_DetallesCaja_MovimientosCaja");
        });

        modelBuilder.Entity<Barbero>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__Barberos__3214EC07C28CA89D");
            entity.Property(e => e.ComisionPorcentaje).HasDefaultValue(0.00m).HasColumnType("decimal(5, 2)");
            entity.Property(e => e.Especialidad).HasMaxLength(100).IsUnicode(false);

            entity.HasOne(d => d.Tenant).WithMany(p => p.Barberos)
                .HasForeignKey(d => d.TenantId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Barberos_Tenants");

            entity.HasOne(d => d.Usuario).WithMany(p => p.Barberos)
                .HasForeignKey(d => d.UsuarioId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Barberos_Usuarios");
        });

        modelBuilder.Entity<BarberoAusencia>(entity =>
        {
            entity.ToTable("BarberoAusencias");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.FechaInicio).HasColumnType("datetime2");
            entity.Property(e => e.FechaFin).HasColumnType("datetime2");
            entity.Property(e => e.FechaRegistro).HasColumnType("datetime2").HasDefaultValueSql("(getdate())");
            entity.Property(e => e.Tipo).HasConversion<int>();
            entity.Property(e => e.Motivo).HasMaxLength(250);
            entity.Property(e => e.Observaciones).HasMaxLength(1000);
            entity.Property(e => e.CreadoPorUsuarioId).HasMaxLength(450);
            entity.HasIndex(e => new { e.TenantId, e.BarberoId, e.FechaInicio, e.FechaFin }).HasDatabaseName("IX_BarberoAusencias_Agenda");
            entity.HasOne(e => e.Barbero).WithMany().HasForeignKey(e => e.BarberoId).OnDelete(DeleteBehavior.Restrict).HasConstraintName("FK_BarberoAusencias_Barberos");
        });

        modelBuilder.Entity<Notificacion>(entity =>
        {
            entity.ToTable("Notificaciones");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Titulo).HasMaxLength(150);
            entity.Property(e => e.Mensaje).HasMaxLength(1000);
            entity.Property(e => e.Tipo).HasMaxLength(50);
            entity.Property(e => e.Icono).HasMaxLength(80);
            entity.Property(e => e.Color).HasMaxLength(30);
            entity.Property(e => e.UrlDestino).HasMaxLength(500);
            entity.Property(e => e.FechaCreacion).HasColumnType("datetime2").HasDefaultValueSql("(getdate())");
            entity.Property(e => e.FechaLectura).HasColumnType("datetime2");
            entity.HasIndex(e => new { e.TenantId, e.UsuarioDestinoId, e.Leida, e.FechaCreacion }).HasDatabaseName("IX_Notificaciones_Usuario_Bandeja");
            entity.HasOne(e => e.Tenant).WithMany().HasForeignKey(e => e.TenantId).OnDelete(DeleteBehavior.Cascade).HasConstraintName("FK_Notificaciones_Tenants");
            entity.HasOne(e => e.UsuarioDestino).WithMany().HasForeignKey(e => e.UsuarioDestinoId).OnDelete(DeleteBehavior.Restrict).HasConstraintName("FK_Notificaciones_Usuarios");
        });

        modelBuilder.Entity<WhatsAppConfiguracion>(entity =>
        {
            entity.ToTable("WhatsAppConfiguraciones");
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.TenantId).IsUnique().HasDatabaseName("UX_WhatsAppConfiguraciones_Tenant");
            entity.Property(e => e.NumeroWhatsApp).HasMaxLength(30);
            entity.Property(e => e.PhoneNumberId).HasMaxLength(100);
            entity.Property(e => e.BusinessAccountId).HasMaxLength(100);
            entity.Property(e => e.AccessTokenProtegido).HasMaxLength(4000);
            entity.Property(e => e.EstadoConexion).HasMaxLength(20).HasDefaultValue("No conectado");
            entity.Property(e => e.WebhookVerifyTokenProtegido).HasMaxLength(4000);
            entity.Property(e => e.FechaConexion).HasColumnType("datetime2");
            entity.HasOne(e => e.Tenant).WithMany().HasForeignKey(e => e.TenantId).OnDelete(DeleteBehavior.Cascade).HasConstraintName("FK_WhatsAppConfiguraciones_Tenants");
            entity.HasQueryFilter(e => EsSuperAdmin || e.TenantId == TenantIdActual);
        });

        modelBuilder.Entity<WhatsAppPlantilla>(entity =>
        {
            entity.ToTable("WhatsAppPlantillas");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Tipo).HasMaxLength(50);
            entity.Property(e => e.Contenido).HasMaxLength(4000);
            entity.Property(e => e.FechaActualizacion).HasColumnType("datetime2");
            entity.HasIndex(e => new { e.TenantId, e.Tipo }).IsUnique().HasDatabaseName("UX_WhatsAppPlantillas_Tenant_Tipo");
            entity.HasOne(e => e.Tenant).WithMany().HasForeignKey(e => e.TenantId).OnDelete(DeleteBehavior.Cascade).HasConstraintName("FK_WhatsAppPlantillas_Tenants");
            entity.HasQueryFilter(e => EsSuperAdmin || e.TenantId == TenantIdActual);
        });

        modelBuilder.Entity<WhatsAppMensajeHistorial>(entity =>
        {
            entity.ToTable("WhatsAppMensajesHistorial");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Telefono).HasMaxLength(30);
            entity.Property(e => e.Tipo).HasMaxLength(50);
            entity.Property(e => e.Mensaje).HasMaxLength(4000);
            entity.Property(e => e.Estado).HasMaxLength(30);
            entity.Property(e => e.Error).HasMaxLength(2000);
            entity.Property(e => e.MetaMessageId).HasMaxLength(200);
            entity.Property(e => e.RespuestaMeta).HasMaxLength(4000);
            entity.Property(e => e.Fecha).HasColumnType("datetime2");
            entity.HasIndex(e => new { e.TenantId, e.CitaId, e.Tipo }).IsUnique().HasFilter("[CitaId] IS NOT NULL").HasDatabaseName("UX_WhatsAppMensajesHistorial_Cita_Tipo");
            entity.HasIndex(e => new { e.TenantId, e.Fecha }).HasDatabaseName("IX_WhatsAppMensajesHistorial_Tenant_Fecha");
            entity.HasOne(e => e.Tenant).WithMany().HasForeignKey(e => e.TenantId).OnDelete(DeleteBehavior.Cascade).HasConstraintName("FK_WhatsAppMensajesHistorial_Tenants");
            entity.HasOne(e => e.Cita).WithMany().HasForeignKey(e => e.CitaId).OnDelete(DeleteBehavior.SetNull).HasConstraintName("FK_WhatsAppMensajesHistorial_Citas");
            entity.HasOne(e => e.Cliente).WithMany().HasForeignKey(e => e.ClienteId).OnDelete(DeleteBehavior.SetNull).HasConstraintName("FK_WhatsAppMensajesHistorial_Usuarios");
            entity.HasOne(e => e.Barbero).WithMany().HasForeignKey(e => e.BarberoId).OnDelete(DeleteBehavior.SetNull).HasConstraintName("FK_WhatsAppMensajesHistorial_Barberos");
            entity.HasQueryFilter(e => EsSuperAdmin || e.TenantId == TenantIdActual);
        });

        modelBuilder.Entity<TenantModulo>(entity =>
        {
            entity.ToTable("TenantModulos");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Codigo).HasMaxLength(50);
            entity.Property(e => e.FechaActualizacion).HasColumnType("datetime2");
            entity.HasIndex(e => new { e.TenantId, e.Codigo }).IsUnique().HasDatabaseName("UX_TenantModulos_Tenant_Codigo");
            entity.HasOne(e => e.Tenant).WithMany(t => t.Modulos).HasForeignKey(e => e.TenantId).OnDelete(DeleteBehavior.Cascade).HasConstraintName("FK_TenantModulos_Tenants");
            entity.HasQueryFilter(e => EsSuperAdmin || e.TenantId == TenantIdActual);
        });

        modelBuilder.Entity<Cita>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__Citas__3214EC07E78AC117");
            // Este índice permite que la validación serializable de agenda tome bloqueos
            // de rango por barbero, sin mezclar reservas entre tenants.
            entity.HasIndex(e => new { e.TenantId, e.BarberoId, e.FechaHora })
                .HasDatabaseName("IX_Citas_Agenda_Tenant_Barbero_Fecha");
            entity.HasIndex(e => new { e.TenantId, e.ClienteId, e.FechaHora })
                .HasDatabaseName("IX_Citas_Agenda_Tenant_Cliente_Fecha");
            entity.Property(e => e.Estado).HasMaxLength(50).IsUnicode(false).HasDefaultValue("Pendiente");
            entity.Property(e => e.FechaHora).HasColumnType("datetime");
            entity.Property(e => e.Notas).HasMaxLength(255).IsUnicode(false);

            entity.HasOne(d => d.Barbero).WithMany(p => p.Cita)
                .HasForeignKey(d => d.BarberoId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Citas_Barberos");

            entity.HasOne(d => d.Cliente).WithMany(p => p.Cita)
                .HasForeignKey(d => d.ClienteId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Citas_Clientes");

            entity.HasOne(d => d.Servicio).WithMany(p => p.Cita)
                .HasForeignKey(d => d.ServicioId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Citas_Servicios");

            entity.HasOne(d => d.Tenant).WithMany(p => p.Cita)
                .HasForeignKey(d => d.TenantId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Citas_Tenants");
        });

        modelBuilder.Entity<Factura>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__Facturas__3214EC0702F9FDB8");
            entity.Property(e => e.FechaPago).HasDefaultValueSql("(getdate())").HasColumnType("datetime");
            entity.Property(e => e.MetodoPago).HasMaxLength(50).IsUnicode(false);
            entity.Property(e => e.Total).HasColumnType("decimal(18, 2)");

            entity.HasOne(d => d.Cita).WithMany(p => p.Facturas)
                .HasForeignKey(d => d.CitaId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Facturas_Citas");

            entity.HasOne(d => d.Tenant).WithMany(p => p.Facturas)
                .HasForeignKey(d => d.TenantId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Facturas_Tenants");
        });

        modelBuilder.Entity<Role>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__Roles__3214EC070C7019EE");
            entity.Property(e => e.NombreRole).HasMaxLength(50).IsUnicode(false);
        });

        modelBuilder.Entity<Servicio>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__Servicio__3214EC07D6D4545F");
            entity.Property(e => e.Nombre).HasMaxLength(100).IsUnicode(false);
            entity.Property(e => e.Precio).HasColumnType("decimal(18, 2)");

            entity.HasOne(d => d.Tenant).WithMany(p => p.Servicios)
                .HasForeignKey(d => d.TenantId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Servicios_Tenants");
        });

        modelBuilder.Entity<Tenant>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__Tenants__3214EC0749AD610E");
            entity.HasIndex(e => e.Subdominio, "UQ__Tenants__3D6948B3947E2614").IsUnique();
            entity.Property(e => e.Activo).HasDefaultValue(true);
            entity.Property(e => e.FechaRegistro).HasDefaultValueSql("(getdate())").HasColumnType("datetime");
            entity.Property(e => e.NombreNegocio).HasMaxLength(150).IsUnicode(false);
            entity.Property(e => e.PlanSuscripcion).HasMaxLength(50).IsUnicode(false).HasDefaultValue("Bronce");
            entity.Property(e => e.Subdominio).HasMaxLength(50).IsUnicode(false);
            entity.Property(e => e.Slug).HasMaxLength(150).IsUnicode(false);
            entity.Property(e => e.TieneModuloWhatsApp).HasDefaultValue(false);
        });

        modelBuilder.Entity<Usuario>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__Usuarios__3214EC072953775D");
            entity.HasIndex(e => e.Email, "UQ__Usuarios__A9D10534E20A66C5").IsUnique();
            entity.Property(e => e.Activo).HasDefaultValue(true);
            entity.Property(e => e.Email).HasMaxLength(150).IsUnicode(false);
            entity.Property(e => e.Nombre).HasMaxLength(100).IsUnicode(false);
            entity.Property(e => e.PasswordHash).HasMaxLength(255).IsUnicode(false);
            entity.Property(e => e.ResetToken).HasMaxLength(255).IsUnicode(false);
            entity.Property(e => e.Telefono).HasMaxLength(20).IsUnicode(false);
            entity.Property(e => e.TokenExpiracion).HasColumnType("datetime");
            entity.Property(e => e.TokenUsado).HasDefaultValue(false);

            entity.HasOne(d => d.Role).WithMany(p => p.Usuarios)
                .HasForeignKey(d => d.RoleId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Usuarios_Roles");

            entity.HasOne(d => d.Tenant).WithMany(p => p.Usuarios)
                .HasForeignKey(d => d.TenantId)
                .HasConstraintName("FK_Usuarios_Tenants");
        });

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
