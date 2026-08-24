using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace HRMS.API.Swagger;

/// <summary>
/// Keeps OpenAPI security metadata aligned with ASP.NET Core endpoint metadata.
/// Anonymous auth endpoints must explicitly clear the document-level Bearer
/// requirement; otherwise Swagger UI falsely presents them as authenticated.
/// </summary>
public sealed class AuthorizeOperationFilter : IOperationFilter
{
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        if (context.ApiDescription.ActionDescriptor is not ControllerActionDescriptor action)
            return;

        var metadata = action.EndpointMetadata;
        var allowsAnonymous = metadata.OfType<IAllowAnonymous>().Any()
            || action.MethodInfo.IsDefined(typeof(AllowAnonymousAttribute), inherit: true)
            || action.ControllerTypeInfo.IsDefined(typeof(AllowAnonymousAttribute), inherit: true);

        if (allowsAnonymous)
        {
            operation.Security = new List<OpenApiSecurityRequirement>();
            return;
        }

        var requiresAuthorization = metadata.OfType<IAuthorizeData>().Any()
            || action.MethodInfo.IsDefined(typeof(AuthorizeAttribute), inherit: true)
            || action.ControllerTypeInfo.IsDefined(typeof(AuthorizeAttribute), inherit: true);

        if (requiresAuthorization)
        {
            operation.Security = new List<OpenApiSecurityRequirement>
            {
                new()
                {
                    [new OpenApiSecurityScheme
                    {
                        Reference = new OpenApiReference
                        {
                            Type = ReferenceType.SecurityScheme,
                            Id = "Bearer"
                        }
                    }] = Array.Empty<string>()
                }
            };
        }
    }
}