using System.IO;
using System.Security.Cryptography;

namespace AideDeCamp.Services;

public sealed partial class GrandTacticianDataService
{
    private readonly Dictionary<string, byte[]> _pendingPortraits = new(StringComparer.OrdinalIgnoreCase);
    public string? PortraitDirectory => ConfigDirectory is null ? null : Path.Combine(Path.GetDirectoryName(Path.GetFullPath(ConfigDirectory))!, "Avatar");

    public int CreateOfficer(OfficerBlueprint blueprint, EditSession session)
    {
        RequireCreationSave(); OfficerRecords.Validate(blueprint, GameDate!.Value);
        var doc = Management ?? throw new InvalidDataException("Officer editing is unavailable.");
        var lines = doc.WorkingLines("commanders.txt") ?? throw new InvalidDataException("The officer records could not be mapped.");
        int id = CreationRecords.FixedCount(lines, 66, "commanders.txt");
        if (doc.Records.Any(r => r.Domain == "Officers" && r.Name.Equals(blueprint.FirstName.Trim() + " " + blueprint.LastName.Trim(), StringComparison.OrdinalIgnoreCase)))
            throw new InvalidDataException("An officer with this name already exists.");
        string portrait = "";
        if (!string.IsNullOrWhiteSpace(blueprint.PortraitPath))
        {
            if (PortraitDirectory is null || !Directory.Exists(ConfigDirectory)) throw new InvalidDataException("Select the game installation before importing a portrait.");
            if (new FileInfo(blueprint.PortraitPath).Length > 10 * 1024 * 1024) throw new InvalidDataException("Choose a PNG portrait smaller than 10 MB.");
            var bytes = File.ReadAllBytes(blueprint.PortraitPath);
            if (bytes.Length < 24 || bytes.Length > 10 * 1024 * 1024 || !bytes.Take(8).SequenceEqual(new byte[] {137,80,78,71,13,10,26,10}))
                throw new InvalidDataException("Choose a PNG portrait smaller than 10 MB.");
            int width = System.Buffers.Binary.BinaryPrimitives.ReadInt32BigEndian(bytes.AsSpan(16,4));
            int height = System.Buffers.Binary.BinaryPrimitives.ReadInt32BigEndian(bytes.AsSpan(20,4));
            if (width is < 1 or > 4096 || height is < 1 or > 4096) throw new InvalidDataException("Portrait dimensions must be between 1 and 4096 pixels.");
            portrait = "adc-" + Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
            _pendingPortraits[portrait] = bytes;
        }
        List<string>? attributesBefore = null, attributesAfter = null;
        if (File.Exists(Path.Combine(SaveDirectory!, "attributes.dat")))
        {
            TrackCreationBuffer("attributes.dat"); attributesBefore = CreationBuffer("attributes.dat").CloneLines();
            OfficerRecords.ValidateAttributes(attributesBefore, id);
            attributesAfter = new(attributesBefore); attributesAfter[0] = (id + 1).ToString(System.Globalization.CultureInfo.InvariantCulture);
            attributesAfter.AddRange(OfficerRecords.AttributeRecord(id));
        }
        var record = OfficerRecords.Commander(id, blueprint, portrait);
        void Refresh() { RefreshOfficerNames(); ResolveDisplayNames(); LoadArmyGroups(); }
        void Apply()
        {
            EnsureFilesUnchanged(); doc.AppendOfficer(record);
            if (attributesAfter is not null) CreationBuffer("attributes.dat").ReplaceLines(attributesAfter);
            Refresh();
        }
        void Undo()
        {
            EnsureFilesUnchanged();
            if (AssignedCommanderIds().Contains(id)) throw new InvalidOperationException("Undo this officer's command assignments first.");
            record = doc.RemoveLastOfficer(id);
            if (attributesBefore is not null) CreationBuffer("attributes.dat").ReplaceLines(attributesBefore);
            Refresh();
        }
        session.ExecuteCreation("Create officer " + record[3], Apply, Undo);
        return id;
    }

    private void RefreshOfficerNames()
    {
        var lines = Management?.WorkingLines("commanders.txt"); if (lines is null) return;
        _commanders.Clear(); _commanderNations.Clear(); _commanderRanks.Clear();
        int count = CreationRecords.FixedCount(lines, 66, "commanders.txt");
        for (int i = 0; i < count; i++)
        {
            int p = 1 + i * 66, id = int.Parse(lines[p], System.Globalization.CultureInfo.InvariantCulture);
            _commanders[id] = lines[p + 3]; _commanderNations[id] = int.Parse(lines[p+4], System.Globalization.CultureInfo.InvariantCulture);
            _commanderRanks[id] = CurrentCommanderRank(lines, p);
        }
    }

    private void ValidateOfficerCreation()
    {
        if (_creationCompanions.TryGetValue("attributes.dat", out var attributes))
            OfficerRecords.ValidateAttributes(attributes.Lines, GetCreationOfficers().Count);
    }

    // Save installs only content-addressed new PNGs; existing assets are never overwritten.
    // Return paths created in this transaction so a later save failure can roll them back.
    private void InstallPendingPortraits(List<string> installed)
    {
        var lines = Management?.WorkingLines("commanders.txt"); if (lines is null || _pendingPortraits.Count == 0) return;
        int count = CreationRecords.FixedCount(lines, 66, "commanders.txt");
        var used = Enumerable.Range(0, count).Select(i => lines[1 + i * 66 + 63]).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var (name, bytes) in _pendingPortraits.Where(p => used.Contains(p.Key)))
        {
            var dir = PortraitDirectory ?? throw new InvalidDataException("Game installation is required for portraits.");
            Directory.CreateDirectory(dir);
            if ((File.GetAttributes(dir) & FileAttributes.ReparsePoint) != 0) throw new IOException("Avatar folder must not be a directory link.");
            string path = Path.Combine(dir, name + ".png");
            if (File.Exists(path))
            {
                if (!File.ReadAllBytes(path).SequenceEqual(bytes)) throw new IOException("A different portrait already uses the reserved filename.");
                continue;
            }
            using (var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                installed.Add(path); stream.Write(bytes); stream.Flush(true);
            }
        }
    }
}
