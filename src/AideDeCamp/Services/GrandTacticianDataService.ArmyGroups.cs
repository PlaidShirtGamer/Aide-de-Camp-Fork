using System.IO;
using AideDeCamp.Models;

namespace AideDeCamp.Services;

public sealed partial class GrandTacticianDataService
{
    private readonly List<ArmyGroupDisplay> _armyGroups = new();
    private readonly Dictionary<int, int> _commanderNations = new();
    public IReadOnlyList<ArmyGroupDisplay> ArmyGroups => _armyGroups;

    private void LoadArmyGroups()
    {
        var path = Path.Combine(SaveDirectory!, "armygroups.dat");
        _armyGroups.Clear();
        foreach (var g in _groups.Values) g.ArmyGroupName = null;
        if (!File.Exists(path) && !_creationCompanions.ContainsKey("armygroups.dat")) return;
        var lines = _creationCompanions.TryGetValue("armygroups.dat", out var staged) ? staged.Lines : TextFileBuffer.Read(path).Lines;
        _armyGroups.AddRange(ArmyGroupDisplay.Parse(lines, _groups, _commanderNations));
        foreach (var entry in _armyGroups)
        {
            var group = entry.Source;
            group.CommanderName = _commanders.GetValueOrDefault(group.CommanderId, $"Commander #{group.CommanderId}");
            group.CommanderDisplayName = CommanderDisplay(group.CommanderId, group.CommanderName);
            foreach (var id in entry.AttachedGroupIds) _groups[id].ArmyGroupName = group.Name;
        }
    }
}
