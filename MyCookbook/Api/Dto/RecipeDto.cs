namespace MyCookbook.Api.Dto;

public class RecipeDto
{
    public Guid Guid { get; set; }
    public string Name { get; set; } = "";
    public string? Category { get; set; }
    public int? Duration { get; set; }
    public string DurationText { get; set; } = "";
    public int Servings { get; set; }
    public DateTime? LastCooked { get; set; }
    public bool IsFavorite { get; set; }
    public List<string> Tags { get; set; } = [];
    public List<IngredientDto> Ingredients { get; set; } = [];
    public List<StepDto> Steps { get; set; } = [];
}
