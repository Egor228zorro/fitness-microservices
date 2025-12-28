using System.ComponentModel.DataAnnotations;
using Swashbuckle.AspNetCore.Annotations;

namespace PublicWorkout.Application.Dtos;

[SwaggerSchema("DTO for synchronizing a private workout to the public catalog.")]
public class SyncPublicWorkoutDto
{
    [Required]
    [SwaggerSchema("The ID of the private workout to synchronize.")]
    public Guid PrivateWorkoutId { get; set; }

    [Required]
    [SwaggerSchema("The name of the workout.")]
    public string Name { get; set; } = string.Empty;

    [Required]
    [SwaggerSchema("The type of the workout.")]
    public string Type { get; set; } = string.Empty;

    [SwaggerSchema("The URL of the workout preview image.")]
    public string? PreviewUrl { get; set; }

    [SwaggerSchema("The list of exercises in the workout.")]
    public List<ExerciseDto> Exercises { get; set; } = new();
}
