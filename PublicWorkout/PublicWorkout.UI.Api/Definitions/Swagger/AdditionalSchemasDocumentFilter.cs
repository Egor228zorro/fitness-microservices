using System.Collections.Generic;
using Microsoft.OpenApi.Models;
using PublicWorkout.Application.Dtos;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace PublicWorkout.UI.Api.Definitions.Swagger;

public class AdditionalSchemasDocumentFilter : IDocumentFilter
{
    public void Apply(OpenApiDocument swaggerDoc, DocumentFilterContext context)
    {
        // Проверяем, есть ли компоненты в документе, если нет — создаем
        if (swaggerDoc.Components == null)
        {
            swaggerDoc.Components = new OpenApiComponents();
        }
        if (swaggerDoc.Components.Schemas == null)
        {
            swaggerDoc.Components.Schemas = new Dictionary<string, OpenApiSchema>();
        }

        // Добавляем схему для AddCommentDto, если её нет
        if (!swaggerDoc.Components.Schemas.ContainsKey("AddCommentDto"))
        {
            swaggerDoc.Components.Schemas.Add(
                "AddCommentDto",
                new OpenApiSchema
                {
                    Type = "object",
                    Properties = new Dictionary<string, OpenApiSchema>
                    {
                        {
                            "Text",
                            new OpenApiSchema
                            {
                                Type = "string",
                                Description = "The text of the comment to add.",
                            }
                        },
                        {
                            "ParentCommentId",
                            new OpenApiSchema
                            {
                                Type = "string",
                                Format = "uuid",
                                Description =
                                    "Optional ID of the parent comment for replies.",
                                Nullable = true,
                            }
                        },
                    },
                    Required = new HashSet<string> { "Text" },
                    Description = "DTO for adding a new comment.",
                }
            );
        }

        // Добавляем схему для UpdateCommentDto, если её нет
        if (!swaggerDoc.Components.Schemas.ContainsKey("UpdateCommentDto"))
        {
            swaggerDoc.Components.Schemas.Add(
                "UpdateCommentDto",
                new OpenApiSchema
                {
                    Type = "object",
                    Properties = new Dictionary<string, OpenApiSchema>
                    {
                        {
                            "Text",
                            new OpenApiSchema
                            {
                                Type = "string",
                                Description = "The updated text of the comment.",
                            }
                        },
                    },
                    Required = new HashSet<string> { "Text" },
                    Description = "DTO for updating an existing comment.",
                }
            );
        }

        // Добавляем схему для SyncPublicWorkoutDto, если её нет
        if (!swaggerDoc.Components.Schemas.ContainsKey("SyncPublicWorkoutDto"))
        {
            swaggerDoc.Components.Schemas.Add(
                "SyncPublicWorkoutDto",
                new OpenApiSchema
                {
                    Type = "object",
                    Properties = new Dictionary<string, OpenApiSchema>
                    {
                        {
                            "PrivateWorkoutId",
                            new OpenApiSchema
                            {
                                Type = "string",
                                Format = "uuid",
                                Description =
                                    "The ID of the private workout to synchronize.",
                            }
                        },
                        {
                            "Name",
                            new OpenApiSchema
                            {
                                Type = "string",
                                Description = "The name of the workout.",
                            }
                        },
                        {
                            "Type",
                            new OpenApiSchema
                            {
                                Type = "string",
                                Description = "The type of the workout.",
                            }
                        },
                        {
                            "PreviewUrl",
                            new OpenApiSchema
                            {
                                Type = "string",
                                Description =
                                    "The URL of the workout preview image.",
                                Nullable = true,
                            }
                        },
                        {
                            "Exercises",
                            new OpenApiSchema
                            {
                                Type = "array",
                                Items = new OpenApiSchema
                                {
                                    Reference = new OpenApiReference
                                    {
                                        Type = ReferenceType.Schema,
                                        Id = "ExerciseDto",
                                    },
                                },
                                Description =
                                    "The list of exercises in the workout.",
                            }
                        },
                    },
                    Required = new HashSet<string>
                    {
                        "PrivateWorkoutId",
                        "Name",
                        "Type",
                    },
                    Description =
                        "DTO for synchronizing a private workout to the public catalog.",
                }
            );
        }

        // Добавляем схему для ExerciseDto, если её нет
        if (!swaggerDoc.Components.Schemas.ContainsKey("ExerciseDto"))
        {
            swaggerDoc.Components.Schemas.Add(
                "ExerciseDto",
                new OpenApiSchema
                {
                    Type = "object",
                    Properties = new Dictionary<string, OpenApiSchema>
                    {
                        {
                            "ExerciseId",
                            new OpenApiSchema
                            {
                                Type = "string",
                                Format = "uuid",
                                Description =
                                    "The unique identifier of the exercise.",
                            }
                        },
                        {
                            "Name",
                            new OpenApiSchema
                            {
                                Type = "string",
                                Description = "The name of the exercise.",
                            }
                        },
                        {
                            "Description",
                            new OpenApiSchema
                            {
                                Type = "string",
                                Description = "The description of the exercise.",
                                Nullable = true,
                            }
                        },
                        {
                            "MediaUrl",
                            new OpenApiSchema
                            {
                                Type = "string",
                                Description =
                                    "The URL of the media associated with the exercise.",
                                Nullable = true,
                            }
                        },
                        {
                            "OrderIndex",
                            new OpenApiSchema
                            {
                                Type = "integer",
                                Description =
                                    "The order index of the exercise in the workout.",
                            }
                        },
                        {
                            "DurationSeconds",
                            new OpenApiSchema
                            {
                                Type = "integer",
                                Description =
                                    "The duration of the exercise in seconds.",
                            }
                        },
                    },
                    Required = new HashSet<string>
                    {
                        "ExerciseId",
                        "Name",
                        "OrderIndex",
                        "DurationSeconds",
                    },
                    Description = "DTO for an exercise in a workout.",
                }
            );
        }
    }
}
