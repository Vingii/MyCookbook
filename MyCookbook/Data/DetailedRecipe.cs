using MyCookbook.Data.CookbookDatabase;

namespace MyCookbook.Data
{
    public class DetailedRecipe
    {
        public Recipe Recipe { get; set; }
        public List<Ingredient> Ingredients { get; set; }
        public List<Step> Steps { get; set; }

        public DetailedRecipe(Recipe recipe, List<Ingredient>? ingredients = null, List<Step>? steps = null)
        {
            Recipe = recipe;
            Ingredients = ingredients ?? new List<Ingredient>();
            Steps = steps ?? new List<Step>();
        }
    }
}
