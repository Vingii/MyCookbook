using MyCookbook.Api.Dto;
using MyCookbook.Data.CookbookDatabase;
using MyCookbook.Model;

namespace MyCookbook.Api;

public static class DtoMapper
{
    public static RecipeDto ToDto(this Recipe recipe, string user) => new()
    {
        Guid = recipe.Guid,
        Name = recipe.Name,
        Category = recipe.Category,
        Duration = recipe.Duration,
        DurationText = recipe.DurationText,
        Servings = recipe.Servings,
        LastCooked = recipe.LastCooked,
        IsFavorite = recipe.IsFavorite(user),
        Tags = recipe.Tags?.Select(t => t.Name).ToList() ?? [],
        Ingredients = recipe.Ingredients?
            .OrderBy(i => i.Order)
            .Select(i => i.ToDto())
            .ToList() ?? [],
        Steps = recipe.Steps?
            .OrderBy(s => s.Order)
            .Select(s => s.ToDto())
            .ToList() ?? []
    };

    public static IngredientDto ToDto(this Ingredient ingredient) => new()
    {
        Id = ingredient.Id,
        Name = ingredient.Name,
        Amount = ingredient.Amount,
        Order = ingredient.Order
    };

    public static StepDto ToDto(this Step step) => new()
    {
        Id = step.Id,
        Description = step.Description,
        Order = step.Order,
        DurationSeconds = step.Duration.HasValue ? (int)step.Duration.Value.TotalSeconds : null,
        StepType = step.StepType.ToString()
    };

    public static PlannedRecipeDto ToDto(this PlannedRecipe planned) => new()
    {
        Id = planned.Id,
        RecipeId = planned.RecipeId,
        RecipeGuid = planned.Recipe?.Guid ?? Guid.Empty,
        RecipeName = planned.Recipe?.Name ?? "",
        Date = planned.Date.ToString("yyyy-MM-dd"),
        FromFridge = planned.FromFridge
    };

    public static StepType ParseStepType(string value) =>
        Enum.TryParse<StepType>(value, true, out var result) ? result : StepType.Active;
}
