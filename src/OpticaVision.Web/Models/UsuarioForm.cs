using System.ComponentModel.DataAnnotations;

namespace OpticaVision.Web.Models;

public sealed class UsuarioForm
{
    [Required(ErrorMessage = "Escriba el nombre completo.")]
    [StringLength(150)]
    [Display(Name = "Nombre completo")]
    public string NombreCompleto { get; set; } = string.Empty;

    [Required(ErrorMessage = "Escriba el correo electrónico.")]
    [EmailAddress(ErrorMessage = "Escriba un correo válido.")]
    [StringLength(254)]
    [Display(Name = "Correo electrónico")]
    public string Correo { get; set; } = string.Empty;

    [Required(ErrorMessage = "Seleccione un rol.")]
    [Display(Name = "Rol")]
    public string Rol { get; set; } = "ventas";

    [Required(ErrorMessage = "Escriba una contraseña temporal.")]
    [StringLength(128, MinimumLength = 12, ErrorMessage = "Use de 12 a 128 caracteres.")]
    [DataType(DataType.Password)]
    [Display(Name = "Contraseña temporal")]
    public string ContrasenaTemporal { get; set; } = string.Empty;

    [Required(ErrorMessage = "Confirme la contraseña.")]
    [Compare(nameof(ContrasenaTemporal), ErrorMessage = "Las contraseñas no coinciden.")]
    [DataType(DataType.Password)]
    [Display(Name = "Confirmar contraseña")]
    public string ConfirmarContrasena { get; set; } = string.Empty;
}
