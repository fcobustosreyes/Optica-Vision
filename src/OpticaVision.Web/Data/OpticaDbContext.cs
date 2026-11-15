using Microsoft.EntityFrameworkCore;
using OpticaVision.Web.Data.Entities;

namespace OpticaVision.Web.Data;

public sealed class OpticaDbContext(DbContextOptions<OpticaDbContext> options) : DbContext(options)
{
    public DbSet<ClienteEntity> Clientes => Set<ClienteEntity>();
    public DbSet<AuditoriaEventoEntity> AuditoriaEventos => Set<AuditoriaEventoEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var clientes = modelBuilder.Entity<ClienteEntity>();
        clientes.ToTable("clientes", "optica");
        clientes.HasKey(x => x.Id);
        clientes.Property(x => x.Id).HasColumnName("id").ValueGeneratedOnAdd();
        clientes.Property(x => x.Codigo).HasColumnName("codigo").HasMaxLength(30).IsRequired();
        clientes.HasIndex(x => x.Codigo).IsUnique();
        clientes.Property(x => x.Nombres).HasColumnName("nombres").HasMaxLength(100).IsRequired();
        clientes.Property(x => x.Apellidos).HasColumnName("apellidos").HasMaxLength(100).IsRequired();
        clientes.Property(x => x.Telefono).HasColumnName("telefono").HasMaxLength(30);
        clientes.Property(x => x.Correo).HasColumnName("correo").HasMaxLength(254);
        clientes.Property(x => x.Direccion).HasColumnName("direccion");
        clientes.Property(x => x.Activo).HasColumnName("activo").HasDefaultValue(true);
        clientes.Property(x => x.CreadoEn).HasColumnName("creado_en").HasColumnType("timestamp with time zone").HasDefaultValueSql("now()");

        var auditoria = modelBuilder.Entity<AuditoriaEventoEntity>();
        auditoria.ToTable("auditoria_eventos", "optica");
        auditoria.HasKey(x => x.Id);
        auditoria.Property(x => x.Id).HasColumnName("id").ValueGeneratedOnAdd();
        auditoria.Property(x => x.ActorPerfilId).HasColumnName("actor_perfil_id");
        auditoria.Property(x => x.Tipo).HasColumnName("tipo").HasMaxLength(60).IsRequired();
        auditoria.Property(x => x.Entidad).HasColumnName("entidad").HasMaxLength(80).IsRequired();
        auditoria.Property(x => x.EntidadId).HasColumnName("entidad_id");
        auditoria.Property(x => x.Detalles).HasColumnName("detalles").HasColumnType("jsonb").HasDefaultValueSql("'{}'::jsonb");
        auditoria.Property(x => x.RegistradoEn).HasColumnName("registrado_en").HasColumnType("timestamp with time zone").HasDefaultValueSql("now()");
        auditoria.HasIndex(x => new { x.Entidad, x.EntidadId, x.RegistradoEn });
    }
}
