using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace User.UI.Api.Definitions.Swagger;

/// <summary>
/// Фильтр для добавления дополнительных схем в Swagger
/// </summary>
public class AdditionalSchemasDocumentFilter : IDocumentFilter
{
    public void Apply(OpenApiDocument swaggerDoc, DocumentFilterContext context)
    {
        // Если нужны дополнительные схемы - добавляй их здесь
        // Например: swaggerDoc.Components.Schemas.Add("CustomType", new OpenApiSchema { ... });
        
        // Пока оставляем пустым, чтобы код компилировался
    }
}
