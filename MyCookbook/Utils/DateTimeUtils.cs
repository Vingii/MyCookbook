namespace MyCookbook.Utils
{
    public static class DateTimeUtils
    {
        public static int? AsMinutes(string s)
        {
            s = s.Trim().Replace("h ", ":").Replace(" ", "").Replace("h:", ":").Replace("h", "").Replace("m", "");
            if (int.TryParse(s, out var minutes)) return minutes;
            if (TimeSpan.TryParse(s, out var timeSpan)) return (int)timeSpan.TotalMinutes;
            return null;
        }
    }
}
