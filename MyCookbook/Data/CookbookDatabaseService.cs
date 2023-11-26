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

        public async Task<Recipe?> GetDetailedRecipeAsync(string name, string user)
        {
            return await _context.Recipes
                 .Where(x => x.UserName == user && x.Name == name)
                 .Include(x => x.Ingredients)
                 .Include(x => x.Steps)
                 .AsNoTracking().FirstOrDefaultAsync();
        }

        public async Task<List<Recipe>> GetRecipesAsync(string user)
        {
            return await _context.Recipes
                 .Where(x => x.UserName == user)
                 .AsNoTracking().ToListAsync();
        }

        public async Task<Recipe> CreateRecipeAsync(Recipe recipe, string user)
        {
            recipe.UserName = user;
            await _context.Recipes.AddAsync(recipe);
            await _context.SaveChangesAsync();
            return recipe;
        }

        public async Task<bool> UpdateRecipeAsync(Recipe recipe, string user)
        {
            var foundRecipe =
                _context.Recipes
                .Where(x => x.Id == recipe.Id && x.UserName == user)
                .FirstOrDefault();

            if (foundRecipe == null) return false;

            foundRecipe.Name = recipe.Name;
            foundRecipe.Category = recipe.Category;
            foundRecipe.Duration = recipe.Duration;
            await _context.SaveChangesAsync();

            return true;
        }

        public async Task<bool> DeleteRecipeAsync(Recipe recipe, string user)
        {
            var foundRecipe =
                _context.Recipes
                .Where(x => x.Id == recipe.Id && x.UserName == user)
                .Include(x => x.Ingredients)
                .Include(x => x.Steps)
                .FirstOrDefault();

            if (foundRecipe == null) return false;

            _context.Recipes.Remove(foundRecipe);
            await _context.SaveChangesAsync();

            return true;
        }

        public async Task<Step> CreateStepAsync(Step step, string user)
        {
            step.UserName = user;
            await _context.Steps.AddAsync(step);
            await _context.SaveChangesAsync();
            return step;
        }

        public async Task<bool> UpdateStepDescriptionAsync(Step step, string user)
        {
            var foundStep =
                _context.Steps
                .Where(x => x.Id == step.Id && x.UserName == user)
                .FirstOrDefault();

            if (foundStep == null) return false;

            foundStep.Description = step.Description;
            await _context.SaveChangesAsync();

            return true;
        }

        public async Task<bool> IncreaseStepOrder(Step step, string user)
        {
            var foundStep =
                _context.Steps
                .Where(x => x.Id == step.Id && x.UserName == user)
                .FirstOrDefault();

            if (foundStep == null) return false;

            var higherStep = _context.Steps
                .Where(x => x.RecipeId == step.RecipeId && x.UserName == user && x.Order == step.Order + 1).FirstOrDefault();

            if (higherStep == null) return true;

            higherStep.Order--;
            foundStep.Order++;

            await _context.SaveChangesAsync();

            return true;
        }

        public async Task<bool> DecreaseStepOrder(Step step, string user)
        {
            var foundStep =
                _context.Steps
                .Where(x => x.Id == step.Id && x.UserName == user)
                .FirstOrDefault();

            if (foundStep == null) return false;

            var lowerStep = _context.Steps
                .Where(x => x.RecipeId == step.RecipeId && x.UserName == user && x.Order == step.Order - 1).FirstOrDefault();

            if (lowerStep == null) return true;

            foundStep.Order--;
            lowerStep.Order++;

            await _context.SaveChangesAsync();

            return true;
        }

        public async Task<bool> DeleteStepAsync(Step step, string user)
        {
            var foundStep = await
                _context.Steps
                .Where(x => x.Id == step.Id && x.UserName == user)
                .FirstOrDefaultAsync();

            if (foundStep == null) return false;

            var higherSteps = await
                _context.Steps
                .Where(x => x.RecipeId == step.RecipeId && x.UserName == user)
                .Where(x => x.Order > step.Order)
                .ToListAsync();

            foreach (var higherStep in higherSteps)
            {
                higherStep.Order--;
            }

            _context.Steps.Remove(foundStep);
            await _context.SaveChangesAsync();

            return true;
        }

        public async Task<bool> FixStepsOrder(Recipe recipe, string user)
        {
            var foundSteps = await _context.Steps
                .Where(x => x.RecipeId == recipe.Id && x.UserName == user).OrderBy(x => x.Order).ToListAsync();

            for (int i = 0; i < foundSteps.Count; i++)
            {
                foundSteps[i].Order = i+1;
            }

            await _context.SaveChangesAsync();

            return true;
        }

        public async Task<Ingredient> CreateIngredientAsync(Ingredient ingredient, string user)
        {
            ingredient.UserName = user;
            await _context.Ingredients.AddAsync(ingredient);
            await _context.SaveChangesAsync();
            return ingredient;
        }

        public async Task<bool> UpdateIngredientAsync(Ingredient ingredient, string user)
        {
            var foundIngredient =
                _context.Ingredients
                .Where(x => x.Id == ingredient.Id && x.UserName == user)
                .FirstOrDefault();

            if (foundIngredient == null) return false;

            foundIngredient.Name = ingredient.Name;
            foundIngredient.Amount = ingredient.Amount;
            foundIngredient.Unit = ingredient.Unit;
            await _context.SaveChangesAsync();

            return true;
        }

        public async Task<bool> DeleteIngredientAsync(Ingredient ingredient, string user)
        {
            var foundIngredient =
                _context.Ingredients
                .Where(x => x.Id == ingredient.Id && x.UserName == user)
                .FirstOrDefault();

            if (foundIngredient == null) return false;

            _context.Ingredients.Remove(foundIngredient);
            await _context.SaveChangesAsync();

            return true;
        }
    }
}