namespace PowNet.Extensions
{
    public static class DateTimeExtensions
    {
        public static string ToAppEndStandard(this DateTime dateTime)
        {
            return dateTime.ToString("yyyy-MM-dd HH:mm:ss tt");
        }

        #region Additional (Merged)
        public static DateTime RoundTo(this DateTime dt, TimeSpan step)
        {
            if (step <= TimeSpan.Zero) return dt;
            var ticks = (dt.Ticks + (step.Ticks / 2)) / step.Ticks;
            return new DateTime(ticks * step.Ticks, dt.Kind);
        }
        public static DateTime FloorTo(this DateTime dt, TimeSpan step)
        {
            if (step <= TimeSpan.Zero) return dt;
            var ticks = dt.Ticks / step.Ticks;
            return new DateTime(ticks * step.Ticks, dt.Kind);
        }
        public static DateTime CeilingTo(this DateTime dt, TimeSpan step)
        {
            if (step <= TimeSpan.Zero) return dt;
            var ticks = (dt.Ticks + step.Ticks - 1) / step.Ticks;
            return new DateTime(ticks * step.Ticks, dt.Kind);
        }
        public static bool IsBetween(this DateTime dt, DateTime start, DateTime end, bool inclusive = true)
        {
            return inclusive ? (dt >= start && dt <= end) : (dt > start && dt < end);
        }
        public static DateTime Next(this DateTime dt, DayOfWeek day)
        {
            int diff = (7 + (int)day - (int)dt.DayOfWeek) % 7;
            diff = diff == 0 ? 7 : diff;
            return dt.Date.AddDays(diff);
        }
        #endregion
    }
}