using MyCookbook.Data.CookbookDatabase;

namespace MyCookbook.Data.CookbookDatabaseExtensions
{
    public static class RecipeExtensions
    {
        public static string GetDurationFormatted(this Recipe recipe)
        {
            if (recipe.Duration == null) return "";

            var span = TimeSpan.FromMinutes(recipe.Duration ?? 0);
            return span.Hours > 0 ? $"{span.Hours}:{span.Minutes}" : span.Minutes.ToString();
        }
    }
}
