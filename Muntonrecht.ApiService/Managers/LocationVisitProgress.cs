namespace Muntonrecht.ApiService.Managers;

/// <summary>Accusations require a recorded visit to every current map location.</summary>
public static class LocationVisitProgress
{
    public static async Task<(int Visited, int Total)> GetAsync(MuntonrechtContext dbContext, int teamId)
    {
        var total = await dbContext.Locations.CountAsync();

        // The organisation code intentionally unlocks every location without
        // writing one TeamUnlock per NFC tag. Treat that team as having visited
        // the complete set everywhere this shared progress helper is used
        // (status display as well as final-accusation authorization).
        var allLocationsUnlocked = await dbContext.TeamProgresss
            .Where(progress => progress.TeamId == teamId)
            .Select(progress => progress.AllLocationsUnlocked)
            .FirstOrDefaultAsync();
        if (allLocationsUnlocked)
            return (total, total);

        // Count locations, not codes: several NFC tags may point to the same
        // place. A pre-assigned dossier alone is not a recorded visit.
        var visited = await dbContext.Locations.CountAsync(location =>
            dbContext.TeamUnlocks.Any(unlock => unlock.TeamId == teamId
                && unlock.LocationCode.LocationId == location.Id));
        return (visited, total);
    }
}
