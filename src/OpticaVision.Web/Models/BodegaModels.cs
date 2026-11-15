namespace OpticaVision.Web.Models;

public sealed record KardexRow(
    DateTimeOffset Fecha,
    string Bodega,
    string CodigoProducto,
    string Producto,
    string TipoMovimiento,
    long Entrada,
    long Salida,
    long Saldo,
    string Referencia,
    string Responsable);

public sealed class KardexViewModel
{
    public required string Busqueda { get; init; }
    public required IReadOnlyList<KardexRow> Movimientos { get; init; }
}

public sealed record RecepcionRow(
    long Id,
    DateTimeOffset Fecha,
    string Proveedor,
    string Bodega,
    string Estado,
    long Unidades,
    string Productos,
    string Responsable);

public sealed class RecepcionesViewModel
{
    public required string Busqueda { get; init; }
    public required IReadOnlyList<RecepcionRow> Recepciones { get; init; }
}
