namespace OpticaVision.Web.Data.Entities;

public sealed class AuditoriaEventoEntity
{
    public long Id { get; set; }
    public Guid? ActorPerfilId { get; set; }
    public string Tipo { get; set; } = string.Empty;
    public string Entidad { get; set; } = string.Empty;
    public long? EntidadId { get; set; }
    public string Detalles { get; set; } = "{}";
    public DateTimeOffset RegistradoEn { get; set; }
}
