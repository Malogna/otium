namespace Otium.Core.Power;

public static class Electricity
{
    public static double CostPerDay(double watts, double pricePerKwh) => watts * 24.0 / 1000.0 * pricePerKwh;

    public static double KilowattHours(double watts, TimeSpan duration) => watts * duration.TotalHours / 1000.0;
}
