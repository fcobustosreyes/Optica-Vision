using Microsoft.EntityFrameworkCore;
using OpticaVision.Web.Data.Entities;
using OpticaVision.Web.Models;

namespace OpticaVision.Web.Data;

public sealed class EfClienteRepository(IDbContextFactory<OpticaDbContext> contextFactory) : IClienteRepository
{
    public async Task<IReadOnlyList<ClienteRecord>> ListarAsync(
        string? busqueda,
        bool incluirInactivos,
        CancellationToken cancellationToken)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        var query = db.Clientes.AsNoTracking();
        if (!incluirInactivos)
            query = query.Where(x => x.Activo);

        if (!string.IsNullOrWhiteSpace(busqueda))
        {
            var patron = $"%{busqueda.Trim()}%";
            query = query.Where(x =>
                EF.Functions.ILike(x.Codigo, patron)
                || EF.Functions.ILike(x.Nombres, patron)
                || EF.Functions.ILike(x.Apellidos, patron)
                || (x.Telefono != null && EF.Functions.ILike(x.Telefono, patron))
                || (x.Correo != null && EF.Functions.ILike(x.Correo, patron)));
        }

        var rows = await query
            .OrderByDescending(x => x.Activo)
            .ThenBy(x => x.Apellidos)
            .ThenBy(x => x.Nombres)
            .ThenBy(x => x.Id)
            .Take(250)
            .ToListAsync(cancellationToken);

        return rows.Select(Map).ToArray();
    }

    public async Task<ClienteRecord?> ObtenerAsync(long id, CancellationToken cancellationToken)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        var row = await db.Clientes.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
        return row is null ? null : Map(row);
    }

    public async Task<ClienteRecord> CrearAsync(ClienteForm form, Guid actorPerfilId, CancellationToken cancellationToken)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);

        var row = new ClienteEntity
        {
            Codigo = $"CLI-{Guid.NewGuid():N}"[..16].ToUpperInvariant(),
            Nombres = form.Nombres.Trim(),
            Apellidos = form.Apellidos.Trim(),
            Telefono = Normalize(form.Telefono),
            Correo = Normalize(form.Correo),
            Direccion = Normalize(form.Direccion),
            Activo = true
        };
        db.Clientes.Add(row);
        await db.SaveChangesAsync(cancellationToken);
        db.AuditoriaEventos.Add(Audit("cliente.creado", row.Id, actorPerfilId));
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return Map(row);
    }

    public async Task<bool> ActualizarAsync(long id, ClienteForm form, Guid actorPerfilId, CancellationToken cancellationToken)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var row = await db.Clientes.SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (row is null)
            return false;

        row.Nombres = form.Nombres.Trim();
        row.Apellidos = form.Apellidos.Trim();
        row.Telefono = Normalize(form.Telefono);
        row.Correo = Normalize(form.Correo);
        row.Direccion = Normalize(form.Direccion);
        db.AuditoriaEventos.Add(Audit("cliente.actualizado", row.Id, actorPerfilId));
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return true;
    }

    public async Task<bool> CambiarEstadoAsync(long id, bool activo, Guid actorPerfilId, CancellationToken cancellationToken)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var row = await db.Clientes.SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (row is null || row.Activo == activo)
            return false;

        row.Activo = activo;
        db.AuditoriaEventos.Add(Audit(activo ? "cliente.reactivado" : "cliente.desactivado", row.Id, actorPerfilId));
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return true;
    }

    private static AuditoriaEventoEntity Audit(string tipo, long id, Guid actorPerfilId) => new()
    {
        ActorPerfilId = actorPerfilId,
        Tipo = tipo,
        Entidad = "clientes",
        EntidadId = id,
        Detalles = "{}"
    };

    private static string? Normalize(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static ClienteRecord Map(ClienteEntity row) => new(
        row.Id, row.Codigo, row.Nombres, row.Apellidos, row.Telefono, row.Correo, row.Direccion,
        row.Activo, row.CreadoEn);
}
