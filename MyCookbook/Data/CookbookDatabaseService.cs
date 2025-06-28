using Microsoft.EntityFrameworkCore;
using System.Text.RegularExpressions;
using MyCookbook.Data.CookbookDatabase;
using MyCookbook.Logging;
using System.Reflection;

namespace MyCookbook.Data
{
    public class CookbookDatabaseService
    {
        private readonly IDbContextFactory<CookbookDatabaseContext> _contextFactory;

        public CookbookDatabaseService(IDbContextFactory<CookbookDatabaseContext> contextFactory)
        {
            _contextFactory = contextFactory;
        }

        private async Task<CookbookDatabaseContext> GetContext()
        {
            return await _contextFactory.CreateDbContextAsync();
        }

        public async Task<Recipe?> GetDetailedRecipeAsync(string name, string user)
        {
            using var logger = new TimeLogger(MethodBase.GetCurrentMethod());
            var context = await GetContext();
            return await context.Recipes
                 .Where(x => x.UserName == user && x.Name == name)
                 .Include(x => x.Ingredients)
                 .Include(x => x.Steps)
                 .Include(x => x.FavoriteRecipes)
                 .Include(x => x.Tags)
                 .AsSplitQuery().FirstOrDefaultAsync();
        }

        public async Task<Recipe?> GetDetailedRecipeByIdAsync(string id)
        {
            using var logger = new TimeLogger(MethodBase.GetCurrentMethod());
            var context = await GetContext();
            return await context.Recipes
                 .Where(x => x.Guid == new Guid(id))
                 .Include(x => x.Ingredients)
                 .Include(x => x.Steps)
                 .Include(x => x.FavoriteRecipes)
                 .Include(x => x.Tags)
                 .AsNoTracking().AsSplitQuery().FirstOrDefaultAsync();
        }

        public async Task<List<Recipe>> GetRecipesAsync(string user)
        {
            using var logger = new TimeLogger(MethodBase.GetCurrentMethod());
            var context = await GetContext();
            return await context.Recipes
                 .Where(x => x.UserName == user)
                 .Include(x => x.FavoriteRecipes)
                 .Include(x => x.Tags)
                 .AsNoTracking().ToListAsync();
        }

        public async Task<List<Recipe>> GetFavoriteRecipesAsync(string user)
        {
            using var logger = new TimeLogger(MethodBase.GetCurrentMethod());
            var context = await GetContext();
            return await context.FavoriteRecipes
                 .Where(x => x.UserName == user)
                 .Join(context.Recipes,
                 x => x.RecipeId,
                 x => x.Id,
                 (favorite, recipe) => recipe)
                 .Include(x => x.FavoriteRecipes)
                 .Include(x => x.Tags)
                 .AsNoTracking().ToListAsync();
        }

        public async Task<Recipe> CreateRecipeAsync(Recipe recipe, string user)
        {
            using var logger = new TimeLogger(MethodBase.GetCurrentMethod());
            var context = await GetContext();
            recipe.UserName = user;
            await context.Recipes.AddAsync(recipe);
            await context.SaveChangesAsync();
            return recipe;
        }

        public async Task<bool> UpdateRecipeAsync(Recipe recipe, string user)
        {
            using var logger = new TimeLogger(MethodBase.GetCurrentMethod());
            var context = await GetContext();
            var foundRecipe =
                context.Recipes
                .Where(x => x.Id == recipe.Id && x.UserName == user)
                .FirstOrDefault();

            if (foundRecipe == null) return false;

            foundRecipe.Name = recipe.Name;
            foundRecipe.Category = recipe.Category;
            foundRecipe.Duration = recipe.Duration;
            foundRecipe.Servings = recipe.Servings;
            await context.SaveChangesAsync();

            return true;
        }

        public async Task<bool> UpdateRecipeLastCookedAsync(Recipe recipe, string user)
        {
            using var logger = new TimeLogger(MethodBase.GetCurrentMethod());
            var context = await GetContext();
            var foundRecipe =
                context.Recipes
                .Where(x => x.Id == recipe.Id && x.UserName == user)
                .FirstOrDefault();

            if (foundRecipe == null) return false;

            foundRecipe.LastCooked = DateTime.Now;
            await context.SaveChangesAsync();

            return true;
        }

        public async Task<bool> DeleteRecipeAsync(Recipe recipe, string user)
        {
            using var logger = new TimeLogger(MethodBase.GetCurrentMethod());
            var context = await GetContext();
            var foundRecipe =
                context.Recipes
                .Where(x => x.Id == recipe.Id && x.UserName == user)
                .Include(x => x.Ingredients)
                .Include(x => x.Steps)
                .Include(x => x.Tags)
                .FirstOrDefault();

            if (foundRecipe == null) return false;

            context.Recipes.Remove(foundRecipe);
            await context.SaveChangesAsync();

            return true;
        }

        public async Task<Recipe> CloneRecipeAsync(Recipe recipe, string user)
        {
            using var logger = new TimeLogger(MethodBase.GetCurrentMethod());
            var context = await GetContext();
            recipe = await GetDetailedRecipeAsync(recipe.Name, recipe.UserName) ?? recipe;
            var newRecipe = recipe.Clone();
            newRecipe.UserName = user;
            var name = newRecipe.Name;
            (name, int highestIndex) = await GetHighestRecipeNameIndex(name, user);
            newRecipe.Name = highestIndex >= 0 ? $"{name} ({highestIndex + 1})" : name;
            context.Recipes.Add(newRecipe);
            await context.SaveChangesAsync();

            newRecipe.Ingredients = recipe.Ingredients.Select(x => x.Clone(recipe)).ToList();
            newRecipe.Steps = recipe.Steps.Select(x => x.Clone(recipe)).ToList();
            newRecipe.Tags = recipe.Tags.Select(x => x.Clone(recipe)).ToList();

            await context.SaveChangesAsync();

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
            var context = await GetContext();
            var favoriteRecipe = new FavoriteRecipe
            {
                UserName = user,
                RecipeId = recipe.Id
            };
            await context.FavoriteRecipes.AddAsync(favoriteRecipe);
            await context.SaveChangesAsync();
            return favoriteRecipe;
        }

        public async Task<bool> DeleteFavoriteAsync(int recipeId, string user)
        {
            using var logger = new TimeLogger(MethodBase.GetCurrentMethod());
            var context = await GetContext();
            var foundFavorite = await
                context.FavoriteRecipes
                .Where(x => x.RecipeId == recipeId && x.UserName == user)
                .FirstOrDefaultAsync();

            if (foundFavorite == null) return false;

            context.FavoriteRecipes.Remove(foundFavorite);
            await context.SaveChangesAsync();

            return true;
        }

        public async Task<Step> CreateStepAsync(Step step, string user)
        {
            using var logger = new TimeLogger(MethodBase.GetCurrentMethod());
            var context = await GetContext();
            step.UserName = user;
            await context.Steps.AddAsync(step);
            await context.SaveChangesAsync();
            return step;
        }

        public async Task<bool> UpdateStepDescriptionAsync(Step step, string user)
        {
            using var logger = new TimeLogger(MethodBase.GetCurrentMethod());
            var context = await GetContext();
            if (step.Description.Length > 10000) return false;

            var foundStep =
                context.Steps
                .Where(x => x.Id == step.Id && x.UserName == user)
                .FirstOrDefault();

            if (foundStep == null) return false;

            foundStep.Description = step.Description;
            await context.SaveChangesAsync();

            return true;
        }

        public async Task<bool> IncreaseStepOrder(Step step, string user)
        {
            using var logger = new TimeLogger(MethodBase.GetCurrentMethod());
            var context = await GetContext();
            var foundStep =
                context.Steps
                .Where(x => x.Id == step.Id && x.UserName == user)
                .FirstOrDefault();

            if (foundStep == null) return false;

            var higherStep = context.Steps
                .Where(x => x.RecipeId == step.RecipeId && x.UserName == user && x.Order == step.Order + 1).FirstOrDefault();

            if (higherStep == null) return true;

            higherStep.Order--;
            foundStep.Order++;

            await context.SaveChangesAsync();

            return true;
        }

        public async Task<bool> DecreaseStepOrder(Step step, string user)
        {
            using var logger = new TimeLogger(MethodBase.GetCurrentMethod());
            var context = await GetContext();
            var foundStep =
                context.Steps
                .Where(x => x.Id == step.Id && x.UserName == user)
                .FirstOrDefault();

            if (foundStep == null) return false;

            var lowerStep = context.Steps
                .Where(x => x.RecipeId == step.RecipeId && x.UserName == user && x.Order == step.Order - 1).FirstOrDefault();

            if (lowerStep == null) return true;

            foundStep.Order--;
            lowerStep.Order++;

            await context.SaveChangesAsync();

            return true;
        }

        public async Task<bool> DeleteStepAsync(Step step, string user)
        {
            using var logger = new TimeLogger(MethodBase.GetCurrentMethod());
            var context = await GetContext();
            var foundStep = await
                context.Steps
                .Where(x => x.Id == step.Id && x.UserName == user)
                .FirstOrDefaultAsync();

            if (foundStep == null) return false;

            var higherSteps = await
                context.Steps
                .Where(x => x.RecipeId == step.RecipeId && x.UserName == user)
                .Where(x => x.Order > step.Order)
                .ToListAsync();

            foreach (var higherStep in higherSteps)
            {
                higherStep.Order--;
            }

            context.Steps.Remove(foundStep);
            await context.SaveChangesAsync();

            return true;
        }

        public async Task<bool> FixStepsOrder(Recipe recipe, string user)
        {
            using var logger = new TimeLogger(MethodBase.GetCurrentMethod());
            var context = await GetContext();
            var foundSteps = await context.Steps
                .Where(x => x.RecipeId == recipe.Id && x.UserName == user).OrderBy(x => x.Order).ToListAsync();

            for (int i = 0; i < foundSteps.Count; i++)
            {
                foundSteps[i].Order = i + 1;
            }

            await context.SaveChangesAsync();

            return true;
        }

        public async Task<bool> FixIngredientsOrder(Recipe recipe, string user)
        {
            using var logger = new TimeLogger(MethodBase.GetCurrentMethod());
            var context = await GetContext();
            var foundIngredients = await context.Ingredients
                .Where(x => x.RecipeId == recipe.Id && x.UserName == user).OrderBy(x => x.Order).ToListAsync();

            for (int i = 0; i < foundIngredients.Count; i++)
            {
                foundIngredients[i].Order = i + 1;
            }

            await context.SaveChangesAsync();

            return true;
        }

        public async Task<Ingredient> CreateIngredientAsync(Ingredient ingredient, string user)
        {
            using var logger = new TimeLogger(MethodBase.GetCurrentMethod());
            var context = await GetContext();
            ingredient.UserName = user;
            await context.Ingredients.AddAsync(ingredient);
            await context.SaveChangesAsync();
            return ingredient;
        }

        public async Task<bool> UpdateIngredientAsync(Ingredient ingredient, string user)
        {
            using var logger = new TimeLogger(MethodBase.GetCurrentMethod());
            var context = await GetContext();
            var foundIngredient =
                context.Ingredients
                .Where(x => x.Id == ingredient.Id && x.UserName == user)
                .FirstOrDefault();

            if (foundIngredient == null) return false;

            foundIngredient.Name = ingredient.Name;
            foundIngredient.Amount = ingredient.Amount;
            await context.SaveChangesAsync();

            return true;
        }

        public async Task<bool> DeleteIngredientAsync(Ingredient ingredient, string user)
        {
            using var logger = new TimeLogger(MethodBase.GetCurrentMethod());
            var context = await GetContext();
            var foundIngredient =
                context.Ingredients
                .Where(x => x.Id == ingredient.Id && x.UserName == user)
                .FirstOrDefault();

            if (foundIngredient == null) return false;

            var higherIngredients = await
                context.Ingredients
                .Where(x => x.RecipeId == ingredient.RecipeId && x.UserName == user)
                .Where(x => x.Order > ingredient.Order)
                .ToListAsync();

            foreach (var higherIngredient in higherIngredients)
            {
                higherIngredient.Order--;
            }

            context.Ingredients.Remove(foundIngredient);
            await context.SaveChangesAsync();

            return true;
        }

        public async Task<bool> IncreaseIngredientOrder(Ingredient ingredient, string user)
        {
            using var logger = new TimeLogger(MethodBase.GetCurrentMethod());
            var context = await GetContext();
            var foundIngredient =
                context.Ingredients
                .Where(x => x.Id == ingredient.Id && x.UserName == user)
                .FirstOrDefault();

            if (foundIngredient == null) return false;

            var higherIngredient = context.Ingredients
                .Where(x => x.RecipeId == ingredient.RecipeId && x.UserName == user && x.Order == ingredient.Order + 1).FirstOrDefault();

            if (higherIngredient == null) return true;

            higherIngredient.Order--;
            foundIngredient.Order++;

            await context.SaveChangesAsync();

            return true;
        }

        public async Task<bool> DecreaseIngredientOrder(Ingredient ingredient, string user)
        {
            using var logger = new TimeLogger(MethodBase.GetCurrentMethod());
            var context = await GetContext();
            var foundIngredient =
                context.Ingredients
                .Where(x => x.Id == ingredient.Id && x.UserName == user)
                .FirstOrDefault();

            if (foundIngredient == null) return false;

            var lowerIngredient = context.Ingredients
                .Where(x => x.RecipeId == ingredient.RecipeId && x.UserName == user && x.Order == ingredient.Order - 1).FirstOrDefault();

            if (lowerIngredient == null) return true;

            foundIngredient.Order--;
            lowerIngredient.Order++;

            await context.SaveChangesAsync();

            return true;
        }

        public async Task<List<Tag>> GetAllTags(string user)
        {
            using var logger = new TimeLogger(MethodBase.GetCurrentMethod());
            var context = await GetContext();
            return await context.Tags.Where(x => x.UserName == user).AsNoTracking().ToListAsync();
        }

        public async Task<bool> AddTag(Recipe recipe, string user, string name)
        {
            using var logger = new TimeLogger(MethodBase.GetCurrentMethod());
            var context = await GetContext();
            var tag = new Tag
            {
                UserName = user,
                RecipeId = recipe.Id,
                Name = name,
            };

            await context.Tags.AddAsync(tag);
            return true;
        }

        public async Task<bool> UpdateUserPreference(string key, string value, string user)
        {
            using var logger = new TimeLogger(MethodBase.GetCurrentMethod());
            var context = await GetContext();
            var userPreference = new UserPreference
            {
                UserName = user,
                Key = key,
                Value = value
            };

            var exists = context.UserPreferences.Any(x => x.UserName == user && x.Key == key);

            if (exists)
            {
                context.UserPreferences.Update(userPreference);
            }
            else
            {
                await context.UserPreferences.AddAsync(userPreference);
            }
            await context.SaveChangesAsync();
            return true;
        }

        public async Task<string?> GetUserPreference(string key, string user)
        {
            using var logger = new TimeLogger(MethodBase.GetCurrentMethod());
            var context = await GetContext();
            var preference = await context.UserPreferences.Where(x => x.UserName == user && x.Key == key).AsNoTracking().FirstOrDefaultAsync();
            return preference?.Value;
        }
    }
}