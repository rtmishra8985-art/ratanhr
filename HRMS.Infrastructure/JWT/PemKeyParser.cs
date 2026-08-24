namespace HRMS.Infrastructure.JWT;

/// <summary>
/// Normalizes PEM values supplied through environment variables.
/// Compose/.env files commonly carry line breaks as the literal "\n" sequence.
/// </summary>
public static class PemKeyParser
{
    public static string Normalize(string pem)
    {
        return pem
            .Replace("\\r\\n", "\n", StringComparison.Ordinal)
            .Replace("\\n", "\n", StringComparison.Ordinal)
            .Replace("\\r", "\r", StringComparison.Ordinal)
            .Trim();
    }
}