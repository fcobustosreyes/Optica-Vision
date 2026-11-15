using OpticaVision.Web.Models;

namespace OpticaVision.Web.Data;

public interface IClienteRepository
{
    Task<IReadOnlyList<ClienteRecord>> ListarAsync(string? busqueda, bool incluirInactivos, CancellationToken cancellationToken);
    Task<ClienteRecord?> ObtenerAsync(long id, CancellationToken cancellationToken);
    Task<ClienteRecord> CrearAsync(ClienteForm form, Guid actorPerfilId, CancellationToken cancellationToken);
    Task<bool> ActualizarAsync(long id, ClienteForm form, Guid actorPerfilId, CancellationToken cancellationToken);
    Task<bool> CambiarEstadoAsync(long id, bool activo, Guid actorPerfilId, CancellationToken cancellationToken);
}
