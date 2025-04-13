using Microsoft.EntityFrameworkCore;
using System.Text.RegularExpressions;
using MyCookbook.Data.CookbookDatabase;
using MyCookbook.Logging;
using System.Reflection;

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
            using var logger = new TimeLogger(MethodBase.GetCurrentMethod());
            return await _context.Recipes
                 .Where(x => x.UserName == user && x.Name == name)
                 .Include(x => x.Ingredients)
                 .Include(x => x.Steps)
                 .Include(x => x.FavoriteRecipes)
                 .AsSplitQuery().FirstOrDefaultAsync();
        }

        public async Task<Recipe?> GetDetailedRecipeByIdAsync(string id)
        {
            using var logger = new TimeLogger(MethodBase.GetCurrentMethod());
            return await _context.Recipes
                 .Where(x => x.Guid == new Guid(id))
                 .Include(x => x.Ingredients)
                 .Include(x => x.Steps)
                 .Include(x => x.FavoriteRecipes)
                 .AsNoTracking().AsSplitQuery().FirstOrDefaultAsync();
        }

        public async Task<List<Recipe>> GetRecipesAsync(string user)
        {
            using var logger = new TimeLogger(MethodBase.GetCurrentMethod());
            return await _context.Recipes
                 .Where(x => x.UserName == user)
                 .Include(x => x.FavoriteRecipes)
                 .AsNoTracking().ToListAsync();
        }

        public async Task<List<Recipe>> GetFavoriteRecipesAsync(string user)
        {
            using var logger = new TimeLogger(MethodBase.GetCurrentMethod());
            return await _context.FavoriteRecipes
                 .Where(x => x.UserName == user)
                 .Join(_context.Recipes,
                 x => x.RecipeId,
                 x => x.Id,
                 (favorite, recipe) => recipe)
                 .Include(x => x.FavoriteRecipes)
                 .AsNoTracking().ToListAsync();
        }

        public async Task<Recipe> CreateRecipeAsync(Recipe recipe, string user)
        {
            using var logger = new TimeLogger(MethodBase.GetCurrentMethod());
            recipe.UserName = user;
            await _context.Recipes.AddAsync(recipe);
            await _context.SaveChangesAsync();
            return recipe;
        }

        public async Task<bool> UpdateRecipeAsync(Recipe recipe, string user)
        {
            using var logger = new TimeLogger(MethodBase.GetCurrentMethod());
            var foundRecipe =
                _context.Recipes
                .Where(x => x.Id == recipe.Id && x.UserName == user)
                .FirstOrDefault();

            if (foundRecipe == null) return false;

            foundRecipe.Name = recipe.Name;
            foundRecipe.Category = recipe.Category;
            foundRecipe.Duration = recipe.Duration;
            foundRecipe.Servings = recipe.Servings;
            await _context.SaveChangesAsync();

            return true;
        }

        public async Task<bool> UpdateRecipeLastCookedAsync(Recipe recipe, string user)
        {
            using var logger = new TimeLogger(MethodBase.GetCurrentMethod());
            var foundRecipe =
                _context.Recipes
                .Where(x => x.Id == recipe.Id && x.UserName == user)
                .FirstOrDefault();

            if (foundRecipe == null) return false;

            foundRecipe.LastCooked = DateTime.Now;
            await _context.SaveChangesAsync();

            return true;
        }

        public async Task<bool> DeleteRecipeAsync(Recipe recipe, string user)
        {
            using var logger = new TimeLogger(MethodBase.GetCurrentMethod());
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

        public async Task<Recipe> CloneRecipeAsync(Recipe recipe, string user)
        {
            using var logger = new TimeLogger(MethodBase.GetCurrentMethod());
            recipe = await GetDetailedRecipeAsync(recipe.Name, recipe.UserName) ?? recipe;
            var newRecipe = recipe.Clone();
            newRecipe.UserName = user;
            var name = newRecipe.Name;
            (name, int highestIndex) = await GetHighestRecipeNameIndex(name, user);
            newRecipe.Name = highestIndex >= 0 ? $"{name} ({highestIndex + 1})" : name;
            _context.Recipes.Add(newRecipe);
            await _context.SaveChangesAsync();

            newRecipe.Ingredients = recipe.Ingredients.Select(x => x.Clone(recipe)).ToList();
            newRecipe.Steps = recipe.Steps.Select(x => x.Clone(recipe)).ToList();

            await _context.SaveChangesAsync();

            if (recipe.FavoriteRecipes != null && recipe.FavoriteRecipes.Any(x => x.UserName == user))
            {
                await AddFavoriteAsync(newRecipe, user);
            }

            return newRecipe;
        }

        private async Task<(string, int)> GetHighestRecipeNameIndex(string name, string user)
        {
            using var logger = new TimeLogger(MethodBase.GetCurrentMethod());
            var highestIndex = -1;
            var namePattern = @$"(.*) \(\d+\)";
            Regex nameRegex = new Regex(namePattern);

            var matches = nameRegex.Match(name);
            if (matches.Success)
            {
                name = matches.Groups[1].Value;
            }

            var indexPattern = @$"{name} \((\d+)\)";
            Regex indexRegex = new Regex(indexPattern);

            var recipes = await GetRecipesAsync(user);

            foreach (var recipe in recipes)
            {
                matches = indexRegex.Match(recipe.Name);
                if (matches.Success)
                {
                    var index = int.Parse(matches.Groups[1].Value);
                    if (index > highestIndex)
                    {
                        highestIndex = index;
                    }
                }
                else if (highestIndex == -1 && recipe.Name == name)
                {
                    highestIndex = 0;
                }
            }
            return (name, highestIndex);
        }

        public async Task<FavoriteRecipe> AddFavoriteAsync(Recipe recipe, string user)
        {
            using var logger = new TimeLogger(MethodBase.GetCurrentMethod());
            var favoriteRecipe = new FavoriteRecipe
            {
                UserName = user,
                RecipeId = recipe.Id
            };
            await _context.FavoriteRecipes.AddAsync(favoriteRecipe);
            await _context.SaveChangesAsync();
            return favoriteRecipe;
        }

        public async Task<bool> DeleteFavoriteAsync(int recipeId, string user)
        {
            using var logger = new TimeLogger(MethodBase.GetCurrentMethod());
            var foundFavorite = await
                _context.FavoriteRecipes
                .Where(x => x.RecipeId == recipeId && x.UserName == user)
                .FirstOrDefaultAsync();

            if (foundFavorite == null) return false;

            _context.FavoriteRecipes.Remove(foundFavorite);
            await _context.SaveChangesAsync();

            return true;
        }

        public async Task<Step> CreateStepAsync(Step step, string user)
        {
            using var logger = new TimeLogger(MethodBase.GetCurrentMethod());
            step.UserName = user;
            await _context.Steps.AddAsync(step);
            await _context.SaveChangesAsync();
            return step;
        }

        public async Task<bool> UpdateStepDescriptionAsync(Step step, string user)
        {
            using var logger = new TimeLogger(MethodBase.GetCurrentMethod());
            if (step.Description.Length > 10000) return false;

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
            using var logger = new TimeLogger(MethodBase.GetCurrentMethod());
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
            using var logger = new TimeLogger(MethodBase.GetCurrentMethod());
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
            using var logger = new TimeLogger(MethodBase.GetCurrentMethod());
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
            using var logger = new TimeLogger(MethodBase.GetCurrentMethod());
            var foundSteps = await _context.Steps
                .Where(x => x.RecipeId == recipe.Id && x.UserName == user).OrderBy(x => x.Order).ToListAsync();

            for (int i = 0; i < foundSteps.Count; i++)
            {
                foundSteps[i].Order = i + 1;
            }

            await _context.SaveChangesAsync();

            return true;
        }

        public async Task<bool> FixIngredientsOrder(Recipe recipe, string user)
        {
            using var logger = new TimeLogger(MethodBase.GetCurrentMethod());
            var foundIngredients = await _context.Ingredients
                .Where(x => x.RecipeId == recipe.Id && x.UserName == user).OrderBy(x => x.Order).ToListAsync();

            for (int i = 0; i < foundIngredients.Count; i++)
            {
                foundIngredients[i].Order = i + 1;
            }

            await _context.SaveChangesAsync();

            return true;
        }

        public async Task<Ingredient> CreateIngredientAsync(Ingredient ingredient, string user)
        {
            using var logger = new TimeLogger(MethodBase.GetCurrentMethod());
            ingredient.UserName = user;
            await _context.Ingredients.AddAsync(ingredient);
            await _context.SaveChangesAsync();
            return ingredient;
        }

        public async Task<bool> UpdateIngredientAsync(Ingredient ingredient, string user)
        {
            using var logger = new TimeLogger(MethodBase.GetCurrentMethod());
            var foundIngredient =
                _context.Ingredients
                .Where(x => x.Id == ingredient.Id && x.UserName == user)
                .FirstOrDefault();

            if (foundIngredient == null) return false;

            foundIngredient.Name = ingredient.Name;
            foundIngredient.Amount = ingredient.Amount;
            await _context.SaveChangesAsync();

            return true;
        }

        public async Task<bool> DeleteIngredientAsync(Ingredient ingredient, string user)
        {
            using var logger = new TimeLogger(MethodBase.GetCurrentMethod());
            var foundIngredient =
                _context.Ingredients
                .Where(x => x.Id == ingredient.Id && x.UserName == user)
                .FirstOrDefault();

            if (foundIngredient == null) return false;

            var higherIngredients = await
                _context.Ingredients
                .Where(x => x.RecipeId == ingredient.RecipeId && x.UserName == user)
                .Where(x => x.Order > ingredient.Order)
                .ToListAsync();

            foreach (var higherIngredient in higherIngredients)
            {
                higherIngredient.Order--;
            }

            _context.Ingredients.Remove(foundIngredient);
            await _context.SaveChangesAsync();

            return true;
        }

        public async Task<bool> IncreaseIngredientOrder(Ingredient ingredient, string user)
        {
            using var logger = new TimeLogger(MethodBase.GetCurrentMethod());
            var foundIngredient =
                _context.Ingredients
                .Where(x => x.Id == ingredient.Id && x.UserName == user)
                .FirstOrDefault();

            if (foundIngredient == null) return false;

            var higherIngredient = _context.Ingredients
                .Where(x => x.RecipeId == ingredient.RecipeId && x.UserName == user && x.Order == ingredient.Order + 1).FirstOrDefault();

            if (higherIngredient == null) return true;

            higherIngredient.Order--;
            foundIngredient.Order++;

            await _context.SaveChangesAsync();

            return true;
        }

        public async Task<bool> DecreaseIngredientOrder(Ingredient ingredient, string user)
        {
            using var logger = new TimeLogger(MethodBase.GetCurrentMethod());
            var foundIngredient =
                _context.Ingredients
                .Where(x => x.Id == ingredient.Id && x.UserName == user)
                .FirstOrDefault();

            if (foundIngredient == null) return false;

            var lowerIngredient = _context.Ingredients
                .Where(x => x.RecipeId == ingredient.RecipeId && x.UserName == user && x.Order == ingredient.Order - 1).FirstOrDefault();

            if (lowerIngredient == null) return true;

            foundIngredient.Order--;
            lowerIngredient.Order++;

            await _context.SaveChangesAsync();

            return true;
        }
    }
}