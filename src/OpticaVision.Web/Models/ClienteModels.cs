using System.ComponentModel.DataAnnotations;

namespace OpticaVision.Web.Models;

public sealed record ClienteRecord(
    long Id,
    string Codigo,
    string Nombres,
    string Apellidos,
    string? Telefono,
    string? Correo,
    string? Direccion,
    bool Activo,
    DateTimeOffset CreadoEn);

public sealed class ClienteForm
{
    [Required(ErrorMessage = "Escriba los nombres.")]
    [StringLength(100, ErrorMessage = "Máximo 100 caracteres.")]
    [RegularExpression(@"\p{L}[\p{L}\p{M}]*(?:[ \x27\u2019-]\p{L}[\p{L}\p{M}]*)*", ErrorMessage = "Usa letras para el nombre. Puedes incluir espacios, guiones y apóstrofos.")]
    [Display(Name = "Nombres")]
    public string Nombres { get; set; } = string.Empty;

    [Required(ErrorMessage = "Escriba los apellidos.")]
    [StringLength(100, ErrorMessage = "Máximo 100 caracteres.")]
    [RegularExpression(@"\p{L}[\p{L}\p{M}]*(?:[ \x27\u2019-]\p{L}[\p{L}\p{M}]*)*", ErrorMessage = "Usa letras para el nombre. Puedes incluir espacios, guiones y apóstrofos.")]
    [Display(Name = "Apellidos")]
    public string Apellidos { get; set; } = string.Empty;

    [StringLength(30, ErrorMessage = "Máximo 30 caracteres.")]
    [Display(Name = "Teléfono")]
    public string? Telefono { get; set; }

    [EmailAddress(ErrorMessage = "Escriba un correo válido.")]
    [StringLength(254, ErrorMessage = "Máximo 254 caracteres.")]
    [Display(Name = "Correo electrónico")]
    public string? Correo { get; set; }

    [StringLength(500, ErrorMessage = "Máximo 500 caracteres.")]
    [Display(Name = "Dirección")]
    public string? Direccion { get; set; }
}

public sealed class ClientesIndexViewModel
{
    public string? Busqueda { get; init; }
    public bool IncluirInactivos { get; init; }
    public required IReadOnlyList<ClienteRecord> Clientes { get; init; }
}

