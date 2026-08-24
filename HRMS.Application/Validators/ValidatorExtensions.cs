using FluentValidation;
using Microsoft.Extensions.DependencyInjection;

namespace HRMS.Application.Validators;

/// <summary>
/// Registers all FluentValidation validators in HRMS.Application with the DI container.
/// Call this from <c>IServiceCollection.AddInfrastructure()</c> or <c>Program.cs</c>.
/// Each DTO has exactly ONE validator — duplicates are prevented at compile time.
/// </summary>
public static class ValidatorExtensions
{
    public static IServiceCollection AddHrmsValidators(this IServiceCollection services)
    {
        // Scan the HRMS.Application assembly and register every AbstractValidator<T>
        // that is defined there. This covers all validators in the Validators folder.
        services.AddValidatorsFromAssemblyContaining<LoginDtoValidator>(ServiceLifetime.Scoped);
        return services;
    }
}
