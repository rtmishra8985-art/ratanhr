namespace HRMS.Application.Common;

/// <summary>Small result type for service operations that return success or failure.</summary>
public sealed class ServiceResult
{
    public bool IsSuccess { get; }
    public string Message { get; }
    public string Error { get; }

    private ServiceResult(bool isSuccess, string message)
    {
        IsSuccess = isSuccess;
        Message = message;
        Error = isSuccess ? string.Empty : message;
    }

    public static ServiceResult Ok(string message) => new(true, message);
    public static ServiceResult Fail(string message) => new(false, message);
}