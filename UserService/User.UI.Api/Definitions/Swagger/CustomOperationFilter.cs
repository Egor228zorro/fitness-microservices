using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;
using User.Application.Dtos;

namespace User.UI.Api.Definitions.Swagger;

public class CustomOperationFilter : IOperationFilter
{
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        // Проверяем, есть ли в методе чтение JSON из HttpContext
        var methodBody = context.MethodInfo.GetMethodBody();
        if (methodBody == null)
            return;

        // Проверяем имя метода, чтобы определить ожидаемый тип DTO
        var methodName = context.MethodInfo.Name;
        Type? requestType = null;

        if (methodName.Contains("SendOtpToEmail"))
        {
            requestType = typeof(EmailRequestDto);
        }
        else if (methodName.Contains("GetJwtToken"))
        {
            requestType = typeof(VerifyOtpRequestDto);
        }
        else if (
            methodName.Contains("RefreshAccessToken")
            || methodName.Contains("RevokeRefreshToken")
        )
        {
            requestType = typeof(RefreshTokenRequestDto);
        }
        else if (methodName.Contains("CreateUser"))
        {
            requestType = typeof(RegisterUserDto);
        }

        if (requestType != null && operation.RequestBody == null)
        {
            operation.RequestBody = new OpenApiRequestBody
            {
                Content = new Dictionary<string, OpenApiMediaType>
                {
                    {
                        "application/json",
                        new OpenApiMediaType
                        {
                            Schema = context.SchemaGenerator.GenerateSchema(
                                requestType,
                                context.SchemaRepository
                            ),
                        }
                    },
                },
                Description = $"Request body for {requestType.Name}",
                Required = true,
            };
        }
    }
}
