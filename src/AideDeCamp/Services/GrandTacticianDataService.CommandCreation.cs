using System.Globalization;
using System.IO;
using AideDeCamp.Models;

namespace AideDeCamp.Services;

public sealed partial class GrandTacticianDataService
{
    public bool SupportsGtipArmies => ConfigDirectory is not null && File.Exists(Path.Combine(
        Path.GetDirectoryName(Path.GetFullPath(ConfigDirectory))!, "The Civil War (1861-1865)_Data", "Managed", "GTIP.Runtime.dll"));

    public HashSet<int> AssignedCommanderIds() => _groups.Values.Select(g => g.CommanderId)
        .Concat(_units.Select(u => u.CommanderId)).Concat(_armyGroups.Select(a => a.Source.CommanderId)).ToHashSet();

    private void RequireCreationSave()
    {
        if (SaveDirectory is null || IsReadOnlySave || GameDate is null ||
            File.ReadAllText(Path.Combine(SaveDirectory, "version.dat")).Trim() != "1.142")
            throw new InvalidDataException("Open a dated, extracted version-1.142 campaign save first.");
        EnsureFilesUnchanged();
    }

    public CreationAssessment AssessArmyGroup(CreationRequest request)
    {
        var errors = new List<string>(); var warnings = new List<string>(); var b = request.Blueprint;
        try
        {
            RequireCreationSave();
            if (!b.Headquarters || !b.ArmyGroup || b.Faction is not (0 or 1) || request.Placement.ParentId is not null || request.Placement.Town is not null)
                errors.Add("Army Groups are independent headquarters placed through their attached commands.");
            if (string.IsNullOrWhiteSpace(b.Name) || b.Name.Length > 160 || b.Name.Any(char.IsControl)) errors.Add("Enter a name of 1–160 characters without control characters.");
            if (_armyGroups.Any(a => a.Source.Name == b.Name) || _groups.Values.Any(g => g.Name == b.Name)) errors.Add("That command name is already in use.");
            var officer = GetCreationOfficers().SingleOrDefault(o => o.Id == b.CommanderId);
            var commanderProblem = GetCreationCommanderRule(b.Faction, 18, true).Problem(officer, b.Faction, AssignedCommanderIds(), GameDate);
            if (commanderProblem is not null) errors.Add(commanderProblem);
            if (b.AttachedCommandIds.Distinct().Count() != b.AttachedCommandIds.Length) errors.Add("Each attached command must be selected once.");
            foreach (int id in b.AttachedCommandIds)
                if (!_groups.TryGetValue(id, out var g) || g.ParentId >= 0 || g.Nation != b.Faction ||
                    !g.IsLandCommand || g.ArmyGroupName is not null || !IsCreationParent(g, b.Faction))
                    errors.Add($"Command #{id} must be an independent land HQ of this faction, outside any Army Group.");
            var refs = ReadCreationCompanion("armygrouprefs.dat");
            ValidateArmyReferences(refs, _groups.Count, _units.Count);
            if (b.AttachedCommandIds.Length == 0) warnings.Add("This Army Group has no attached commands. The game will not draw a campaign-map icon until commands are attached.");
            warnings.Add("Army Group creation uses the native save records. Confirm placement and commander availability after loading the game.");
        }
        catch (Exception e) when (e is IOException or InvalidOperationException or FormatException or OverflowException) { errors.Add(e.Message); }
        return new(errors, warnings);
    }

    private GroupNode CreateArmyGroup(CreationRequest request, bool confirmed, EditSession session)
    {
        var assessment = AssessArmyGroup(request);
        if (!assessment.CanCreate) throw new InvalidDataException(string.Join("\n", assessment.Errors));
        if (!confirmed) throw new InvalidOperationException("Review the Army Group creation first.");
        TrackCreationBuffer("armygroups.dat"); TrackCreationBuffer("armygrouprefs.dat");
        var before = CreationBuffer("armygroups.dat").CloneLines();
        int id = CreationRecords.FixedCount(before, 6, "armygroups.dat");
        var refsBefore = CreationBuffer("armygrouprefs.dat").CloneLines();
        var after = new List<string>(before); var b = request.Blueprint;
        after[0] = (id + 1).ToString(CultureInfo.InvariantCulture);
        after.AddRange([id.ToString(CultureInfo.InvariantCulture), b.Name, b.CommanderId.ToString(CultureInfo.InvariantCulture),
            string.Concat(b.AttachedCommandIds.Select(i => i.ToString("D4", CultureInfo.InvariantCulture) + ";")), "18", "243-176-048"]);
        var refsAfter = new List<string>(refsBefore);
        // The assignment section follows groups.dat record order, not necessarily ID order.
        for (int i = 0; i < _groups.Count; i++)
            if (b.AttachedCommandIds.Contains(int.Parse(_groupBuffer!.Lines[1 + i * 32], CultureInfo.InvariantCulture)))
                refsAfter[1 + i] = b.CommanderId.ToString(CultureInfo.InvariantCulture);
        void Set(List<string> groups, List<string> refs)
        {
            EnsureFilesUnchanged();
            CreationBuffer("armygroups.dat").ReplaceLines(groups);
            CreationBuffer("armygrouprefs.dat").ReplaceLines(refs);
            LoadArmyGroups();
        }
        session.ExecuteCreation("Create Army Group " + b.Name, () => Set(after, refsAfter), () => Set(before, refsBefore));
        return _armyGroups.Single(a => a.Source.GroupId == -2 - id).Source;
    }

    public bool CanAssignArmyGroup(GroupNode command, GroupNode target) => !IsReadOnlySave &&
        !command.IsArmyGroup && command.IsLandCommand && command.ParentId < 0 &&
        _groups.TryGetValue(command.GroupId, out var actual) && ReferenceEquals(command, actual) &&
        target.IsArmyGroup && target.Nation == command.Nation &&
        _armyGroups.Any(a => a.Source.GroupId == target.GroupId && a.Source.CommanderId == target.CommanderId && a.Source.Nation == command.Nation);

    public void AssignArmyGroup(GroupNode command, GroupNode target, int index, EditSession session)
    {
        RequireCreationSave();
        if (!CanAssignArmyGroup(command, target)) throw new InvalidOperationException("Choose an Army Group in the same faction for an independent land command.");
        TrackCreationBuffer("armygroups.dat"); TrackCreationBuffer("armygrouprefs.dat");
        var before = CreationBuffer("armygroups.dat").CloneLines();
        var refsBefore = CreationBuffer("armygrouprefs.dat").CloneLines();
        ValidateArmyReferences(refsBefore, _groups.Count, _units.Count);
        ArmyGroupDisplay.Parse(before, _groups, _commanderNations);
        var after = new List<string>(before); var refsAfter = new List<string>(refsBefore);
        int targetIndex = -2 - target.GroupId;
        for (int i = 0; i < int.Parse(before[0], CultureInfo.InvariantCulture); i++) {
            int line = 4 + i * 6;
            var tokens = before[line].Split(';', StringSplitOptions.RemoveEmptyEntries).ToList();
            bool removed = tokens.RemoveAll(t => int.Parse(t, CultureInfo.InvariantCulture) == command.GroupId) > 0;
            if (i == targetIndex) tokens.Insert(Math.Clamp(index, 0, tokens.Count), command.GroupId.ToString("D4", CultureInfo.InvariantCulture));
            if (removed || i == targetIndex) after[line] = string.Concat(tokens.Select(t => t + ";"));
        }
        for (int i = 0; i < _groups.Count; i++)
            if (int.Parse(_groupBuffer!.Lines[1 + i * 32], CultureInfo.InvariantCulture) == command.GroupId)
                refsAfter[1+i] = target.CommanderId.ToString(CultureInfo.InvariantCulture);
        void Set(List<string> groups, List<string> refs) {
            EnsureFilesUnchanged();
            CreationBuffer("armygroups.dat").ReplaceLines(groups); CreationBuffer("armygrouprefs.dat").ReplaceLines(refs);
            LoadArmyGroups();
        }
        session.ExecuteCreation("Assign " + command.Name + " to " + target.Name, () => Set(after, refsAfter), () => Set(before, refsBefore));
    }

    public void ReparentCommand(GroupNode moving, GroupNode target, EditSession session)
    {
        RequireCreationSave();
        if (!CanMoveGroup(moving,target)) throw new InvalidOperationException("Choose a higher land HQ in the same faction, outside this command's branch.");
        TrackCreationBuffer("armygroups.dat"); TrackCreationBuffer("armygrouprefs.dat");
        var before=CreationBuffer("armygroups.dat").CloneLines();var refsBefore=CreationBuffer("armygrouprefs.dat").CloneLines();
        ValidateArmyReferences(refsBefore,_groups.Count,_units.Count);
        var after=new List<string>(before);var refsAfter=new List<string>(refsBefore);int parent=moving.ParentId;
        for(int i=0;i<int.Parse(after[0]);i++){
            int line=4+i*6;var tokens=after[line].Split(';',StringSplitOptions.RemoveEmptyEntries).ToList();
            if(tokens.RemoveAll(t=>int.Parse(t)==moving.GroupId)>0)after[line]=string.Concat(tokens.Select(t=>t+";"));
        }
        for(int i=0;i<_groups.Count;i++)if(int.Parse(_groupBuffer!.Lines[1+i*32])==moving.GroupId)refsAfter[1+i]="-1";
        void Set(List<string> groups,List<string> refs,int newParent){
            EnsureFilesUnchanged();moving.ParentId=newParent;
            CreationBuffer("armygroups.dat").ReplaceLines(groups);CreationBuffer("armygrouprefs.dat").ReplaceLines(refs);
            LoadArmyGroups();ResolveUnitContext();
        }
        session.ExecuteCreation("Attach "+moving.Name+" to "+target.Name,()=>Set(after,refsAfter,target.GroupId),()=>Set(before,refsBefore,parent));
    }

    private void ValidateAdditionalCreation()
    {
        if (_creationCompanions.TryGetValue("armygroups.dat", out var armyGroups))
            ArmyGroupDisplay.Parse(armyGroups.Lines, _groups, _commanderNations);
        if (_creationCompanions.TryGetValue("armygrouprefs.dat", out var refs))
            ValidateArmyReferences(refs.Lines, _groups.Count, _units.Count);
        ValidateOfficerCreation();
    }
}
