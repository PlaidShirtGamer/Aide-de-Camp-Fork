using AideDeCamp.Models;
using AideDeCamp.Services;
using System.Security.Cryptography;

internal static class ArmyGroupChecks
{
    public static async Task Run(Action<bool, string> check)
    {
        bool oldScale = OobPresentation.RegimentalScale;
        OobPresentation.RegimentalScale = true;
        var army = new GroupNode { GroupId = 7, Name = "Test Army", ParentId = -1, Nation = 0, UnitTier = 18 };
        var corps = new GroupNode { GroupId = 8, Name = "Test Corps", ParentId = 7, Nation = 0, UnitTier = 16 };
        var groups = new[] { army, corps }.ToDictionary(g => g.GroupId);
        string[] records = ["1", "0", "Test Army Group", "2", "0007;", "18", "001-002-003"];
        var parsed = ArmyGroupDisplay.Parse(records, groups, new Dictionary<int,int>());
        check(army.TierName == "Army" && army.PresentationTier == 16 && corps.TierName == "Corps", "GTIP Army and Corps labels and counters");
        check(parsed[0].Source.TierName == "Army Group" && parsed[0].Source.PresentationTier == 18, "Separate Army Group retains its label and counter");
        var model = new OobDisplayModelService().Build(groups, [], 0, [7]);
        ArmyGroupDisplay.Attach(model, parsed, 0);
        check(model.Roots.Count == 1 && model.Roots[0].Subcommands.Single().Source == army && model.CommandsById[8].Depth == 2,
            "Army Group wraps Army and Corps exactly once");
        check(army.ParentId == -1 && groups.Count == 2, "Display attachment does not mutate native parent IDs or writable HQ dictionary");
        bool Reject(string[] input) { try { ArmyGroupDisplay.Parse(input, groups, new Dictionary<int,int>()); return false; } catch (InvalidDataException) { return true; } }
        var bad = records.ToArray(); bad[4] = "9999;";
        check(Reject(bad), "Missing Army Group member rejected");
        bad[4] = "0007;0007;"; check(Reject(bad), "Duplicate Army Group member rejected");
        bad[4] = "0008;"; check(Reject(bad), "Nonroot Army Group member rejected");
        check(Reject(records[..^1]), "Truncated Army Group rejected");
        var empty = records.ToArray(); empty[4] = "";
        check(ArmyGroupDisplay.Parse(empty, groups, new Dictionary<int,int>{{2,1}})[0].Source.Nation == 1,
            "Empty Army Group uses commander faction");
        OobPresentation.RegimentalScale = false;
        check(new GroupNode { UnitTier = 16 }.TierName == "Army" && parsed[0].Source.TierName == "Army Group", "Vanilla brigade-scale names preserved");
        OobPresentation.RegimentalScale = oldScale;

        var dir = Path.Combine(Path.GetTempPath(), "adc-army-groups-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        // Deliberately retained temporary fixture; no user save is ever changed.
        var hq = Enumerable.Repeat("0", 32).ToArray(); hq[0] = "7"; hq[1] = "Test Army"; hq[2] = "-1"; hq[4] = "2"; hq[17] = "18";
        File.WriteAllLines(Path.Combine(dir, "groups.dat"), new[] {"1"}.Concat(hq));
        File.WriteAllText(Path.Combine(dir, "regiments.dat"), "0\n");
        File.WriteAllText(Path.Combine(dir, "commanders.txt"), "0\n");
        File.WriteAllLines(Path.Combine(dir, "armygroups.dat"), records);
        using var data = new GrandTacticianDataService();
        await data.LoadAsync(dir);
        check(data.ArmyGroups.Count == 1 && data.Groups[7].ArmyGroupName == "Test Army Group", "Save loader resolves separate Army Group attachment");
        check(!data.CanMoveGroup(data.Groups[7], data.ArmyGroups[0].Source), "Display-only parent cannot be a writable HQ destination");
        bool blocked = false;
        try { data.MoveUnit(new CombatUnitNode(), data.ArmyGroups[0].Source); } catch (InvalidOperationException) { blocked = true; }
        check(blocked, "Combat units cannot be attached to display-only Army Groups");
        check(!(await data.SaveAsync()).WroteFiles, "No-op load/save writes nothing");
        var before = File.ReadAllBytes(Path.Combine(dir, "armygroups.dat"));
        data.Groups[7].Name = "Renamed Army";
        await data.SaveAsync();
        check(before.SequenceEqual(File.ReadAllBytes(Path.Combine(dir, "armygroups.dat"))), "HQ rename preserves Army Group bytes");
        using var reload = new GrandTacticianDataService(); await reload.LoadAsync(dir);
        check(reload.Groups[7].Name == "Renamed Army" && reload.Groups[7].ParentId == -1 && reload.ArmyGroups.Count == 1,
            "Rename round trip keeps native parent and Army Group attachment");
        File.AppendAllText(Path.Combine(dir, "armygroups.dat"), "changed\n");
        blocked = false;
        try { await reload.SaveAsync(); } catch (IOException) { blocked = true; }
        check(blocked, "External Army Group changes trigger save conflict protection");
    }

    public static async Task AuditSave(string path, Action<bool,string> check)
    {
        var before = Directory.GetFiles(path).ToDictionary(f => f, f => SHA256.HashData(File.ReadAllBytes(f)));
        using var data = new GrandTacticianDataService(); await data.LoadAsync(path);
        foreach (int nation in new[] { 0, 1 })
        {
            var roots = data.BuildRoots(nation).OfType<GroupNode>().Select(g => g.GroupId);
            var model = new OobDisplayModelService().Build(data.Groups, data.Units, nation, roots);
            ArmyGroupDisplay.Attach(model, data.ArmyGroups, nation);
            foreach (var entry in data.ArmyGroups.Where(a => a.Source.Nation == nation))
                check(model.CommandsById[entry.Source.GroupId].Subcommands.Count == entry.AttachedGroupIds.Count,
                    "Real save: all Army Group attachments represented");
        }
        check(before.All(p => p.Value.SequenceEqual(SHA256.HashData(File.ReadAllBytes(p.Key)))), "Real save audit changes no files");
        Console.WriteLine($"Loaded {data.ArmyGroups.Count} Army Groups and {data.Groups.Values.Count(g => g.UnitTier == 18)} tier-18 HQs.");
    }
}
