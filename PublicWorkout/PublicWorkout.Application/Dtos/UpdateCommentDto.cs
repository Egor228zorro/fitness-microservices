using System.ComponentModel.DataAnnotations;
using Swashbuckle.AspNetCore.Annotations;

namespace PublicWorkout.Application.Dtos;

[SwaggerSchema("DTO for updating a comment.")]
public record UpdateCommentDto(
    [Required] [SwaggerSchema("The updated text of the comment.")] string Text
);
