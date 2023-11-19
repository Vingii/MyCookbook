using Microsoft.EntityFrameworkCore;
using MyCookbook.Data.CookbookDatabase;

namespace MyCookbook.Data
{
    public class CookbookDatabaseService
    {
        private readonly CookbookDatabaseContext _context;

        public CookbookDatabaseService(CookbookDatabaseContext context)
        {
            _context = context;
        }

        public async Task<List<Recipe>> GetRecipesAsync(string user)
        {
            return await _context.Recipes
                 .Where(x => x.UserName == user)
                 .AsNoTracking().ToListAsync();
        }

        public async Task<Recipe> CreateRecipeAsync(Recipe recipe)
        {
            await _context.Recipes.AddAsync(recipe);
            await _context.SaveChangesAsync();
            return recipe;
        }

        public async Task<bool> UpdateRecipeAsync(Recipe recipe)
        {
            var foundRecipe =
                _context.Recipes
                .Where(x => x.Id == recipe.Id)
                .FirstOrDefault();

            if (foundRecipe == null) return false;

            foundRecipe.Name = recipe.Name;
            foundRecipe.Category = recipe.Category;
            foundRecipe.Duration = recipe.Duration;
            await _context.SaveChangesAsync();

            return true;
        }

        public async Task<bool> DeleteRecipeAsync(Recipe recipe)
        {
            var foundRecipe =
                _context.Recipes
                .Where(x => x.Id == recipe.Id)
                .FirstOrDefault();

            if (foundRecipe == null) return false;

            _context.Recipes.Remove(foundRecipe);
            await _context.SaveChangesAsync();

            return true;
        }
    }
}