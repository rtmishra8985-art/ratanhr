namespace HRMS.Application.Common;

/// <summary>
/// Centralized validation filter to ensure all endpoints return consistent error responses.
/// Replaces manual ModelState checks throughout controllers.
/// </summary>
public class ValidationFilterAttribute : Microsoft.AspNetCore.Mvc.Filters.ActionFilterAttribute
{
    public override void OnActionExecuting(Microsoft.AspNetCore.Mvc.Filters.ActionExecutingContext context)
    {
        if (!context.ModelState.IsValid)
        {
            // ApiResponse.Fail(ModelStateDictionary) extracts and flattens error messages.
            context.Result = new Microsoft.AspNetCore.Mvc.BadRequestObjectResult(
                ApiResponse.Fail(context.ModelState));
        }
    }
}
