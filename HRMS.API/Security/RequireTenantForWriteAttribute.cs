using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace HRMS.API.Security;

/// <summary>
/// Prevents write requests from operating without an explicit tenant scope.
/// SuperAdmin has unrestricted read scope, but write operations must identify a
/// concrete company because the write contracts accept one company at a time.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public sealed class RequireTenantForWriteAttribute : TypeFilterAttribute
{
    public RequireTenantForWriteAttribute() : base(typeof(RequireTenantForWriteFilter)) { }
}

public sealed class RequireTenantForWriteFilter : IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(
        ActionExecutingContext context,
        ActionExecutionDelegate next)
    {
        var method = context.HttpContext.Request.Method;
        if (HttpMethods.IsGet(method) || HttpMethods.IsHead(method) || HttpMethods.IsOptions(method))
        {
            await next();
            return;
        }

        var principal = context.HttpContext.User;
        var hasValidCompany = int.TryParse(
            principal.FindFirstValue("companyId"),
            out var companyId) && companyId > 0;

        if (!hasValidCompany)
        {
            context.Result = new ForbidResult();
            return;
        }

        await next();
    }
}