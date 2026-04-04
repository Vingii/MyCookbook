using MyCookbook.Api;
using MyCookbook.Data.CookbookDatabase;
using MyCookbook.Model;

namespace MyCookbook.Test.Api;

public class DtoMapperTests
{
    [Fact]
    public void RecipeToDto_MapsAllScalarFields()
    {
        var lastCooked = new DateTime(2025, 1, 15);
        var recipe = new Recipe
        {
            Guid = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000000"),
            Name = "Pancakes",
            Category = "Breakfast",
            Duration = 30,
            Servings = 4,
            LastCooked = lastCooked
        };

        var dto = recipe.ToDto("user1");

        Assert.Equal(recipe.Guid, dto.Guid);
        Assert.Equal("Pancakes", dto.Name);
        Assert.Equal("Breakfast", dto.Category);
        Assert.Equal(30, dto.Duration);
        Assert.Equal(4, dto.Servings);
        Assert.Equal(lastCooked, dto.LastCooked);
    }

    [Fact]
    public void RecipeToDto_DurationText_NullDuration_ReturnsEmpty()
    {
        var recipe = new Recipe { Name = "Test", Duration = null };
        var dto = recipe.ToDto("user");
        Assert.Equal("", dto.DurationText);
    }

    [Fact]
    public void RecipeToDto_DurationText_90Minutes_FormatsCorrectly()
    {
        var recipe = new Recipe { Name = "Test", Duration = 90 };
        var dto = recipe.ToDto("user");
        Assert.Equal("1:30", dto.DurationText);
    }

    [Fact]
    public void RecipeToDto_DurationText_5Minutes_PadsMinutes()
    {
        var recipe = new Recipe { Name = "Test", Duration = 5 };
        var dto = recipe.ToDto("user");
        Assert.Equal("0:05", dto.DurationText);
    }

    [Fact]
    public void RecipeToDto_IsFavorite_TrueWhenUserHasFavorite()
    {
        var recipe = new Recipe { Name = "Test" };
        recipe.FavoriteRecipes = new List<FavoriteRecipe>
        {
            new FavoriteRecipe { UserName = "testuser", RecipeId = recipe.Id }
        };

        var dto = recipe.ToDto("testuser");

        Assert.True(dto.IsFavorite);
    }

    [Fact]
    public void RecipeToDto_IsFavorite_FalseForDifferentUser()
    {
        var recipe = new Recipe { Name = "Test" };
        recipe.FavoriteRecipes = new List<FavoriteRecipe>
        {
            new FavoriteRecipe { UserName = "otheruser", RecipeId = recipe.Id }
        };

        var dto = recipe.ToDto("testuser");

        Assert.False(dto.IsFavorite);
    }

    [Fact]
    public void RecipeToDto_IsFavorite_FalseWhenFavoriteCollectionNull()
    {
        var recipe = new Recipe { Name = "Test" };
        recipe.FavoriteRecipes = null;

        var dto = recipe.ToDto("testuser");

        Assert.False(dto.IsFavorite);
    }

    [Fact]
    public void RecipeToDto_OrdersIngredientsByOrder()
    {
        var recipe = new Recipe { Name = "Test" };
        recipe.Ingredients = new List<Ingredient>
        {
            new Ingredient { Name = "Third", Order = 3 },
            new Ingredient { Name = "First", Order = 1 },
            new Ingredient { Name = "Second", Order = 2 },
        };

        var dto = recipe.ToDto("user");

        Assert.Equal(new[] { "First", "Second", "Third" }, dto.Ingredients.Select(i => i.Name));
    }

    [Fact]
    public void RecipeToDto_OrdersStepsByOrder()
    {
        var recipe = new Recipe { Name = "Test" };
        recipe.Steps = new List<Step>
        {
            new Step { Description = "Third", Order = 3 },
            new Step { Description = "First", Order = 1 },
            new Step { Description = "Second", Order = 2 },
        };

        var dto = recipe.ToDto("user");

        Assert.Equal(new[] { "First", "Second", "Third" }, dto.Steps.Select(s => s.Description));
    }

    [Fact]
    public void RecipeToDto_NullIngredients_ReturnsEmptyList()
    {
        var recipe = new Recipe { Name = "Test" };
        recipe.Ingredients = null;

        var dto = recipe.ToDto("user");

        Assert.Empty(dto.Ingredients);
    }

    [Fact]
    public void IngredientToDto_MapsAllFields()
    {
        var ingredient = new Ingredient { Id = 7, Name = "Flour", Amount = "200g", Order = 2 };

        var dto = ingredient.ToDto();

        Assert.Equal(7, dto.Id);
        Assert.Equal("Flour", dto.Name);
        Assert.Equal("200g", dto.Amount);
        Assert.Equal(2, dto.Order);
    }

    [Fact]
    public void StepToDto_DurationSeconds_ConvertsTimeSpanToSeconds()
    {
        var step = new Step { Description = "Mix", Duration = TimeSpan.FromSeconds(150) };

        var dto = step.ToDto();

        Assert.Equal(150, dto.DurationSeconds);
    }

    [Fact]
    public void StepToDto_DurationSeconds_NullWhenNoDuration()
    {
        var step = new Step { Description = "Mix", Duration = null };

        var dto = step.ToDto();

        Assert.Null(dto.DurationSeconds);
    }

    [Fact]
    public void StepToDto_StepType_SerializedAsString()
    {
        var step = new Step { Description = "Simmer", StepType = StepType.SemiPassive };

        var dto = step.ToDto();

        Assert.Equal("SemiPassive", dto.StepType);
    }

    [Fact]
    public void PlannedRecipeToDto_DateFormattedAsIso()
    {
        var planned = new PlannedRecipe
        {
            Id = 1,
            RecipeId = 2,
            Date = new DateOnly(2025, 3, 5),
            Recipe = new Recipe { Guid = Guid.NewGuid(), Name = "Pasta" }
        };

        var dto = planned.ToDto();

        Assert.Equal("2025-03-05", dto.Date);
    }

    [Theory]
    [InlineData("Active", StepType.Active)]
    [InlineData("active", StepType.Active)]
    [InlineData("SemiPassive", StepType.SemiPassive)]
    [InlineData("semipassive", StepType.SemiPassive)]
    [InlineData("Passive", StepType.Passive)]
    [InlineData("garbage", StepType.Active)]
    [InlineData("", StepType.Active)]
    public void ParseStepType_ValidAndInvalidValues(string input, StepType expected)
    {
        Assert.Equal(expected, DtoMapper.ParseStepType(input));
    }
}
