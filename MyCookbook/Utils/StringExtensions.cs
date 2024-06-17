namespace MyCookbook.Utils
{
    public static class StringExtensions
    {
        public static string CapitalizeFirst(this string input) =>
            input switch
            {
                "" => "",
                _ => string.Concat(input[0].ToString().ToUpper(), input.AsSpan(1))
            };
    }
}
