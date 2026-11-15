using System.ComponentModel.DataAnnotations;
namespace OpticaVision.Web.Models;

public sealed class CitaForm
{
    [Required(ErrorMessage = "Selecciona la fecha y la hora.")]
    [Display(Name = "Fecha y hora en Nicaragua")]
    public DateTime? Fecha { get; set; }

    [Required(ErrorMessage = "Selecciona un oftalmólogo.")]
    [Range(1,long.MaxValue,ErrorMessage = "Selecciona un oftalmólogo válido.")]
    [Display(Name = "Oftalmólogo")]
    public long? OftalmologoId { get; set; }

    [Required(ErrorMessage = "Escribe el servicio.")]
    [StringLength(120)]
    [RegularExpression(@".*\p{L}.*", ErrorMessage = "Incluye el nombre del servicio.")]
    public string Servicio { get; set; } = "";
}

