namespace MyCookbook.Api.Dto;

public class IngredientDto
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string? Amount { get; set; }
    public int Order { get; set; }
}
