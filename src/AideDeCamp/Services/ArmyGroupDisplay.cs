using System.Globalization;
using System.IO;
using AideDeCamp.Models;

namespace AideDeCamp.Services;

/// <summary>Separate six-field armygroups.dat records; IDs are not groups.dat IDs.</summary>
public sealed record ArmyGroupDisplay(GroupNode Source, IReadOnlyList<int> AttachedGroupIds)
{
    public static IReadOnlyList<ArmyGroupDisplay> Parse(IReadOnlyList<string> lines,
        IReadOnlyDictionary<int, GroupNode> groups, IReadOnlyDictionary<int, int> commanderNations)
    {
        int Integer(string value) => int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var n)
            ? n : throw new InvalidDataException("Invalid armygroups.dat integer.");
        var count = lines.Count == 0 ? -1 : Integer(lines[0]);
        if (count < 0 || lines.Count != 1L + count * 6L)
            throw new InvalidDataException("armygroups.dat record count does not match its six-field records.");
        var result = new List<ArmyGroupDisplay>();
        var ids = new HashSet<int>();
        var assigned = new HashSet<int>();
        for (int i = 0; i < count; i++)
        {
            int start = 1 + i * 6, id = Integer(lines[start]), commander = Integer(lines[start + 2]);
            if (id < 0 || !ids.Add(id)) throw new InvalidDataException("Duplicate or negative Army Group ID.");
            var members = lines[start + 3].Split(';', StringSplitOptions.RemoveEmptyEntries).Select(Integer).ToArray();
            foreach (var member in members)
                if (!groups.TryGetValue(member, out var g) || g.ParentId >= 0 || !assigned.Add(member))
                    throw new InvalidDataException("Army Group attachment is missing, duplicated, or not a root HQ.");
            var nation = members.Length > 0 ? groups[members[0]].Nation : commanderNations.GetValueOrDefault(commander, -1);
            if (members.Any(member => groups[member].Nation != nation))
                throw new InvalidDataException("Army Group attachments cross factions.");
            // Negative IDs exist only in the display model, never in native parent fields.
            var source = new GroupNode { GroupId = -2 - i, IsArmyGroup = true, ParentId = -1,
                Name = lines[start + 1], RawName = lines[start + 1], CommanderId = commander,
                Nation = nation, UnitTier = 18, EditorOrder = i, IsExpanded = true,
                SavedCategory = CommandCategory.FieldCommand };
            result.Add(new(source, members));
        }
        return result;
    }

    public static void Attach(DisplayOobModel model, IEnumerable<ArmyGroupDisplay> armyGroups, int nation)
    {
        foreach (var entry in armyGroups.Where(a => a.Source.Nation == nation))
        {
            var source = entry.Source;
            source.Children.Clear();
            var wrapper = new DisplayCommandNode { Source = source, Depth = 0 };
            foreach (var id in entry.AttachedGroupIds)
            {
                var child = model.Roots.FirstOrDefault(r => r.Source.GroupId == id);
                if (child is null) continue;
                model.Roots.Remove(child);
                wrapper.Subcommands.Add(child);
                source.Children.Add(child.Source);
                IncreaseDepth(child);
            }
            source.RefreshAggregates();
            model.CommandsById[source.GroupId] = wrapper;
            model.Roots.Add(wrapper);
        }
    }

    private static void IncreaseDepth(DisplayCommandNode node)
    {
        node.Depth++;
        foreach (var child in node.Subcommands) IncreaseDepth(child);
    }
}
