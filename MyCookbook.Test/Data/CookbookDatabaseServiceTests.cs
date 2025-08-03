using Microsoft.EntityFrameworkCore;
using MyCookbook.Data;
using MyCookbook.Data.CookbookDatabase;
using MyCookbook.Test.Common;

namespace MyCookbook.Test.Data
{
    public class CookbookDatabaseServiceTests
    {
        private IDbContextFactory<CookbookDatabaseContext> _dbContextFactory;
        private void SetupDatabase() => _dbContextFactory = new TestDbContextFactory();

        [Fact]
        public async Task GetRecipesAsync_ShouldReturnOnlyUserRecipes()
        {
            // Arrange
            SetupDatabase();
            var service = new CookbookDatabaseService(_dbContextFactory);
            using var context = await _dbContextFactory.CreateDbContextAsync();

            await context.Recipes.AddAsync(new Recipe { Name = "User1 Recipe", UserName = "user1" });
            await context.Recipes.AddAsync(new Recipe { Name = "User2 Recipe", UserName = "user2" });
            await context.SaveChangesAsync();

            // Act
            var result = await service.GetRecipesAsync("user1");

            // Assert
            Assert.NotNull(result);
            Assert.Single(result);
            Assert.Equal("User1 Recipe", result.First().Name);
        }

        [Fact]
        public async Task CreateRecipeAsync_ShouldSaveRecipeAndSetUserName()
        {
            // Arrange
            SetupDatabase();
            var service = new CookbookDatabaseService(_dbContextFactory);
            var newRecipe = new Recipe { Name = "New Recipe" };

            // Act
            var result = await service.CreateRecipeAsync(newRecipe, "testuser");

            // Assert
            Assert.Equal("testuser", result.UserName);
            using var context = await _dbContextFactory.CreateDbContextAsync();

            var savedRecipe = await context.Recipes.FirstAsync();
            Assert.Equal("New Recipe", savedRecipe.Name);
            Assert.Equal("testuser", savedRecipe.UserName);

        }

        [Fact]
        public async Task UpdateRecipeAsync_ShouldReturnFalse_WhenRecipeNotFoundForUser()
        {
            // Arrange
            SetupDatabase();
            var service = new CookbookDatabaseService(_dbContextFactory);
            using var context = await _dbContextFactory.CreateDbContextAsync();

            await context.Recipes.AddAsync(new Recipe { Id = 1, Name = "Original Name", UserName = "user1" });
            await context.SaveChangesAsync();
            var recipeToUpdate = new Recipe { Id = 1, Name = "Updated Name" };


            // Act
            var result = await service.UpdateRecipeAsync(recipeToUpdate, "user2");

            // Assert
            Assert.False(result);
            var savedRecipe = await context.Recipes.FirstAsync();
            Assert.Equal("Original Name", savedRecipe.Name);
        }

        [Fact]
        public async Task DeleteRecipeAsync_ShouldDeleteRecipeAndReturnTrue()
        {
            // Arrange
            SetupDatabase();
            var service = new CookbookDatabaseService(_dbContextFactory);
            var recipeToDelete = new Recipe { Id = 1, Name = "To Delete", UserName = "user1" };
            using var context = await _dbContextFactory.CreateDbContextAsync();
            
            await context.Recipes.AddAsync(recipeToDelete);
            await context.SaveChangesAsync();

            // Act
            var result = await service.DeleteRecipeAsync(recipeToDelete, "user1");

            // Assert
            Assert.True(result);
            Assert.Empty(context.Recipes);
        }

        [Fact]
        public async Task CloneRecipeAsync_WhenNoConflict_ShouldCreateCopyWithSuffix()
        {
            // Arrange
            SetupDatabase();
            var service = new CookbookDatabaseService(_dbContextFactory);
            var originalRecipe = new Recipe { Name = "Pancakes", UserName = "user1" };
            originalRecipe.Ingredients.Add(new Ingredient { Name = "Flour", Amount = "1 cup", UserName = "user1" });

            using var context = await _dbContextFactory.CreateDbContextAsync();

            await context.Recipes.AddAsync(originalRecipe);
            await context.SaveChangesAsync();


            // Act
            var clonedRecipe = await service.CloneRecipeAsync(originalRecipe, "user1");

            // Assert
            Assert.Equal("Pancakes (1)", clonedRecipe.Name);
            Assert.Equal("user1", clonedRecipe.UserName);
            Assert.NotEqual(originalRecipe.Id, clonedRecipe.Id);

            var recipes = await context.Recipes.ToListAsync();
            Assert.Equal(2, recipes.Count);
            Assert.Contains(recipes, r => r.Name == "Pancakes");
            Assert.Contains(recipes, r => r.Name == "Pancakes (1)");

            // Verify ingredients were cloned
            var clonedIngredients = await context.Ingredients.Where(i => i.RecipeId == clonedRecipe.Id).ToListAsync();
            Assert.Single(clonedIngredients);
            Assert.Equal("Flour", clonedIngredients.First().Name);
        }

        [Fact]
        public async Task CloneRecipeAsync_WithExistingSuffixes_ShouldFindHighestIndex()
        {
            // Arrange
            SetupDatabase();
            var service = new CookbookDatabaseService(_dbContextFactory);
            var originalRecipe = new Recipe { Name = "Pizza", UserName = "user1" };
            using var context = await _dbContextFactory.CreateDbContextAsync();

            await context.Recipes.AddAsync(new Recipe { Name = "Pizza", UserName = "user1" });
            await context.Recipes.AddAsync(new Recipe { Name = "Pizza (1)", UserName = "user1" });
            await context.Recipes.AddAsync(new Recipe { Name = "Pizza (3)", UserName = "user1" });
            await context.SaveChangesAsync();

            // Act
            var clonedRecipe = await service.CloneRecipeAsync(originalRecipe, "user1");

            // Assert
            Assert.Equal("Pizza (4)", clonedRecipe.Name);
        }

        [Fact]
        public async Task DeleteStepAsync_ShouldReorderSubsequentSteps()
        {
            // Arrange
            SetupDatabase();
            var service = new CookbookDatabaseService(_dbContextFactory);
            var recipe = new Recipe { Id = 1, Name = "Test Recipe", UserName = "user1" };
            var step1 = new Step { Id = 10, RecipeId = 1, Order = 1, UserName = "user1", Description = "Step One" };
            var step2 = new Step { Id = 11, RecipeId = 1, Order = 2, UserName = "user1", Description = "Step Two" };
            var step3 = new Step { Id = 12, RecipeId = 1, Order = 3, UserName = "user1", Description = "Step Three" };

            using var context = await _dbContextFactory.CreateDbContextAsync();

            await context.Recipes.AddAsync(recipe);
            await context.Steps.AddRangeAsync(step1, step2, step3);
            await context.SaveChangesAsync();

            // Act
            var result = await service.DeleteStepAsync(step2, "user1");

            // Assert
            Assert.True(result);

            var remainingSteps = await context.Steps.OrderBy(s => s.Order).ToListAsync();
            Assert.Equal(2, remainingSteps.Count);
            Assert.Equal(1, remainingSteps[0].Order);
            Assert.Equal("Step One", remainingSteps[0].Description);
            Assert.Equal(2, remainingSteps[1].Order);
            Assert.Equal("Step Three", remainingSteps[1].Description);
        }
    }
}