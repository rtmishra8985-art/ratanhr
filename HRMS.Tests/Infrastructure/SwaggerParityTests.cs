using System.Net;
using System.Reflection;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.ApiExplorer;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;

namespace HRMS.Tests.Infrastructure;

/// <summary>
/// Contract checks for the generated Swagger document.
///
/// The always-on check builds the controller inventory from ASP.NET Core's
/// ApiExplorer rather than maintaining a second hand-written route list. The
/// live check is opt-in: set HRMS_SWAGGER_BASE_URL to an approved running
/// staging API and it will fetch /swagger/v1/swagger.json and compare every
/// operation, request/response metadata, and bearer security definition.
/// </summary>
public sealed class SwaggerParityTests
{
    private static readonly Assembly ApiAssembly = typeof(Program).Assembly;

    [Fact]
    public void ControllerApiExplorerInventory_IsPresentAndUnique()
    {
        var inventory = BuildControllerInventory();

        Assert.NotEmpty(inventory);
        Assert.Equal(
            inventory.Count,
            inventory.Select(static operation => operation.Key).Distinct().Count());
        Console.WriteLine($"Controller ApiExplorer operation inventory: {inventory.Count}");
    }

    [LiveSwaggerFact]
    public async Task LiveSwagger_MatchesControllerApiExplorerInventory()
    {
        var baseUrl = Environment.GetEnvironmentVariable("HRMS_SWAGGER_BASE_URL");
        Assert.False(
            string.IsNullOrWhiteSpace(baseUrl),
            "HRMS_SWAGGER_BASE_URL must be set when the live Swagger parity test runs.");

        using var client = new HttpClient
        {
            BaseAddress = new Uri(baseUrl!.TrimEnd('/') + "/")
        };

        HttpResponseMessage response;
        try
        {
            response = await client.GetAsync("swagger/v1/swagger.json");
        }
        catch (Exception ex)
        {
            throw new Xunit.Sdk.XunitException(
                $"Configured Swagger endpoint could not be reached: {client.BaseAddress}. {ex.Message}");
        }

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(
            "application/json",
            response.Content.Headers.ContentType?.MediaType);

        using var document = await JsonDocument.ParseAsync(
            await response.Content.ReadAsStreamAsync());
        var root = document.RootElement;

        Assert.Equal("3.0.1", root.GetProperty("openapi").GetString());
        AssertBearerSecurityDefinition(root);

        var expected = BuildControllerInventory()
            .ToDictionary(static operation => operation.Key);
        var actual = ReadSwaggerInventory(root);

        // FIX: Program.cs registers a small number of endpoints via the minimal API
        // (app.MapGet(...)) rather than MVC controllers — the root text banner ("/") and
        // the CSRF token seed endpoint ("/api/auth/csrf"). BuildControllerInventory() only
        // walks IApiDescriptionGroupCollectionProvider entries backed by
        // ControllerActionDescriptor, so these two routes are legitimately absent from the
        // "expected" (controller-only) side even though they correctly appear in the live
        // Swagger document. Without this allowlist, every live run failed with "Swagger
        // contains operations without a controller action" for routes that were never
        // supposed to be controller actions in the first place.
        var minimalApiEndpoints = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "GET ",                  // "/" — HtmlNonceInjectionMiddleware/root banner, path.Name.TrimEnd('/') on "/" yields ""
            "GET /api/auth/csrf"      // CSRF token seed endpoint (MapGet in Program.cs)
        };

        var missing = expected.Keys.Except(actual.Keys).OrderBy(static key => key).ToArray();
        var extra = actual.Keys.Except(expected.Keys)
            .Except(minimalApiEndpoints, StringComparer.OrdinalIgnoreCase)
            .OrderBy(static key => key)
            .ToArray();

        Assert.True(
            missing.Length == 0,
            "Swagger is missing controller operations:\n" + string.Join('\n', missing));
        Assert.True(
            extra.Length == 0,
            "Swagger contains operations without a controller action:\n" +
            string.Join('\n', extra));

        var mismatches = new List<string>();
        foreach (var key in expected.Keys.OrderBy(static key => key))
        {
            var controllerOperation = expected[key];
            var swaggerOperation = actual[key];

            if (!controllerOperation.PathParameters.SetEquals(swaggerOperation.PathParameters))
            {
                mismatches.Add(
                    $"{key}: path parameters expected " +
                    $"{FormatSet(controllerOperation.PathParameters)} but Swagger has " +
                    $"{FormatSet(swaggerOperation.PathParameters)}");
            }

            if (!controllerOperation.QueryParameters.SetEquals(swaggerOperation.QueryParameters))
            {
                mismatches.Add(
                    $"{key}: query parameters expected " +
                    $"{FormatSet(controllerOperation.QueryParameters)} but Swagger has " +
                    $"{FormatSet(swaggerOperation.QueryParameters)}");
            }

            if (controllerOperation.HasRequestBody != swaggerOperation.HasRequestBody)
            {
                mismatches.Add(
                    $"{key}: request body expected {controllerOperation.HasRequestBody} " +
                    $"but Swagger has {swaggerOperation.HasRequestBody}");
            }

            if (!controllerOperation.ResponseStatusCodes.SetEquals(
                    swaggerOperation.ResponseStatusCodes))
            {
                mismatches.Add(
                    $"{key}: response status codes expected " +
                    $"{FormatSet(controllerOperation.ResponseStatusCodes)} but Swagger has " +
                    $"{FormatSet(swaggerOperation.ResponseStatusCodes)}");
            }

            if (!controllerOperation.RequestContentTypes.SetEquals(
                    swaggerOperation.RequestContentTypes))
            {
                mismatches.Add(
                    $"{key}: request content types expected " +
                    $"{FormatSet(controllerOperation.RequestContentTypes)} but Swagger has " +
                    $"{FormatSet(swaggerOperation.RequestContentTypes)}");
            }

            if (!controllerOperation.ResponseContentTypes.SetEquals(
                    swaggerOperation.ResponseContentTypes))
            {
                mismatches.Add(
                    $"{key}: response content types expected " +
                    $"{FormatSet(controllerOperation.ResponseContentTypes)} but Swagger has " +
                    $"{FormatSet(swaggerOperation.ResponseContentTypes)}");
            }

            if (controllerOperation.RequiresBearer &&
                !swaggerOperation.HasEffectiveBearerSecurity)
            {
                mismatches.Add($"{key}: secured action has no effective Bearer/JWT metadata");
            }
        }

        Assert.True(
            mismatches.Count == 0,
            "Swagger/controller metadata mismatches:\n" + string.Join('\n', mismatches));
    }

    private static IReadOnlyList<OperationContract> BuildControllerInventory()
    {
        var services = new ServiceCollection();
        services
            .AddLogging()
            .AddMvcCore()
            .AddApplicationPart(ApiAssembly)
            .AddApiExplorer();

        // FIX: Program.cs registers API versioning with a QueryStringApiVersionReader
        // ("api-version"), which ASP.NET Core's ApiExplorer surfaces as an implicit query
        // parameter on every operation once Swashbuckle enumerates it. The offline inventory
        // builder previously never registered API versioning at all, so every single
        // controller action was missing the "api-version" query parameter that the live
        // Swagger document correctly includes — producing a mismatch on nearly every
        // versioned route rather than reflecting a real app defect.
        services.AddApiVersioning(options =>
        {
            options.DefaultApiVersion                  = new Asp.Versioning.ApiVersion(1, 0);
            options.AssumeDefaultVersionWhenUnspecified = true;
            options.ReportApiVersions                  = true;
            options.ApiVersionReader = Asp.Versioning.ApiVersionReader.Combine(
                new Asp.Versioning.UrlSegmentApiVersionReader(),
                new Asp.Versioning.HeaderApiVersionReader("api-version"),
                new Asp.Versioning.QueryStringApiVersionReader("api-version")
            );
        }).AddApiExplorer(options =>
        {
            options.GroupNameFormat           = "'v'VVV";
            options.SubstituteApiVersionInUrl = true;
        });

        using var provider = services.BuildServiceProvider();
        var apiDescriptions = provider
            .GetRequiredService<IApiDescriptionGroupCollectionProvider>()
            .ApiDescriptionGroups
            .Items
            .SelectMany(static group => group.Items)
            .Where(static description => !string.IsNullOrWhiteSpace(description.HttpMethod))
            .ToArray();

        return apiDescriptions
            .Select(CreateControllerContract)
            .OrderBy(static operation => operation.Key)
            .ToArray();
    }

    private static OperationContract CreateControllerContract(ApiDescription description)
    {
        var action = Assert.IsType<ControllerActionDescriptor>(description.ActionDescriptor);
        var path = "/" + (description.RelativePath ?? string.Empty).Split('?')[0].Trim('/');
        var method = description.HttpMethod!.ToUpperInvariant();
        var key = $"{method} {path}";

        var pathParameters = description.ParameterDescriptions
            .Where(static parameter => parameter.Source == BindingSource.Path)
            .Select(static parameter => parameter.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var queryParameters = description.ParameterDescriptions
            .Where(static parameter => parameter.Source == BindingSource.Query)
            .Select(static parameter => parameter.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var requestContentTypes = description.SupportedRequestFormats
            .Select(static format => format.MediaType)
            .Where(static mediaType => !string.IsNullOrWhiteSpace(mediaType))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        // FIX: [FromForm] parameters (multipart file uploads, e.g. EmployeeController.Create)
        // are described by ApiExplorer with BindingSource.Form, not BindingSource.Body — but
        // Swashbuckle still emits a genuine OpenAPI "requestBody" with multipart/form-data
        // content for them. Only checking for BindingSource.Body under-counted every
        // multipart endpoint as having no request body and no content type, which is a
        // test-harness fidelity gap rather than a real API defect.
        //
        // A second, related gap: controller actions that bind a bare IFormFile parameter
        // directly (no [FromForm] attribute, e.g. AttendanceController.UploadExcel(IFormFile
        // file)) are described by ApiExplorer with BindingSource.FormFile rather than
        // BindingSource.Form, so both binding sources must be treated as "this action has a
        // multipart request body".
        var hasFormParameters = description.ParameterDescriptions.Any(
            static parameter => parameter.Source == BindingSource.Form
                              || parameter.Source == BindingSource.FormFile);
        if (hasFormParameters && requestContentTypes.Count == 0)
            requestContentTypes.Add("multipart/form-data");
        var responseContentTypes = description.SupportedResponseTypes
            .SelectMany(static response => response.ApiResponseFormats)
            .Select(static format => format.MediaType)
            .Where(static mediaType => !string.IsNullOrWhiteSpace(mediaType))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var controllerAuthorize = action.ControllerTypeInfo
            .GetCustomAttributes(inherit: true)
            .OfType<Microsoft.AspNetCore.Authorization.AuthorizeAttribute>();
        var actionAuthorize = action.MethodInfo
            .GetCustomAttributes(inherit: true)
            .OfType<Microsoft.AspNetCore.Authorization.AuthorizeAttribute>();
        var allowAnonymous = action.ControllerTypeInfo
            .GetCustomAttributes(inherit: true)
            .OfType<Microsoft.AspNetCore.Authorization.AllowAnonymousAttribute>()
            .Any()
            || action.MethodInfo
                .GetCustomAttributes(inherit: true)
                .OfType<Microsoft.AspNetCore.Authorization.AllowAnonymousAttribute>()
                .Any();

        var responseStatusCodes = description.SupportedResponseTypes
            .Select(static response => response.StatusCode)
            .Select(static statusCode => statusCode.ToString())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        // FIX: Swashbuckle infers a default 200 OK response for any action that does not
        // declare [ProducesResponseType] explicitly (as long as it isn't void/IActionResult
        // with no return value). ApiExplorer's SupportedResponseTypes only reflects explicit
        // conventions/attributes, so it is empty for the majority of this codebase's actions.
        // Without mirroring that default, nearly every action without an explicit attribute
        // mismatched with "expected {} but Swagger has {200}" — a test-harness fidelity gap,
        // not an application defect.
        if (responseStatusCodes.Count == 0)
            responseStatusCodes.Add("200");

        return new OperationContract(
            key,
            pathParameters,
            queryParameters,
            hasFormParameters || description.ParameterDescriptions.Any(
                static parameter => parameter.Source == BindingSource.Body),
            responseStatusCodes,
            requestContentTypes,
            responseContentTypes,
            !allowAnonymous && (controllerAuthorize.Any() || actionAuthorize.Any()));
    }

    private static Dictionary<string, SwaggerOperation> ReadSwaggerInventory(JsonElement root)
    {
        var globalBearer = root.TryGetProperty("security", out var security)
            && ContainsBearer(security);
        var result = new Dictionary<string, SwaggerOperation>(StringComparer.OrdinalIgnoreCase);

        foreach (var path in root.GetProperty("paths").EnumerateObject())
        {
            foreach (var operation in path.Value.EnumerateObject()
                         .Where(static property => IsHttpMethod(property.Name)))
            {
                var operationKey = $"{operation.Name.ToUpperInvariant()} " +
                                   $"{path.Name.TrimEnd('/')}";
                var operationValue = operation.Value;
                var pathParameters = ReadParameterNames(operationValue, "path");
                var queryParameters = ReadParameterNames(operationValue, "query");
                var hasRequestBody = operationValue.TryGetProperty(
                    "requestBody",
                    out var requestBody);
                var requestContentTypes = hasRequestBody
                    ? requestBody.GetProperty("content").EnumerateObject()
                        .Select(static property => property.Name)
                        .ToHashSet(StringComparer.OrdinalIgnoreCase)
                    : new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                var responseContentTypes = new HashSet<string>(
                    StringComparer.OrdinalIgnoreCase);
                var statusCodes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                foreach (var response in operationValue.GetProperty("responses").EnumerateObject())
                {
                    statusCodes.Add(response.Name);
                    if (response.Value.TryGetProperty("content", out var content))
                    {
                        foreach (var contentType in content.EnumerateObject())
                            responseContentTypes.Add(contentType.Name);
                    }
                }

                var effectiveBearer = operationValue.TryGetProperty(
                    "security",
                    out var operationSecurity)
                    ? ContainsBearer(operationSecurity)
                    : globalBearer;

                result[operationKey] = new SwaggerOperation(
                    operationKey,
                    pathParameters,
                    queryParameters,
                    hasRequestBody,
                    statusCodes,
                    requestContentTypes,
                    responseContentTypes,
                    effectiveBearer);
            }
        }

        return result;
    }

    private static HashSet<string> ReadParameterNames(
        JsonElement operation,
        string location)
    {
        if (!operation.TryGetProperty("parameters", out var parameters))
            return new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        return parameters.EnumerateArray()
            .Where(parameter =>
                parameter.TryGetProperty("in", out var parameterLocation)
                && string.Equals(
                    parameterLocation.GetString(),
                    location,
                    StringComparison.OrdinalIgnoreCase))
            .Select(parameter => parameter.GetProperty("name").GetString()!)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    private static void AssertBearerSecurityDefinition(JsonElement root)
    {
        var schemes = root.GetProperty("components").GetProperty("securitySchemes");
        Assert.True(schemes.TryGetProperty("Bearer", out var bearer));
        Assert.Equal("http", bearer.GetProperty("type").GetString());
        Assert.Equal("bearer", bearer.GetProperty("scheme").GetString());
        Assert.Equal("JWT", bearer.GetProperty("bearerFormat").GetString());
    }

    private static bool ContainsBearer(JsonElement security)
    {
        return security.ValueKind == JsonValueKind.Array
            && security.EnumerateArray().Any(requirement =>
                requirement.ValueKind == JsonValueKind.Object
                && requirement.EnumerateObject().Any(
                    property => string.Equals(
                        property.Name,
                        "Bearer",
                        StringComparison.OrdinalIgnoreCase)));
    }

    private static bool IsHttpMethod(string name) =>
        name is "get" or "post" or "put" or "patch" or "delete" or "head" or "options" or "trace";

    private static string FormatSet(IEnumerable<string> values) =>
        "{" + string.Join(", ", values.OrderBy(static value => value)) + "}";

    private sealed record OperationContract(
        string Key,
        HashSet<string> PathParameters,
        HashSet<string> QueryParameters,
        bool HasRequestBody,
        HashSet<string> ResponseStatusCodes,
        HashSet<string> RequestContentTypes,
        HashSet<string> ResponseContentTypes,
        bool RequiresBearer);

    private sealed record SwaggerOperation(
        string Key,
        HashSet<string> PathParameters,
        HashSet<string> QueryParameters,
        bool HasRequestBody,
        HashSet<string> ResponseStatusCodes,
        HashSet<string> RequestContentTypes,
        HashSet<string> ResponseContentTypes,
        bool HasEffectiveBearerSecurity);
}

/// <summary>
/// Runs the live contract check only when an approved staging base URL is supplied.
/// Discovery marks it skipped otherwise, preserving the distinction between
/// "not exercised" and "passed" in the test report.
/// </summary>
internal sealed class LiveSwaggerFactAttribute : FactAttribute
{
    public LiveSwaggerFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(
                Environment.GetEnvironmentVariable("HRMS_SWAGGER_BASE_URL")))
        {
            Skip = "Live Swagger parity requires HRMS_SWAGGER_BASE_URL.";
        }
    }
}