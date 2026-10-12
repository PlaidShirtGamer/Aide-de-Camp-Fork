using System.Globalization;
using System.IO;
using System.Security.Cryptography;

namespace AideDeCamp.Services;

// A reviewed snapshot, separate from the loaded campaign and its undo history.
public sealed class BaseGameOfficerPlan
{
    private sealed record Change(TextFileBuffer Buffer, byte[] Original, List<string> Lines);
    private readonly List<Change> _changes = new();
    private readonly string _config;
    private readonly string? _portraitPath;
    private readonly byte[]? _portrait;
    private readonly bool _hadAttributes;
    private bool _committed;
    public int OfficerId { get; }
    public string CatalogPath => Path.Combine(_config, "commanders.txt");

    public BaseGameOfficerPlan(string config, OfficerBlueprint blueprint)
    {
        _config = Path.GetFullPath(config);
        if (!Directory.Exists(_config)) throw new InvalidDataException("Select the game folder first.");
        OfficerRecords.Validate(blueprint, new DateTime(1900,1,1));
        var original = File.ReadAllBytes(CatalogPath);
        var buffer = TextFileBuffer.Read(CatalogPath);
        if (!original.SequenceEqual(File.ReadAllBytes(CatalogPath))) throw new IOException("Officer catalog changed while reading.");
        OfficerId = CreationRecords.FixedCount(buffer.Lines,66,"base-game commanders.txt");
        var ids = new HashSet<int>();
        for (int i = 0; i < OfficerId; i++) {
            int p = 1 + i * 66;
            if (!int.TryParse(buffer.Lines[p],out int id) || id < 0 || id >= OfficerId || !ids.Add(id)) throw new InvalidDataException("Base-game officer IDs are invalid.");
            if (buffer.Lines[p+3].Equals(blueprint.FirstName.Trim()+" "+blueprint.LastName.Trim(),StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("An officer with this name already exists in the base game.");
        }
        string portraitName = "";
        if (!string.IsNullOrWhiteSpace(blueprint.PortraitPath)) {
            if (new FileInfo(blueprint.PortraitPath).Length > 10*1024*1024) throw new InvalidDataException("Portrait must be a PNG smaller than 10 MB.");
            _portrait = File.ReadAllBytes(blueprint.PortraitPath);
            if (_portrait.Length < 24 || !_portrait.Take(8).SequenceEqual(new byte[]{137,80,78,71,13,10,26,10}) ||
                System.Buffers.Binary.BinaryPrimitives.ReadInt32BigEndian(_portrait.AsSpan(16,4)) is < 1 or > 4096 ||
                System.Buffers.Binary.BinaryPrimitives.ReadInt32BigEndian(_portrait.AsSpan(20,4)) is < 1 or > 4096)
                throw new InvalidDataException("Choose a PNG portrait up to 4096×4096 pixels.");
            portraitName = "adc-"+Convert.ToHexString(SHA256.HashData(_portrait)).ToLowerInvariant();
            _portraitPath = Path.Combine(Path.GetDirectoryName(_config)!,"Avatar",portraitName+".png");
        }
        var record = OfficerRecords.Commander(OfficerId,blueprint,portraitName);
        record[59] = "-1"; // Base catalogs derive the starting rank from promotion dates.
        var lines = buffer.CloneLines(); lines[0] = (OfficerId+1).ToString(CultureInfo.InvariantCulture); lines.AddRange(record);
        _changes.Add(new(buffer,original,lines));
        string attributes = Path.Combine(_config,"attributes.dat"); _hadAttributes = File.Exists(attributes);
        if (_hadAttributes) {
            var originalAttrs = File.ReadAllBytes(attributes);
            var attrs = TextFileBuffer.Read(attributes);
            if (!originalAttrs.SequenceEqual(File.ReadAllBytes(attributes))) throw new IOException("Officer attributes changed while reading."); OfficerRecords.ValidateAttributes(attrs.Lines,OfficerId);
            var updated = attrs.CloneLines(); updated[0] = (OfficerId+1).ToString(CultureInfo.InvariantCulture); updated.AddRange(OfficerRecords.AttributeRecord(OfficerId));
            _changes.Add(new(attrs,originalAttrs,updated));
        }
    }

    public string Commit()
    {
        if (_committed) throw new InvalidOperationException("This officer has already been added.");
        void Check() {
            if (_hadAttributes != File.Exists(Path.Combine(_config,"attributes.dat")) ||
                _changes.Any(c => !File.ReadAllBytes(c.Buffer.FilePath).SequenceEqual(c.Original)))
                throw new IOException("The base-game catalog changed after review. Reopen Create Officer and review again.");
        }
        Check();
        string backup = Path.Combine(_config,"ADC-Backups",DateTime.UtcNow.ToString("yyyyMMdd-HHmmss")+"-"+Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(backup);
        var replaced = new List<Change>(); var temps = new List<string>(); bool addedPortrait = false;
        try {
            foreach (var change in _changes) {
                File.WriteAllBytes(Path.Combine(backup,Path.GetFileName(change.Buffer.FilePath)),change.Original);
                string temp = change.Buffer.FilePath+".adc-"+Guid.NewGuid().ToString("N"); temps.Add(temp);
                change.Buffer.WriteTo(temp,change.Lines);
            }
            if (_portraitPath is not null) {
                string dir = Path.GetDirectoryName(_portraitPath)!; Directory.CreateDirectory(dir);
                if ((File.GetAttributes(dir)&FileAttributes.ReparsePoint)!=0) throw new IOException("Avatar must not be a directory link.");
                if (File.Exists(_portraitPath)) {
                    if (!File.ReadAllBytes(_portraitPath).SequenceEqual(_portrait!)) throw new IOException("Portrait filename conflict.");
                } else {
                    using var output = new FileStream(_portraitPath,FileMode.CreateNew,FileAccess.Write,FileShare.None);
                    addedPortrait = true; output.Write(_portrait!); output.Flush(true);
                }
            }
            Check();
            for (int i=0; i<_changes.Count; i++) { File.Replace(temps[i],_changes[i].Buffer.FilePath,null); replaced.Add(_changes[i]); }
            _committed = true;
            return backup;
        }
        catch {
            foreach (var change in replaced.AsEnumerable().Reverse()) File.Copy(Path.Combine(backup,Path.GetFileName(change.Buffer.FilePath)),change.Buffer.FilePath,true);
            if (addedPortrait) File.Delete(_portraitPath!);
            throw;
        }
        finally { foreach (string temp in temps) if (File.Exists(temp)) File.Delete(temp); }
    }
}
