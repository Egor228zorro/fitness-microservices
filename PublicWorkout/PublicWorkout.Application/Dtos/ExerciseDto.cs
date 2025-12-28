using System.ComponentModel.DataAnnotations;
using Swashbuckle.AspNetCore.Annotations;

namespace PublicWorkout.Application.Dtos;

[SwaggerSchema("DTO for an exercise in a workout.")]
public abstract record ExerciseDto(
    [Required]
    [SwaggerSchema("The unique identifier of the exercise.")]
        Guid ExerciseId,
    [Required] [SwaggerSchema("The name of the exercise.")] string Name,
    [SwaggerSchema("The description of the exercise.")] string? Description,
    [SwaggerSchema("The URL of the media associated with the exercise.")]
        string? MediaUrl,
    [Required]
    [SwaggerSchema("The order index of the exercise in the workout.")]
        int OrderIndex,
    [Required]
    [SwaggerSchema("The duration of the exercise in seconds.")]
        int DurationSeconds
);
