using Npgsql;

namespace OpticaVision.Web.Data;

public sealed class OpticaDatabase : IDisposable
{
    private readonly NpgsqlDataSource? _dataSource;

    public OpticaDatabase(IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("OpticaPostgres");
        if (string.IsNullOrWhiteSpace(connectionString))
            return;

        var settings = new NpgsqlConnectionStringBuilder(connectionString)
        {
            SslMode = SslMode.VerifyFull,
            Pooling = true,
            MaxPoolSize = 10,
            Timeout = 15,
            CommandTimeout = 30
        };

        _dataSource = NpgsqlDataSource.Create(settings.ConnectionString);
    }

    public bool IsConfigured => _dataSource is not null;

    public NpgsqlDataSource DataSource => _dataSource
        ?? throw new DatabaseNotConfiguredException();

    public void Dispose() => _dataSource?.Dispose();
}

public sealed class DatabaseNotConfiguredException : Exception
{
    public DatabaseNotConfiguredException() : base("Falta configurar la conexión segura de PostgreSQL para Óptica Visión.") { }
}
