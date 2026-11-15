using System.ComponentModel.DataAnnotations;

namespace OpticaVision.Web.Models;

public sealed class LoginForm
{
    [Required(ErrorMessage = "Escriba el correo electrónico.")]
    [EmailAddress(ErrorMessage = "Escriba un correo válido.")]
    [Display(Name = "Correo electrónico")]
    public string Correo { get; set; } = string.Empty;

    [Required(ErrorMessage = "Escriba la contraseña.")]
    [DataType(DataType.Password)]
    [Display(Name = "Contraseña")]
    public string Contrasena { get; set; } = string.Empty;

    public string? Retorno { get; set; }
}
