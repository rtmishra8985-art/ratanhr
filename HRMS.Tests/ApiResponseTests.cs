using HRMS.Application.Common;
using Xunit;

namespace HRMS.Tests;

/// <summary>
/// Tests for the ApiResponse envelope used in all controller responses.
/// Validates: Success/Fail factory methods, data propagation, error lists,
/// and correct Success flag semantics.
/// </summary>
public class ApiResponseTests
{
    // ── ApiResponse<T> ───────────────────────────────────────────────────────

    [Fact]
    public void Ok_WithData_SetsSuccessTrueAndPopulatesData()
    {
        var response = ApiResponse<string>.Ok("hello", "Created");

        Assert.True(response.Success);
        Assert.Equal("hello", response.Data);
        Assert.Equal("Created", response.Message);
        Assert.Empty(response.Errors);
    }

    [Fact]
    public void Ok_DefaultMessage_IsSuccess()
    {
        var response = ApiResponse<int>.Ok(42);

        Assert.True(response.Success);
        Assert.Equal(42, response.Data);
        Assert.Equal("Success", response.Message);
    }

    [Fact]
    public void Fail_WithMessage_SetsSuccessFalseAndNullData()
    {
        var response = ApiResponse<string>.Fail("Not found");

        Assert.False(response.Success);
        Assert.Equal("Not found", response.Message);
        Assert.Null(response.Data);
    }

    [Fact]
    public void Fail_WithErrors_PopulatesErrorList()
    {
        var errors = new List<string> { "Field A is required", "Field B must be positive" };
        var response = ApiResponse<object>.Fail("Validation failed", errors);

        Assert.False(response.Success);
        Assert.Equal(2, response.Errors.Count);
        Assert.Contains("Field A is required", response.Errors);
        Assert.Contains("Field B must be positive", response.Errors);
    }

    // ── Non-generic ApiResponse ──────────────────────────────────────────────

    [Fact]
    public void NonGeneric_Ok_SetsSuccessTrue()
    {
        var response = ApiResponse.Ok("Operation completed");

        Assert.True(response.Success);
        Assert.Equal("Operation completed", response.Message);
    }

    [Fact]
    public void NonGeneric_Fail_SetsSuccessFalse()
    {
        var response = ApiResponse.Fail("Something went wrong");

        Assert.False(response.Success);
        Assert.Equal("Something went wrong", response.Message);
    }
}
