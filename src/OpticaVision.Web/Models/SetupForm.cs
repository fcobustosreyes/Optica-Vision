using System.ComponentModel.DataAnnotations;

namespace OpticaVision.Web.Models;

public sealed class SetupForm
{
    [Required(ErrorMessage = "Escriba el nombre completo.")]
    [StringLength(150)]
    [Display(Name = "Nombre completo")]
    public string NombreCompleto { get; set; } = string.Empty;

    [Required(ErrorMessage = "Escriba el correo electrónico.")]
    [EmailAddress(ErrorMessage = "Escriba un correo válido.")]
    [Display(Name = "Correo electrónico")]
    public string Correo { get; set; } = string.Empty;

    [Required(ErrorMessage = "Escriba una contraseña de al menos 12 caracteres.")]
    [StringLength(128, MinimumLength = 12, ErrorMessage = "Use de 12 a 128 caracteres.")]
    [DataType(DataType.Password)]
    [Display(Name = "Contraseña inicial")]
    public string Contrasena { get; set; } = string.Empty;

    [Required(ErrorMessage = "Confirme la contraseña.")]
    [Compare(nameof(Contrasena), ErrorMessage = "Las contraseñas no coinciden.")]
    [DataType(DataType.Password)]
    [Display(Name = "Confirmar contraseña")]
    public string ConfirmarContrasena { get; set; } = string.Empty;

    [Required(ErrorMessage = "Escriba el código de configuración.")]
    [DataType(DataType.Password)]
    [Display(Name = "Código de configuración")]
    public string TokenConfiguracion { get; set; } = string.Empty;
}
