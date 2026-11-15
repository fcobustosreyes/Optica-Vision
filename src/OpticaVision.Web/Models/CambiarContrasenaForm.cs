using System.ComponentModel.DataAnnotations;

namespace OpticaVision.Web.Models;

public sealed class CambiarContrasenaForm
{
    [Required(ErrorMessage = "Escriba la nueva contraseña.")]
    [StringLength(128, MinimumLength = 12, ErrorMessage = "Use de 12 a 128 caracteres.")]
    [DataType(DataType.Password)]
    [Display(Name = "Nueva contraseña")]
    public string NuevaContrasena { get; set; } = string.Empty;

    [Required(ErrorMessage = "Confirme la contraseña.")]
    [Compare(nameof(NuevaContrasena), ErrorMessage = "Las contraseñas no coinciden.")]
    [DataType(DataType.Password)]
    [Display(Name = "Confirmar nueva contraseña")]
    public string ConfirmarContrasena { get; set; } = string.Empty;
}
