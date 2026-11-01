using Microsoft.EntityFrameworkCore;
using MyCookbook.Api.Dto;
using MyCookbook.Data;
using MyCookbook.Data.CookbookDatabase;

namespace MyCookbook.Services;

/// <summary>
/// Remembers recipes and cookbooks other users shared with the current user, so they can be found
/// again from the "Shared" page (MC-71).
/// </summary>
public class SharedItemService(CookbookDatabaseService db)
{
    public enum RecordResult { Recorded, Ignored, NotFound, Forbidden }

    /// <summary>Records that <paramref name="user"/> opened a shared single-recipe link.</summary>
    public async Task<RecordResult> RecordRecipeAsync(string user, Guid recipeGuid)
    {
        await using var context = await db.GetContext();
        var owner = await context.Recipes
            .Where(r => r.Guid == recipeGuid)
            .Select(r => r.UserName)
            .FirstOrDefaultAsync();
        if (owner == null) return RecordResult.NotFound;
        if (owner == user) return RecordResult.Ignored;

        var item = await context.SharedItems.FirstOrDefaultAsync(i =>
            i.UserName == user && i.Kind == SharedItemKind.Recipe && i.RecipeGuid == recipeGuid);
        if (item == null)
        {
            item = new SharedItem { UserName = user, Kind = SharedItemKind.Recipe, RecipeGuid = recipeGuid };
            context.SharedItems.Add(item);
        }
        item.OwnerUserName = owner;
        item.LastOpened = DateTime.UtcNow;
        await context.SaveChangesAsync();
        return RecordResult.Recorded;
    }

    /// <summary>Records that <paramref name="user"/> opened a shared cookbook link with a valid token.</summary>
    public async Task<RecordResult> RecordCookbookAsync(string user, string ownerParam, string shareToken)
    {
        var owner = await db.ResolveUserIdAsync(ownerParam);
        if (owner == user) return RecordResult.Ignored;
        var stored = await db.GetUserPreference("ShareToken", owner);
        if (string.IsNullOrEmpty(stored) || stored != shareToken) return RecordResult.Forbidden;

        await using var context = await db.GetContext();
        var item = await context.SharedItems.FirstOrDefaultAsync(i =>
            i.UserName == user && i.Kind == SharedItemKind.Cookbook && i.OwnerUserName == owner);
        if (item == null)
        {
            item = new SharedItem { UserName = user, Kind = SharedItemKind.Cookbook, OwnerUserName = owner };
            context.SharedItems.Add(item);
        }
        // A regenerated link replaces the old token, so keep the latest one.
        item.ShareToken = shareToken;
        item.LastOpened = DateTime.UtcNow;
        await context.SaveChangesAsync();
        return RecordResult.Recorded;
    }

    /// <summary>All of the user's shared items, pinned first, then most recently opened.</summary>
    public async Task<List<SharedItemDto>> GetAllAsync(string user)
    {
        await using var context = await db.GetContext();
        var items = await context.SharedItems.AsNoTracking().Where(i => i.UserName == user).ToListAsync();
        if (items.Count == 0) return [];

        var recipeGuids = items.Where(i => i.RecipeGuid != null).Select(i => i.RecipeGuid!.Value).ToList();
        var recipeNames = await context.Recipes.AsNoTracking()
            .Where(r => recipeGuids.Contains(r.Guid))
            .ToDictionaryAsync(r => r.Guid, r => r.Name);

        var owners = items.Select(i => i.OwnerUserName).Distinct().ToList();
        var preferences = await context.UserPreferences.AsNoTracking()
            .Where(p => owners.Contains(p.UserName) && (p.Key == "DisplayName" || p.Key == "ShareToken"))
            .ToListAsync();
        string? Preference(string owner, string key) =>
            preferences.FirstOrDefault(p => p.UserName == owner && p.Key == key)?.Value;

        return items
            .Select(i =>
            {
                var recipeName = i.RecipeGuid is Guid g && recipeNames.TryGetValue(g, out var name) ? name : null;
                var currentToken = Preference(i.OwnerUserName, "ShareToken");
                return new SharedItemDto
                {
                    Id = i.Id,
                    Kind = i.Kind,
                    Owner = i.OwnerUserName,
                    OwnerName = Preference(i.OwnerUserName, "DisplayName") ?? i.OwnerUserName,
                    RecipeGuid = i.RecipeGuid,
                    RecipeName = recipeName,
                    ShareToken = i.ShareToken,
                    LastOpened = DateTime.SpecifyKind(i.LastOpened, DateTimeKind.Utc),
                    Pinned = i.Pinned,
                    Available = i.Kind == SharedItemKind.Recipe
                        ? recipeName != null
                        : !string.IsNullOrEmpty(currentToken) && currentToken == i.ShareToken,
                };
            })
            .OrderByDescending(i => i.Pinned)
            .ThenByDescending(i => i.LastOpened)
            .ToList();
    }

    /// <returns>False when the item does not exist or belongs to someone else.</returns>
    public async Task<bool> SetPinnedAsync(string user, int id, bool pinned)
    {
        await using var context = await db.GetContext();
        var item = await context.SharedItems.FirstOrDefaultAsync(i => i.Id == id && i.UserName == user);
        if (item == null) return false;
        item.Pinned = pinned;
        await context.SaveChangesAsync();
        return true;
    }

    /// <returns>False when the item does not exist or belongs to someone else.</returns>
    public async Task<bool> RemoveAsync(string user, int id)
    {
        await using var context = await db.GetContext();
        var item = await context.SharedItems.FirstOrDefaultAsync(i => i.Id == id && i.UserName == user);
        if (item == null) return false;
        context.SharedItems.Remove(item);
        await context.SaveChangesAsync();
        return true;
    }
}
