using System.Globalization;
using System.IO;

namespace AideDeCamp.Services;

public sealed record CreationCommanderRule(int MinimumRank, double MinimumExperience = 0, string? Error = null)
{
    public static string RankName(int rank) => rank is >= 1 and <= 9 ? OfficerRecords.Ranks[rank - 1] : "Unknown rank";
    public string Description => Error ?? $"Available land officers: {RankName(MinimumRank)} or higher."
        + (MinimumExperience > 0 ? $" At {RankName(MinimumRank)}, at least {MinimumExperience:0.##}% experience is required." : "");

    public string? Problem(CreationOfficer? officer, int faction, IReadOnlySet<int> assigned, DateTime? date)
    {
        if (Error is not null) return Error;
        if (officer is null || officer.Faction != faction || assigned.Contains(officer.Id)) return "Select an unassigned officer in this faction.";
        if (!officer.Active || officer.Status != 0 || officer.Branch is < -1 or > 3 || officer.Rank is < 1 or > 9 ||
            officer.UnavailableFrom is DateTime unavailable && (date is null || date >= unavailable))
            return "Select an active, available land officer.";
        if (officer.Rank < MinimumRank) return $"This command requires {RankName(MinimumRank)} or higher.";
        // The game's Army Group suitability check uses raw experience, and only
        // applies the threshold at the minimum rank (higher ranks are exempt).
        if (officer.Rank == MinimumRank && (!double.IsFinite(officer.Experience) || officer.Experience < MinimumExperience))
            return $"At {RankName(MinimumRank)}, this command requires at least {MinimumExperience:0.##}% experience.";
        return null;
    }
}

public sealed partial class GrandTacticianDataService
{
    private static DateTime? OfficerUnavailableDate(IReadOnlyList<string> lines, int start)
    {
        int day = RequiredInt(lines[start + 50], "Unavailable day"), month = RequiredInt(lines[start + 51], "Unavailable month"), year = RequiredInt(lines[start + 52], "Unavailable year");
        if (day <= 0 && month <= 0 && year <= 0) return null;
        try { return new DateTime(year, month, day); }
        catch (ArgumentOutOfRangeException) { throw new InvalidDataException("Invalid officer unavailability date."); }
    }

    public CreationCommanderRule GetCreationCommanderRule(int faction, int nativeTier, bool armyGroup)
    {
        try
        {
            if (SaveDirectory is null || faction is < 0 or > 1) throw new InvalidDataException("Choose a save and faction to resolve commander requirements.");
            string? Resolve(string name) => CampaignRules.Resolve(SaveDirectory, ConfigDirectory, name);
            double shiftValue = CampaignRules.Setting(Resolve("campaignprefs.txt"), "The shift in command hierarchy")
                ?? throw new InvalidDataException("Commander requirements unavailable: campaign hierarchy setting is missing.");
            if (shiftValue != Math.Truncate(shiftValue) || shiftValue is < -1 or > 0) throw new InvalidDataException("Unsupported command hierarchy shift.");
            int shift = (int)shiftValue, tier = armyGroup ? 18 : nativeTier;
            if (tier != 17 && tier + shift >= 10) tier += shift;
            if (tier == 17 && (armyGroup || nativeTier == 18)) tier = 16;
            if (tier is < 10 or > 18) throw new InvalidDataException("Unsupported commander tier.");
            // Native loading chooses the faction-specific table in the same
            // directory as ranks.txt, otherwise that directory's common table.
            string ranks = Resolve("ranks.txt") ?? throw new InvalidDataException("Commander requirements unavailable: ranks.txt is missing.");
            string factionRanks = Path.Combine(Path.GetDirectoryName(ranks)!, $"ranks{faction}.txt");
            var lines = File.ReadAllLines(File.Exists(factionRanks) ? factionRanks : ranks);
            if (lines.Length < 156) throw new InvalidDataException("Unsupported rank table layout.");
            int Read(int section) => int.Parse(lines[section * 39 + 2 + 2 * tier], CultureInfo.InvariantCulture);
            int before = Read(0), after = Read(2), minimum = before;
            if (before != after)
            {
                var projects = Management?.Records.SingleOrDefault(r => r.Domain == "Progression" && r.Side == faction)?.Fields.SingleOrDefault(f => f.Key == "Projects");
                if (projects is null) throw new InvalidDataException("Commander requirements unavailable: this faction's organization reform could not be read.");
                if (Management!.ListValue(projects).Contains(shift == -1 ? "89" : "90")) minimum = after;
            }
            if (minimum is < 1 or > 9) throw new InvalidDataException("Invalid minimum commander rank.");
            double experience = 0;
            if (armyGroup)
            {
                experience = 100 * (CampaignRules.Setting(Resolve("dlcwl_config.dat"), "minimum experience for assigning a commander to army group")
                    ?? throw new InvalidDataException("Army Group experience setting is missing."));
                if (experience is < 0 or > 100) throw new InvalidDataException("Invalid Army Group experience setting.");
            }
            return new(minimum, experience);
        }
        catch (Exception e) when (e is IOException or InvalidDataException or FormatException or OverflowException or InvalidOperationException)
        { return new(9, 0, e.Message); }
    }
}
