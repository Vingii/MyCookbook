namespace MyCookbook.Api.Dto;

/// <summary>A <c>[[wiki link]]</c> found in a recipe's steps, resolved to the recipe it points at.</summary>
public class RecipeLinkDto
{
    /// <summary>The reference part of the link (after the pipe), as the user typed it.</summary>
    public string Text { get; set; } = "";

    /// <summary>The name of the recipe the link resolves to.</summary>
    public string Name { get; set; } = "";

    public Guid Guid { get; set; }
}

/// <summary>A recipe that links to the one being viewed.</summary>
public class RecipeRefDto
{
    public Guid Guid { get; set; }
    public string Name { get; set; } = "";
}
