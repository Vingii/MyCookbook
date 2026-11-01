using Microsoft.EntityFrameworkCore;
using MyCookbook.Api.Dto;
using MyCookbook.Data;
using MyCookbook.Data.CookbookDatabase;

namespace MyCookbook.Services;

/// <summary>
/// Remembers recipes other users shared with the current user (via a recipe's Share link), so they can be
/// found again from the "Shared" page (MC-71).
/// </summary>
public class SharedItemService(CookbookDatabaseService db)
{
    public enum RecordResult { Recorded, Ignored, NotFound }

    /// <summary>Records that <paramref name="user"/> opened a shared recipe link.</summary>
    public async Task<RecordResult> RecordRecipeAsync(string user, Guid recipeGuid)
    {
        await using var context = await db.GetContext();
        var owner = await context.Recipes
            .Where(r => r.Guid == recipeGuid)
            .Select(r => r.UserName)
            .FirstOrDefaultAsync();
        if (owner == null) return RecordResult.NotFound;
        if (owner == user) return RecordResult.Ignored;

        var item = await context.SharedItems.FirstOrDefaultAsync(i => i.UserName == user && i.RecipeGuid == recipeGuid);
        if (item == null)
        {
            item = new SharedItem { UserName = user, RecipeGuid = recipeGuid };
            context.SharedItems.Add(item);
        }
        item.OwnerUserName = owner;
        item.LastOpened = DateTime.UtcNow;
        await context.SaveChangesAsync();
        return RecordResult.Recorded;
    }

    /// <summary>All of the user's shared recipes, pinned first, then most recently opened.</summary>
    public async Task<List<SharedItemDto>> GetAllAsync(string user)
    {
        await using var context = await db.GetContext();
        var items = await context.SharedItems.AsNoTracking().Where(i => i.UserName == user).ToListAsync();
        if (items.Count == 0) return [];

        var recipeGuids = items.Select(i => i.RecipeGuid).ToList();
        var recipeNames = await context.Recipes.AsNoTracking()
            .Where(r => recipeGuids.Contains(r.Guid))
            .ToDictionaryAsync(r => r.Guid, r => r.Name);

        var owners = items.Select(i => i.OwnerUserName).Distinct().ToList();
        var displayNames = await context.UserPreferences.AsNoTracking()
            .Where(p => owners.Contains(p.UserName) && p.Key == "DisplayName")
            .ToDictionaryAsync(p => p.UserName, p => p.Value);

        return items
            .Select(i => new SharedItemDto
            {
                Id = i.Id,
                OwnerName = displayNames.GetValueOrDefault(i.OwnerUserName) ?? i.OwnerUserName,
                RecipeGuid = i.RecipeGuid,
                RecipeName = recipeNames.GetValueOrDefault(i.RecipeGuid),
                LastOpened = DateTime.SpecifyKind(i.LastOpened, DateTimeKind.Utc),
                Pinned = i.Pinned,
                Available = recipeNames.ContainsKey(i.RecipeGuid),
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
