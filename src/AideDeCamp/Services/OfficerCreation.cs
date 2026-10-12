using System.Globalization;
using System.IO;

namespace AideDeCamp.Services;

public sealed record OfficerBlueprint(string FirstName, string LastName, int Faction, int Rank, int Branch,
    DateTime BirthDate, DateTime CommissionDate, int StateId = -1, bool Veteran = false, bool WestPoint = false,
    bool Political = false, double Experience = 0, double Fame = 0, double Leadership = 50,
    double Initiative = 50, double Administration = 50, double Cunning = 50, string? PortraitPath = null);

public static class OfficerRecords
{
    public static readonly string[] Ranks = ["Lieutenant", "Captain", "Major", "Lieutenant Colonel", "Colonel", "Brigadier General", "Major General", "Lieutenant General", "General"];
    public static void Validate(OfficerBlueprint b, DateTime date)
    {
        foreach (var name in new[] { b.FirstName, b.LastName })
            if (string.IsNullOrWhiteSpace(name) || name.Length > 70 || name.Any(char.IsControl))
                throw new InvalidDataException("First and last names must contain 1–70 characters without control characters.");
        if (b.Faction is not (0 or 1) || b.Rank is < 1 or > 9 || b.Branch is < -1 or > 3)
            throw new InvalidDataException("Choose a faction, land branch, and rank.");
        if (b.StateId < -1 || b.StateId > 52) throw new InvalidDataException("Choose a valid home state.");
        if (b.BirthDate.Year < 1700 || b.BirthDate.AddYears(16) > b.CommissionDate || b.CommissionDate > date)
            throw new InvalidDataException("Commission date must be on/before the campaign date and at least 16 years after birth.");
        foreach (double value in new[] { b.Experience, b.Fame, b.Leadership, b.Initiative, b.Administration, b.Cunning })
            if (!double.IsFinite(value) || value is < 0 or > 100) throw new InvalidDataException("Officer attributes must be between 0 and 100.");
    }

    public static List<string> Commander(int id, OfficerBlueprint b, string portrait)
    {
        string N(double n) => n.ToString("0.########", CultureInfo.InvariantCulture);
        var lines = Enumerable.Repeat("-1", 66).ToList();
        lines[0] = N(id); lines[1] = b.LastName.Trim(); lines[2] = b.FirstName.Trim(); lines[3] = b.FirstName.Trim() + " " + b.LastName.Trim();
        lines[4] = N(b.Faction);
        double[] stats = [b.Experience, b.Fame, b.Leadership, b.Initiative, b.Administration, b.Cunning];
        for (int i = 0; i < stats.Length; i++) lines[5 + i] = N(stats[i]);
        lines[11] = b.Veteran.ToString(); lines[12] = b.WestPoint.ToString(); lines[13] = N(b.Branch);
        void Date(int offset, DateTime value) { lines[offset] = N(value.Day); lines[offset+1] = N(value.Month); lines[offset+2] = N(value.Year); }
        Date(14, b.BirthDate);
        // Set attained ranks only; no future auto-promotions are scheduled.
        for (int rank = 1; rank <= b.Rank; rank++) Date(17 + (rank - 1) * 3, b.CommissionDate);
        lines[53] = "1"; lines[54] = N(b.StateId); lines[55] = b.Political.ToString();
        lines[56] = ""; lines[57] = "0"; lines[58] = "0"; lines[59] = N(b.Rank); lines[60] = "0";
        lines[61] = "-1"; lines[62] = "-1"; lines[63] = portrait; lines[64] = "-1"; lines[65] = "True";
        return lines;
    }

    public static readonly int[] AttributeWidths = [8, 7, 7, 6, 6, 6];
    public static void ValidateAttributes(IReadOnlyList<string> lines, int officers)
    {
        int pos = 0;
        int Take() => pos < lines.Count && int.TryParse(lines[pos++], NumberStyles.Integer, CultureInfo.InvariantCulture, out int n) ? n : throw new InvalidDataException("Invalid officer attribute record.");
        if (Take() != officers) throw new InvalidDataException("attributes.dat officer count does not match commanders.txt.");
        var ids = new HashSet<int>();
        for (int i = 0; i < officers; i++)
        {
            int id = Take(); if (id < 0 || id >= officers || !ids.Add(id)) throw new InvalidDataException("Invalid attribute officer ID.");
            foreach (int width in AttributeWidths)
            {
                int count = Take(); if (count < 0 || count > width) throw new InvalidDataException("Unknown officer attribute array width.");
                for (int j = 0; j < count; j++)
                    if (pos >= lines.Count || !double.TryParse(lines[pos++], NumberStyles.Float, CultureInfo.InvariantCulture, out double value) || !double.IsFinite(value))
                        throw new InvalidDataException("Invalid officer attribute history.");
            }
        }
        if (pos != lines.Count) throw new InvalidDataException("Unexpected attribute data tail.");
    }
    public static List<string> AttributeRecord(int id)
    {
        var result = new List<string> { id.ToString(CultureInfo.InvariantCulture) };
        foreach (int width in AttributeWidths) { result.Add(width.ToString(CultureInfo.InvariantCulture)); result.AddRange(Enumerable.Repeat("0", width)); }
        return result;
    }
}

public sealed partial class ManagementDocument
{
    private List<string>? _officerSavedLines;
    private bool OfficersStructureChanged => _officerSavedLines is not null && !_buffers["commanders.txt"].Lines.SequenceEqual(_officerSavedLines);
    public void AppendOfficer(List<string> record)
    {
        var buffer = _buffers.GetValueOrDefault("commanders.txt") ?? throw new InvalidDataException("Officer editing is unavailable in this save.");
        var lines = buffer.CloneLines(); int id = CreationRecords.FixedCount(lines, 66, "commanders.txt");
        if (record.Count != 66 || record[0] != id.ToString(Inv)) throw new InvalidDataException("New officer ID does not match the next unused ID.");
        lines.AddRange(record); lines[0] = (id + 1).ToString(Inv); buffer.ReplaceLines(lines);
        Records.RemoveAll(r => r.Domain == "Officers"); ParseOfficers(lines);
    }
    public List<string> RemoveLastOfficer(int id)
    {
        var buffer = _buffers["commanders.txt"]; var lines = WorkingLines("commanders.txt")!;
        if (CreationRecords.FixedCount(lines, 66, "commanders.txt") != id + 1) throw new InvalidOperationException("Undo later officer creations first.");
        var record = lines.Skip(1 + id * 66).Take(66).ToList();
        var baseline = buffer.CloneLines(); baseline.RemoveRange(1 + id * 66, 66); baseline[0] = id.ToString(Inv); buffer.ReplaceLines(baseline);
        foreach (var key in _changes.Keys.Where(k => k.File == "commanders.txt" && k.Line >= 1 + id * 66).ToArray()) _changes.Remove(key);
        Records.RemoveAll(r => r.Domain == "Officers"); ParseOfficers(baseline);
        return record;
    }
}
