namespace MyCookbook.Api.Dto;

public class PlannedRecipeDto
{
    public int Id { get; set; }
    public int RecipeId { get; set; }
    public Guid RecipeGuid { get; set; }
    public string RecipeName { get; set; } = "";
    public string Date { get; set; } = "";
    public bool FromFridge { get; set; }
}
