namespace MataHR.Organisation;

internal static class ChartCoordinates
{
    // The same coordinate limit applies to cards, the company header and connector bends.
    public static bool IsValid(double x, double y) =>
        double.IsFinite(x) && double.IsFinite(y) && Math.Abs(x) <= 100000 && Math.Abs(y) <= 100000;
}
