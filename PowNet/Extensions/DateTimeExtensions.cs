namespace PowNet.Extensions
{
    public static class DateTimeExtensions
    {
        public static string ToAppEndStandard(this DateTime dateTime)
        {
            return dateTime.ToString("yyyy-MM-dd HH:mm:ss tt");
        }
    }
}