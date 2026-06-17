using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;
using SaaSBarberia.Models;

namespace SaaSBarberia.Data;

public partial class BarberSaaSDbContext : DbContext
{
    public BarberSaaSDbContext()
    {
    }

    public BarberSaaSDbContext(DbContextOptions<BarberSaaSDbContext> options)
        : base(options)
    {
    }

    public virtual DbSet<Barbero> Barberos { get; set; }

    public virtual DbSet<Cita> Citas { get; set; }

    public virtual DbSet<Factura> Facturas { get; set; }

    public virtual DbSet<Role> Roles { get; set; }

    public virtual DbSet<Servicio> Servicios { get; set; }

    public virtual DbSet<Tenant> Tenants { get; set; }

    public virtual DbSet<Usuario> Usuarios { get; set; }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder) { }
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Barbero>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__Barberos__3214EC07C28CA89D");

            entity.Property(e => e.ComisionPorcentaje)
                .HasDefaultValue(0.00m)
                .HasColumnType("decimal(5, 2)");
            entity.Property(e => e.Especialidad)
                .HasMaxLength(100)
                .IsUnicode(false);

            entity.HasOne(d => d.Tenant).WithMany(p => p.Barberos)
                .HasForeignKey(d => d.TenantId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Barberos_Tenants");

            entity.HasOne(d => d.Usuario).WithMany(p => p.Barberos)
                .HasForeignKey(d => d.UsuarioId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Barberos_Usuarios");
        });

        modelBuilder.Entity<Cita>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__Citas__3214EC07E78AC117");

            entity.Property(e => e.Estado)
                .HasMaxLength(50)
                .IsUnicode(false)
                .HasDefaultValue("Pendiente");
            entity.Property(e => e.FechaHora).HasColumnType("datetime");
            entity.Property(e => e.Notas)
                .HasMaxLength(255)
                .IsUnicode(false);

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

            entity.Property(e => e.FechaPago)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime");
            entity.Property(e => e.MetodoPago)
                .HasMaxLength(50)
                .IsUnicode(false);
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

            entity.Property(e => e.NombreRole)
                .HasMaxLength(50)
                .IsUnicode(false);
        });

        modelBuilder.Entity<Servicio>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__Servicio__3214EC07D6D4545F");

            entity.Property(e => e.Nombre)
                .HasMaxLength(100)
                .IsUnicode(false);
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
            entity.Property(e => e.FechaRegistro)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime");
            entity.Property(e => e.NombreNegocio)
                .HasMaxLength(150)
                .IsUnicode(false);
            entity.Property(e => e.PlanSuscripcion)
                .HasMaxLength(50)
                .IsUnicode(false)
                .HasDefaultValue("Bronce");
            entity.Property(e => e.Subdominio)
                .HasMaxLength(50)
                .IsUnicode(false);
        });

        modelBuilder.Entity<Usuario>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__Usuarios__3214EC072953775D");

            entity.HasIndex(e => e.Email, "UQ__Usuarios__A9D10534E20A66C5").IsUnique();

            entity.Property(e => e.Activo).HasDefaultValue(true);
            entity.Property(e => e.Email)
                .HasMaxLength(150)
                .IsUnicode(false);
            entity.Property(e => e.Nombre)
                .HasMaxLength(100)
                .IsUnicode(false);
            entity.Property(e => e.PasswordHash)
                .HasMaxLength(255)
                .IsUnicode(false);
            entity.Property(e => e.ResetToken)
                .HasMaxLength(255)
                .IsUnicode(false);
            entity.Property(e => e.Telefono)
                .HasMaxLength(20)
                .IsUnicode(false);
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
