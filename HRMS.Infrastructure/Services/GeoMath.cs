namespace HRMS.Infrastructure.Services;

internal static class GeoMath
{
    private const double R = 6_371_000;

    public static double HaversineMetres(double lat1, double lon1, double lat2, double lon2)
    {
        var dLat = ToRad(lat2 - lat1);
        var dLon = ToRad(lon2 - lon1);
        var a = Math.Sin(dLat/2)*Math.Sin(dLat/2)
              + Math.Cos(ToRad(lat1))*Math.Cos(ToRad(lat2))*Math.Sin(dLon/2)*Math.Sin(dLon/2);
        return R * 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1-a));
    }

    private static double ToRad(double d) => d * Math.PI / 180.0;
}
