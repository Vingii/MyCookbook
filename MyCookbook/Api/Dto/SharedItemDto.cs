using MyCookbook.Data.CookbookDatabase;

namespace MyCookbook.Api.Dto;

/// <summary>A recipe or cookbook shared with the current user, resolved against its owner's current data.</summary>
public class SharedItemDto
{
    public int Id { get; set; }
    public SharedItemKind Kind { get; set; }

    /// <summary>Owner's user id, for building <c>?user=</c> links.</summary>
    public string Owner { get; set; } = "";

    /// <summary>Owner's display name when known, otherwise their user id.</summary>
    public string OwnerName { get; set; } = "";

    public Guid? RecipeGuid { get; set; }

    /// <summary>The recipe's current name. Null when the recipe no longer exists.</summary>
    public string? RecipeName { get; set; }

    public string? ShareToken { get; set; }
    public DateTime LastOpened { get; set; }
    public bool Pinned { get; set; }

    /// <summary>False once the recipe is deleted or the owner revoked the cookbook link.</summary>
    public bool Available { get; set; }
}

public class RecordSharedCookbookRequest
{
    /// <summary>The <c>?user=</c> value from the share link: a user id or display name.</summary>
    public string User { get; set; } = "";
    public string ShareToken { get; set; } = "";
}

public class SetPinnedRequest
{
    public bool Pinned { get; set; }
}
