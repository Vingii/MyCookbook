namespace MyCookbook.Api.Dto;

/// <summary>A recipe shared with the current user, resolved against its owner's current data.</summary>
public class SharedItemDto
{
    public int Id { get; set; }

    /// <summary>Owner's display name when known, otherwise their user id.</summary>
    public string OwnerName { get; set; } = "";

    public Guid RecipeGuid { get; set; }

    /// <summary>The recipe's current name. Null when the recipe no longer exists.</summary>
    public string? RecipeName { get; set; }

    public DateTime LastOpened { get; set; }
    public bool Pinned { get; set; }

    /// <summary>False once the owner deleted the recipe.</summary>
    public bool Available { get; set; }
}

public class SetPinnedRequest
{
    public bool Pinned { get; set; }
}
