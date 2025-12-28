using System.Reflection;
using Microsoft.AspNetCore.Mvc;
using Microsoft.OpenApi.Models;
using Pepegov.MicroserviceFramework.AspNetCore.WebApplicationDefinition;
using Pepegov.MicroserviceFramework.Definition;
using Pepegov.MicroserviceFramework.Definition.Context;
using Pepegov.MicroserviceFramework.Infrastructure.Attributes;
using PublicWorkout.Application.Dtos;
using PublicWorkout.UI.Api.Definitions.Swagger;
using Swashbuckle.AspNetCore.SwaggerUI;
using User.Application;

namespace User.UI.Api.Definitions.Swagger;

/// <summary>
/// Swagger definition for application.
/// </summary>
public class SwaggerDefinition : ApplicationDefinition
{
    /// <inheritdoc />
    public override Task ConfigureApplicationAsync(
        IDefinitionApplicationContext context
    )
    {
        var webContext = context.Parse<WebDefinitionApplicationContext>();

        webContext.WebApplication.UseSwagger();
        webContext.WebApplication.UseSwaggerUI(settings =>
        {
            settings.InjectStylesheet("/swagger/dark-theme.css");
            settings.DefaultModelExpandDepth(0);
            settings.DefaultModelRendering(ModelRendering.Model);
            settings.DefaultModelsExpandDepth(0);
            settings.DocExpansion(DocExpansion.None);
            settings.DisplayRequestDuration();
        });

        return base.ConfigureApplicationAsync(context);
    }

    /// <inheritdoc />
    public override Task ConfigureServicesAsync(IDefinitionServiceContext context)
    {
        context.ServiceCollection.Configure<ApiBehaviorOptions>(options =>
        {
            options.SuppressModelStateInvalidFilter = true;
        });
        context.ServiceCollection.AddEndpointsApiExplorer();
        context.ServiceCollection.AddSwaggerGen(options =>
        {
            var now = DateTime.Now.ToString("f");
            options.SwaggerDoc(
                "v1",
                new OpenApiInfo
                {
                    Title = AppData.ServiceName,
                    Version = AppData.ServiceVersion,
                    Description =
                        AppData.ServiceDescription + $" | Upload time: {now}",
                }
            );

            options.ResolveConflictingActions(x => x.First());

            // Включаем XML-комментарии для документации
            var xmlFilename =
                $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
            var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFilename);
            if (File.Exists(xmlPath))
            {
                options.IncludeXmlComments(xmlPath);
            }

            options.TagActionsBy(api =>
            {
                string tag;
                if (api.ActionDescriptor is { } descriptor)
                {
                    var attribute = descriptor
                        .EndpointMetadata.OfType<FeatureGroupNameAttribute>()
                        .FirstOrDefault();
                    tag =
                        attribute?.GroupName
                        ?? descriptor.RouteValues["controller"]
                        ?? "Untitled";
                }
                else
                {
                    tag = api.RelativePath!;
                }

                var tags = new List<string>();
                if (!string.IsNullOrEmpty(tag))
                {
                    tags.Add(tag);
                }

                return tags;
            });

            // Добавляем поддержку JWT Bearer токенов
            options.AddSecurityDefinition(
                "Bearer",
                new OpenApiSecurityScheme
                {
                    Name = "Authorization",
                    Type = SecuritySchemeType.Http,
                    Scheme = "bearer",
                    BearerFormat = "JWT",
                    In = ParameterLocation.Header,
                    Description =
                        "JWT Authorization header using the Bearer scheme. Введите токен без префикса 'Bearer '.",
                }
            );
            options.AddSecurityRequirement(
                new OpenApiSecurityRequirement
                {
                    {
                        new OpenApiSecurityScheme
                        {
                            Reference = new OpenApiReference
                            {
                                Type = ReferenceType.SecurityScheme,
                                Id = "Bearer",
                            },
                        },
                        []
                    },
                }
            );

            // Добавляем фильтр для регистрации дополнительных схем
            options.DocumentFilter<AdditionalSchemasDocumentFilter>();

            // На всякий случай, явно задаем идентификатор схемы
            options.CustomSchemaIds(type => type.Name);
        });

        return base.ConfigureServicesAsync(context);
    }
}
