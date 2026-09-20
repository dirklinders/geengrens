using Microsoft.EntityFrameworkCore;

namespace Muntonrecht.ApiService.Managers;

/// <summary>Result counts of a single logigram sync run.</summary>
public record LogigramSyncResult(int Created, int Updated, int Deleted, int Adopted);

/// <summary>
/// Keeps the logigram grid in sync with the game content: every character
/// (suspect), weapon and location automatically becomes an entry of the
/// matching standard category (keys "suspect" | "weapon" | "location").
/// <para>
/// Entries are linked to their source entity via LogigramEntryModel.EntityId
/// and upserted by (LogigramCategoryId, EntityId), so entry ids — and with
/// them the team marks that reference them — survive repeated syncs. Entries
/// are deterministically ordered by source entity id. Legacy manual entries
/// are adopted when their name exactly matches a source entity (preserving
/// their id) and removed otherwise; their marks cascade away.
/// </para>
/// </summary>
public static class LogigramSyncManager
{
    private static readonly (string Key, string Name, int SortOrder)[] StandardCategories =
    [
        ("suspect", "Verdachten", 0),
        ("weapon", "Wapens", 1),
        ("location", "Locaties", 2),
    ];

    private sealed record SourceEntity(int Id, string Name, string? ImageUrl);

    /// <summary>
    /// Syncs the logigram categories/entries with the current characters,
    /// weapons and locations. Idempotent; safe to call on every request.
    /// </summary>
    public static async Task<LogigramSyncResult> SyncFromGameElementsAsync(MuntonrechtContext db)
    {
        // ── 1. Ensure the three standard categories exist ────────────────────
        // Serialize the full sync across requests and API instances, including entries.
        // The database releases this lock on commit or rollback.
        await using var transaction = await db.Database.BeginTransactionAsync();
        await db.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock(714203, 1)");

        var categories = await db.LogigramCategorys.ToListAsync();
        var categoriesChanged = false;
        foreach (var (key, name, sortOrder) in StandardCategories)
        {
            if (categories.Any(c => c.Key == key)) continue;
            var category = new LogigramCategoryModel { Key = key, Name = name, SortOrder = sortOrder };
            db.LogigramCategorys.Add(category);
            categories.Add(category);
            categoriesChanged = true;
        }
        if (categoriesChanged) await db.SaveChangesAsync();

        // ── 2. Source content, deterministically ordered by id ───────────────
        var characters = await db.Characters.OrderBy(c => c.Id).ToListAsync();
        var weapons = await db.Weapons.OrderBy(w => w.Id).ToListAsync();
        var locations = await db.Locations.OrderBy(l => l.Id).ToListAsync();
        var entries = await db.LogigramEntrys.ToListAsync();

        int created = 0, updated = 0, deleted = 0, adopted = 0;

        void SyncCategory(string key, List<SourceEntity> source)
        {
            // Category keys are unique (manual_020).
            var category = categories
                .Where(c => c.Key == key)
                .OrderBy(c => c.SortOrder).ThenBy(c => c.Id)
                .FirstOrDefault();
            if (category == null) return; // unreachable after step 1; stay safe

            var categoryEntries = entries
                .Where(e => e.LogigramCategoryId == category.Id)
                .ToList();
            var sourceIds = source.Select(s => s.Id).ToHashSet();

            // 2a. Adopt legacy manual entries whose name matches a source
            // entity (preserves their id, and with it the team marks that
            // reference it). The first unclaimed entity wins.
            var taken = categoryEntries
                .Where(e => e.EntityId.HasValue)
                .Select(e => e.EntityId!.Value)
                .ToHashSet();
            foreach (var entry in categoryEntries.Where(e => !e.EntityId.HasValue).ToList())
            {
                var match = source.FirstOrDefault(s => !taken.Contains(s.Id) && s.Name == entry.Name);
                if (match == null) continue;
                entry.EntityId = match.Id;
                taken.Add(match.Id);
                adopted++;
            }

            // 2b. Remove entries no longer backed by content: unadopted manual
            // entries and entries whose source entity was deleted.
            foreach (var entry in categoryEntries.ToList())
            {
                var backed = entry.EntityId.HasValue && sourceIds.Contains(entry.EntityId.Value);
                if (backed) continue;
                db.LogigramEntrys.Remove(entry);
                categoryEntries.Remove(entry);
                deleted++;
            }

            // Defensive: drop duplicate entity links (the partial unique index
            // from manual_016 is the real backstop).
            foreach (var duplicate in categoryEntries
                .GroupBy(e => e.EntityId!.Value)
                .SelectMany(g => g.OrderBy(e => e.Id).Skip(1))
                .ToList())
            {
                db.LogigramEntrys.Remove(duplicate);
                categoryEntries.Remove(duplicate);
                deleted++;
            }

            // 2c. Upsert one entry per source entity, ordered by entity id.
            var byEntity = categoryEntries.ToDictionary(e => e.EntityId!.Value);
            for (var i = 0; i < source.Count; i++)
            {
                var entity = source[i];
                var imageUrl = string.IsNullOrWhiteSpace(entity.ImageUrl) ? null : entity.ImageUrl.Trim();

                if (byEntity.TryGetValue(entity.Id, out var entry))
                {
                    if (entry.Name != entity.Name || entry.ImageUrl != imageUrl || entry.SortOrder != i)
                    {
                        entry.Name = entity.Name;
                        entry.ImageUrl = imageUrl;
                        entry.SortOrder = i;
                        updated++;
                    }
                }
                else
                {
                    db.LogigramEntrys.Add(new LogigramEntryModel
                    {
                        LogigramCategoryId = category.Id,
                        EntityId = entity.Id,
                        Name = entity.Name,
                        ImageUrl = imageUrl,
                        SortOrder = i,
                    });
                    created++;
                }
            }
        }

        SyncCategory("suspect",
            characters.Select(c => new SourceEntity(c.Id, c.Name, c.AvatarUrl)).ToList());
        SyncCategory("weapon",
            weapons.Select(w => new SourceEntity(w.Id, w.Name, null)).ToList());
        SyncCategory("location",
            locations.Select(l => new SourceEntity(l.Id, l.Name, null)).ToList());

        await db.SaveChangesAsync();

        await transaction.CommitAsync();

        return new LogigramSyncResult(created, updated, deleted, adopted);
    }
}
