using Npgsql;
using OpticaVision.Web.Data;

namespace OpticaVision.Web.Auth;

public sealed record PerfilAutenticado(Guid AuthUserId, Guid PerfilId, string NombreCompleto, string Correo, string Rol, bool DebeCambiarContrasena);
public sealed record PerfilLista(Guid PerfilId, string NombreCompleto, string Correo, string Rol, bool Activo, DateTimeOffset CreadoEn);

public interface IPerfilRepository
{
    Task<PerfilAutenticado?> ObtenerActivoAsync(Guid authUserId, CancellationToken cancellationToken);
    Task<bool> ExisteSuperadministradorAsync(CancellationToken cancellationToken);
    Task<Guid> CrearAsync(Guid authUserId, string correo, string nombreCompleto, string rol, Guid actorPerfilId, CancellationToken cancellationToken);
    Task<Guid> CrearPrimerSuperadministradorAsync(Guid authUserId, string correo, string nombreCompleto, CancellationToken cancellationToken);
    Task<IReadOnlyList<PerfilLista>> ListarAsync(bool esPropietario, CancellationToken cancellationToken);
    Task<bool> CambiarEstadoAsync(Guid perfilId, bool activo, Guid actorPerfilId, bool esPropietario, CancellationToken cancellationToken);
    Task MarcarContrasenaActualizadaAsync(Guid authUserId, CancellationToken cancellationToken);
}

public sealed class PostgresPerfilRepository(OpticaDatabase database) : IPerfilRepository
{
    public async Task<PerfilAutenticado?> ObtenerActivoAsync(Guid authUserId, CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT p.auth_user_id, p.id, p.nombre_completo, p.correo_login, r.nombre, p.debe_cambiar_contrasena
            FROM optica.perfiles AS p
            INNER JOIN optica.roles AS r ON r.id = p.rol_id
            WHERE p.auth_user_id = @auth_user_id AND p.activo = true AND r.activo = true
            LIMIT 1;
            """;
        await using var connection = await database.DataSource.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("auth_user_id", authUserId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
            return null;
        return new PerfilAutenticado(reader.GetGuid(0), reader.GetGuid(1), reader.GetString(2), reader.GetString(3), reader.GetString(4), reader.GetBoolean(5));
    }

    public async Task<bool> ExisteSuperadministradorAsync(CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT EXISTS (
                SELECT 1 FROM optica.perfiles p
                JOIN optica.roles r ON r.id = p.rol_id
                WHERE p.activo = true AND r.activo = true AND r.nombre = 'superadministrador'
            );
            """;
        await using var connection = await database.DataSource.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand(sql, connection);
        return (bool)(await command.ExecuteScalarAsync(cancellationToken) ?? false);
    }

    public async Task<Guid> CrearAsync(Guid authUserId, string correo, string nombreCompleto, string rol, Guid actorPerfilId, CancellationToken cancellationToken)
    {
        const string sql = """
            INSERT INTO optica.perfiles (auth_user_id, correo_login, rol_id, nombre_completo, debe_cambiar_contrasena)
            SELECT @auth_user_id, @correo, r.id, @nombre, true
            FROM optica.roles r
            WHERE r.nombre = @rol AND r.activo = true
            RETURNING id;
            """;
        await using var connection = await database.DataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("auth_user_id", authUserId);
        command.Parameters.AddWithValue("correo", correo.Trim());
        command.Parameters.AddWithValue("nombre", nombreCompleto.Trim());
        command.Parameters.AddWithValue("rol", rol);
        var result = await command.ExecuteScalarAsync(cancellationToken);
        if (result is not Guid profileId)
            throw new InvalidOperationException("El rol solicitado no existe o esta desactivado.");
        await RegistrarAuditoriaAsync(connection, transaction, actorPerfilId, "usuario.creado", profileId, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return profileId;
    }

    public async Task<Guid> CrearPrimerSuperadministradorAsync(Guid authUserId, string correo, string nombreCompleto, CancellationToken cancellationToken)
    {
        await using var connection = await database.DataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await using (var lockCommand = new NpgsqlCommand("SELECT pg_advisory_xact_lock(726104, 1);", connection, transaction))
            await lockCommand.ExecuteNonQueryAsync(cancellationToken);

        const string existsSql = """
            SELECT EXISTS (
                SELECT 1 FROM optica.perfiles p
                JOIN optica.roles r ON r.id = p.rol_id
                WHERE p.activo = true AND r.activo = true AND r.nombre = 'superadministrador'
            );
            """;
        await using (var existsCommand = new NpgsqlCommand(existsSql, connection, transaction))
        {
            if ((bool)(await existsCommand.ExecuteScalarAsync(cancellationToken) ?? false))
                throw new SetupAlreadyCompletedException();
        }

        const string insertSql = """
            INSERT INTO optica.perfiles (auth_user_id, correo_login, rol_id, nombre_completo, debe_cambiar_contrasena)
            SELECT @auth_user_id, @correo, id, @nombre, false
            FROM optica.roles
            WHERE nombre = 'superadministrador' AND activo = true
            RETURNING id;
            """;
        await using var insertCommand = new NpgsqlCommand(insertSql, connection, transaction);
        insertCommand.Parameters.AddWithValue("auth_user_id", authUserId);
        insertCommand.Parameters.AddWithValue("correo", correo.Trim());
        insertCommand.Parameters.AddWithValue("nombre", nombreCompleto.Trim());
        var result = await insertCommand.ExecuteScalarAsync(cancellationToken);
        if (result is not Guid profileId)
            throw new InvalidOperationException("No esta creado el rol activo superadministrador.");

        await RegistrarAuditoriaAsync(connection, transaction, null, "usuario.superadministrador_inicial", profileId, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return profileId;
    }

    public async Task<IReadOnlyList<PerfilLista>> ListarAsync(bool esPropietario, CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT p.id, p.nombre_completo, COALESCE(p.correo_login, ''), r.nombre, p.activo, p.creado_en
            FROM optica.perfiles p
            JOIN optica.roles r ON r.id = p.rol_id
            WHERE @propietario OR r.nombre <> 'superadministrador'
            ORDER BY p.activo DESC, p.nombre_completo, p.id;
            """;
        await using var connection = await database.DataSource.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("propietario", esPropietario);
        var result = new List<PerfilLista>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
            result.Add(new PerfilLista(reader.GetGuid(0), reader.GetString(1), reader.GetString(2), reader.GetString(3), reader.GetBoolean(4), new DateTimeOffset(reader.GetDateTime(5))));
        return result;
    }

    public async Task<bool> CambiarEstadoAsync(Guid perfilId, bool activo, Guid actorPerfilId, bool esPropietario, CancellationToken cancellationToken)
    {
        await using var connection = await database.DataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await using (var advisoryLock = new NpgsqlCommand("SELECT pg_advisory_xact_lock(726104, 1);", connection, transaction))
            await advisoryLock.ExecuteNonQueryAsync(cancellationToken);
        const string lockSql = "SELECT r.nombre FROM optica.perfiles p JOIN optica.roles r ON r.id=p.rol_id WHERE p.id=@id FOR UPDATE OF p;";
        await using var lockCommand = new NpgsqlCommand(lockSql, connection, transaction);
        lockCommand.Parameters.AddWithValue("id", perfilId);
        var role = await lockCommand.ExecuteScalarAsync(cancellationToken) as string;
        if (role is null || (!esPropietario && role is not ("recepcion" or "ventas" or "bodega" or "oftalmologo")))
            return false;
        if (!activo && role == "superadministrador")
        {
            const string countSql = "SELECT count(*) FROM optica.perfiles p JOIN optica.roles r ON r.id=p.rol_id WHERE p.activo AND r.activo AND r.nombre='superadministrador';";
            await using var countCommand = new NpgsqlCommand(countSql, connection, transaction);
            if (Convert.ToInt32(await countCommand.ExecuteScalarAsync(cancellationToken)) <= 1)
                throw new LastSuperadministratorException();
        }

        const string updateSql = "UPDATE optica.perfiles SET activo=@activo WHERE id=@id AND activo IS DISTINCT FROM @activo;";
        await using var updateCommand = new NpgsqlCommand(updateSql, connection, transaction);
        updateCommand.Parameters.AddWithValue("id", perfilId);
        updateCommand.Parameters.AddWithValue("activo", activo);
        if (await updateCommand.ExecuteNonQueryAsync(cancellationToken) != 1)
            return false;

        var type = activo ? "usuario.reactivado" : "usuario.desactivado";
        await RegistrarAuditoriaAsync(connection, transaction, actorPerfilId, type, perfilId, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return true;
    }

    public async Task MarcarContrasenaActualizadaAsync(Guid authUserId, CancellationToken cancellationToken)
    {
        const string sql = "UPDATE optica.perfiles SET debe_cambiar_contrasena=false WHERE auth_user_id=@auth_user_id AND activo=true;";
        await using var connection = await database.DataSource.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("auth_user_id", authUserId);
        if (await command.ExecuteNonQueryAsync(cancellationToken) != 1)
            throw new InvalidOperationException("No se pudo actualizar el estado de la cuenta.");
    }

    private static async Task RegistrarAuditoriaAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, Guid? actorPerfilId, string tipo, Guid perfilId, CancellationToken cancellationToken)
    {
        const string sql = """
            INSERT INTO optica.auditoria_eventos(actor_perfil_id, tipo, entidad, detalles)
            VALUES (@actor, @tipo, 'perfiles', jsonb_build_object('perfil_id', @perfil_id));
            """;
        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("actor", (object?)actorPerfilId ?? DBNull.Value);
        command.Parameters.AddWithValue("tipo", tipo);
        command.Parameters.AddWithValue("perfil_id", perfilId);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}

public sealed class SetupAlreadyCompletedException : Exception { }
public sealed class LastSuperadministratorException : Exception { }


