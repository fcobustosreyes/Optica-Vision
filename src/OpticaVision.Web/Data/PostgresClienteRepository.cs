using Npgsql;
using OpticaVision.Web.Models;

namespace OpticaVision.Web.Data;

public sealed class PostgresClienteRepository(OpticaDatabase database) : IClienteRepository
{
    private const string Campos = "id, codigo, nombres, apellidos, telefono, correo, direccion, activo, creado_en";

    public async Task<IReadOnlyList<ClienteRecord>> ListarAsync(
        string? busqueda,
        bool incluirInactivos,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT id, codigo, nombres, apellidos, telefono, correo, direccion, activo, creado_en
            FROM optica.clientes
            WHERE (@incluir_inactivos OR activo)
              AND (codigo ILIKE @patron OR nombres ILIKE @patron
                   OR apellidos ILIKE @patron OR telefono ILIKE @patron OR correo ILIKE @patron)
            ORDER BY activo DESC, apellidos, nombres, id
            LIMIT 250;
            """;

        await using var connection = await database.DataSource.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand(sql, connection);
        var texto = string.IsNullOrWhiteSpace(busqueda) ? null : busqueda.Trim();
        command.Parameters.AddWithValue("incluir_inactivos", incluirInactivos);
        command.Parameters.AddWithValue("patron", $"%{texto ?? string.Empty}%");

        var resultado = new List<ClienteRecord>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
            resultado.Add(LeerCliente(reader));

        return resultado;
    }

    public async Task<ClienteRecord?> ObtenerAsync(long id, CancellationToken cancellationToken)
    {
        var sql = $"SELECT {Campos} FROM optica.clientes WHERE id = @id;";
        await using var connection = await database.DataSource.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("id", id);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken) ? LeerCliente(reader) : null;
    }

    public async Task<ClienteRecord> CrearAsync(ClienteForm form, Guid actorPerfilId, CancellationToken cancellationToken)
    {
        const string sql = """
            INSERT INTO optica.clientes (codigo, nombres, apellidos, telefono, correo, direccion)
            VALUES (@codigo, @nombres, @apellidos, @telefono, @correo, @direccion)
            RETURNING id, codigo, nombres, apellidos, telefono, correo, direccion, activo, creado_en;
            """;

        await using var connection = await database.DataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await using var command = new NpgsqlCommand(sql, connection, transaction);
        AgregarCampos(command, form);
        command.Parameters.AddWithValue("codigo", $"CLI-{Guid.NewGuid():N}"[..16].ToUpperInvariant());

        ClienteRecord cliente;
        await using (var reader = await command.ExecuteReaderAsync(cancellationToken))
        {
            if (!await reader.ReadAsync(cancellationToken))
                throw new InvalidOperationException("No se pudo crear el registro del cliente.");
            cliente = LeerCliente(reader);
        }
        await RegistrarAuditoriaAsync(connection, transaction, actorPerfilId, "cliente.creado", cliente.Id, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return cliente;
    }

    public async Task<bool> ActualizarAsync(long id, ClienteForm form, Guid actorPerfilId, CancellationToken cancellationToken)
    {
        const string sql = """
            UPDATE optica.clientes
            SET nombres = @nombres, apellidos = @apellidos, telefono = @telefono,
                correo = @correo, direccion = @direccion
            WHERE id = @id;
            """;

        await using var connection = await database.DataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("id", id);
        AgregarCampos(command, form);
        if (await command.ExecuteNonQueryAsync(cancellationToken) != 1)
            return false;
        await RegistrarAuditoriaAsync(connection, transaction, actorPerfilId, "cliente.actualizado", id, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return true;
    }

    public async Task<bool> CambiarEstadoAsync(long id, bool activo, Guid actorPerfilId, CancellationToken cancellationToken)
    {
        const string sql = "UPDATE optica.clientes SET activo = @activo WHERE id = @id AND activo IS DISTINCT FROM @activo;";
        await using var connection = await database.DataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("id", id);
        command.Parameters.AddWithValue("activo", activo);
        if (await command.ExecuteNonQueryAsync(cancellationToken) != 1)
            return false;
        var tipo = activo ? "cliente.reactivado" : "cliente.desactivado";
        await RegistrarAuditoriaAsync(connection, transaction, actorPerfilId, tipo, id, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return true;
    }

    private static async Task RegistrarAuditoriaAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid actorPerfilId,
        string tipo,
        long entidadId,
        CancellationToken cancellationToken)
    {
        const string sql = """
            INSERT INTO optica.auditoria_eventos(actor_perfil_id, tipo, entidad, entidad_id, detalles)
            VALUES (@actor, @tipo, 'clientes', @entidad_id, '{}'::jsonb);
            """;
        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("actor", actorPerfilId);
        command.Parameters.AddWithValue("tipo", tipo);
        command.Parameters.AddWithValue("entidad_id", entidadId);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static void AgregarCampos(NpgsqlCommand command, ClienteForm form)
    {
        command.Parameters.AddWithValue("nombres", form.Nombres.Trim());
        command.Parameters.AddWithValue("apellidos", form.Apellidos.Trim());
        command.Parameters.AddWithValue("telefono", (object?)form.Telefono?.Trim() ?? DBNull.Value);
        command.Parameters.AddWithValue("correo", (object?)form.Correo?.Trim() ?? DBNull.Value);
        command.Parameters.AddWithValue("direccion", (object?)form.Direccion?.Trim() ?? DBNull.Value);
    }

    private static ClienteRecord LeerCliente(NpgsqlDataReader reader) => new(
        reader.GetInt64(0), reader.GetString(1), reader.GetString(2), reader.GetString(3),
        reader.IsDBNull(4) ? null : reader.GetString(4),
        reader.IsDBNull(5) ? null : reader.GetString(5),
        reader.IsDBNull(6) ? null : reader.GetString(6),
        reader.GetBoolean(7), new DateTimeOffset(reader.GetDateTime(8)));
}
