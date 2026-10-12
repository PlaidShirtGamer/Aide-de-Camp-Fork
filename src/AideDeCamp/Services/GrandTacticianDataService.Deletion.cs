using System.IO;
using AideDeCamp.Models;

namespace AideDeCamp.Services;

public sealed partial class GrandTacticianDataService
{
    public string DeletionSummary(OobNode node)
    {
        if(node is GroupNode {IsArmyGroup:true}) return "Delete this Army Group. Its attached commands become independent; their units and commanders are retained.";
        if(node is GroupNode g) {
            int commands=_groups.Values.Count(c=>c.ParentId==g.GroupId), units=_units.Count(u=>u.ParentId==g.GroupId);
            if(g.ParentId<0 && units>0) throw new InvalidOperationException("This top-level HQ has directly attached combat units and no higher land HQ to receive them. Attach it to a higher HQ first.");
            return $"Delete this HQ only. Move {commands} immediate subordinate HQ(s) and {units} combat unit(s) to {(_groups.TryGetValue(g.ParentId,out var p)?p.Name:g.ArmyGroupName??"independent commands")}. Deeper branches remain intact.";
        }
        return "Delete this combat unit and its runtime path. Its officer is retained and becomes unassigned. Manpower and equipment are not refunded.";
    }

    public void DeleteUnit(OobNode node, EditSession session)
    {
        RequireCreationSave(); _=DeletionSummary(node);
        if (node is not GroupNode and not CombatUnitNode) throw new InvalidOperationException("Select a land formation.");
        var deletedGroup=node as GroupNode; var deletedUnit=node as CombatUnitNode;
        bool armyGroup=deletedGroup?.IsArmyGroup==true;
        if(deletedUnit is not null && (!_units.Contains(deletedUnit) || !new CommandClassificationService().IsLandEditable(deletedUnit,_groups)) ||
            deletedGroup is not null && !armyGroup && (!_groups.Values.Contains(deletedGroup)||!deletedGroup.IsLandCommand) ||
            armyGroup && !_armyGroups.Any(a=>ReferenceEquals(a.Source,node))) throw new InvalidOperationException("Select an existing land formation.");
        if(_pathBuffer is null) throw new InvalidDataException("Deletion requires mapped paths.dat records.");
        var files=new List<string>{"groups.dat","regiments.dat","paths.dat","armygroups.dat","armygrouprefs.dat"};
        foreach(string f in new[]{"battledata.dat","garrisonrefs.dat","ships.dat"}) if(File.Exists(Path.Combine(SaveDirectory!,f))) files.Add(f);
        foreach(string f in files) TrackCreationBuffer(f);
        var before=files.ToDictionary(f=>f,f=>CreationBuffer(f).CloneLines());
        // Include staged semantic edits in the structural snapshots.
        foreach(var g in _groups.Values) PatchGroup(before["groups.dat"],g);
        foreach(var u in _units) { PatchRegiment(before["regiments.dat"],u);PatchPath(before["paths.dat"],u,new()); }
        int gc=CreationRecords.FixedCount(before["groups.dat"],32,"groups.dat"), uc=CreationRecords.FixedCount(before["regiments.dat"],39,"regiments.dat");
        ValidateArmyReferences(before["armygrouprefs.dat"],gc,uc);
        var oldGroups=_groups.Values.OrderBy(g=>g.GroupLineStart).ToArray();var oldUnits=_units.OrderBy(u=>u.RegimentLineStart).ToArray();
        var groupState=oldGroups.ToDictionary(g=>g,g=>(Id:g.GroupId,Parent:g.ParentId));var unitState=oldUnits.ToDictionary(u=>u,u=>(Id:u.UnitId,Parent:u.ParentId));
        var created=_sessionCreated.ToArray();
        int deletedId=deletedGroup?.GroupId??-1, deletedParent=deletedGroup?.ParentId??-1;
        var survivors=oldGroups.Where(g=>g!=deletedGroup).ToArray();var survivingUnits=oldUnits.Where(u=>u!=deletedUnit).ToArray();
        var groupMap=survivors.Select((g,i)=>(Old:g.GroupId,New:i)).ToDictionary(p=>p.Old,p=>p.New);
        var unitMap=survivingUnits.Select((u,i)=>(Old:u.UnitId,New:i)).ToDictionary(p=>p.Old,p=>p.New);
        int Parent(int old) => !armyGroup && deletedGroup is not null && old==deletedId ? deletedParent : old;
        int MapParent(int old) { int p=Parent(old);return p<0?-1:groupMap[p]; }
        var after=before.ToDictionary(p=>p.Key,p=>new List<string>(p.Value));
        after["groups.dat"]=[survivors.Length.ToString()];
        foreach(var g in survivors){var r=before["groups.dat"].Skip(g.GroupLineStart).Take(32).ToArray();r[0]=groupMap[g.GroupId].ToString();r[2]=MapParent(g.ParentId).ToString();after["groups.dat"].AddRange(r);}
        after["regiments.dat"]=[survivingUnits.Length.ToString()];
        foreach(var u in survivingUnits){var r=before["regiments.dat"].Skip(u.RegimentLineStart).Take(39).ToArray();r[0]=unitMap[u.UnitId].ToString();r[3]=MapParent(u.ParentId).ToString();after["regiments.dat"].AddRange(r);}
        var ag=before["armygroups.dat"];after["armygroups.dat"]=["0"];var assignments=new Dictionary<int,string>();int agCount=0;
        for(int i=0;i<int.Parse(ag[0]);i++){
            if(armyGroup && -2-i==deletedGroup!.GroupId)continue;
            var r=ag.Skip(1+i*6).Take(6).ToArray();var members=new List<int>();
            foreach(int id in r[3].Split(';',StringSplitOptions.RemoveEmptyEntries).Select(int.Parse)){
                if(!armyGroup && deletedGroup is not null && id==deletedGroup.GroupId)members.AddRange(survivors.Where(g=>g.ParentId==id).Select(g=>g.GroupId));else members.Add(id);
            }
            foreach(int id in members)assignments[id]=r[2];
            r[0]=(agCount++).ToString();r[3]=string.Concat(members.Select(id=>groupMap[id].ToString("D4")+";"));after["armygroups.dat"].AddRange(r);
        }
        after["armygroups.dat"][0]=agCount.ToString();
        var refs=before["armygrouprefs.dat"];var newRefs=new List<string>{survivors.Length.ToString()};
        foreach(var g in survivors) {
            string assignment=refs[1+Array.IndexOf(oldGroups,g)];
            if(armyGroup && assignment==deletedGroup!.CommanderId.ToString()) assignment="-1";
            newRefs.Add(assignments.GetValueOrDefault(g.GroupId,assignment));
        }
        newRefs.Add(survivors.Length.ToString());
        foreach(var g in survivors){int old=Array.IndexOf(oldGroups,g);newRefs.Add(groupMap[g.GroupId].ToString());newRefs.Add(refs[3+gc+old*2]);}
        newRefs.Add(survivingUnits.Length.ToString());
        foreach(var u in survivingUnits){int old=Array.IndexOf(oldUnits,u);newRefs.Add(unitMap[u.UnitId].ToString());newRefs.Add(refs[4+gc*3+old*2]);}
        after["armygrouprefs.dat"]=newRefs;
        var paths=PathRecordParser.Parse(before["paths.dat"],true);
        var removed=paths.Where(r=>!armyGroup && (deletedUnit is not null ? r.Name==deletedUnit.Name && r.CommanderId==deletedUnit.CommanderId && r.UnitType==deletedUnit.UnitType : r.Name==deletedGroup!.Name && r.CommanderId==deletedGroup.CommanderId && r.UnitType>=14)).ToArray();
        if(removed.Length>1 || deletedUnit is not null && removed.Length!=1)throw new InvalidDataException("The unit's runtime path identity is ambiguous or missing.");
        var pathLines=after["paths.dat"];
        foreach(var r in paths)foreach(int start in r.ReferenceStarts??[])
            if(removed.Any(d=>pathLines[start]==d.Name && pathLines[start+1]==d.Abbreviation && pathLines[start+2]==d.UnitType.ToString() && pathLines[start+3]==d.CommanderId.ToString())){
                pathLines[start]="none";pathLines[start+1]="none";pathLines[start+2]="-1";pathLines[start+3]="-1";
            }
        foreach(var r in removed.OrderByDescending(r=>r.Start))pathLines.RemoveRange(r.Start,r.End-r.Start);
        pathLines[0]=(paths.Count-removed.Length).ToString();
        if(after.TryGetValue("battledata.dat",out var battle)){
            int tail=CreationRecords.DeploymentTail(battle);var deployments=new List<string>();var deployedHqs=new HashSet<int>();List<string>? rootPlacement=null;
            for(int i=0;i<int.Parse(battle[42]);i++){
                var r=battle.Skip(43+i*15).Take(15).ToList();int id=int.Parse(r[0]);bool hq=bool.Parse(r[13]);var map=hq?groupMap:unitMap;
                if(hq && deletedGroup is not null && !armyGroup && id==deletedGroup.GroupId){rootPlacement=r;continue;}
                if(!hq && deletedUnit is not null && id==deletedUnit.UnitId)continue;
                if(hq)deployedHqs.Add(id);
                if(id>=0)r[0]=map[id].ToString();deployments.AddRange(r);
            }
            if(deletedGroup is {ParentId:<0,IsArmyGroup:false})foreach(var child in survivors.Where(g=>g.ParentId==deletedGroup.GroupId)){
                if(deployedHqs.Contains(child.GroupId))continue;
                if(rootPlacement is null)throw new InvalidOperationException("This root HQ has no mapped deployment for its promoted subcommands. Attach it to a higher HQ before deleting it.");
                var r=new List<string>(rootPlacement);r[0]=groupMap[child.GroupId].ToString();deployments.AddRange(r);
            }
            var updated=battle.Take(43).ToList();updated[42]=(deployments.Count/15).ToString();
            foreach(int p in new[]{22,31}){int id=int.Parse(updated[p]);updated[p]=(id<0?-1:groupMap.GetValueOrDefault(Parent(id),-1)).ToString();}
            updated.AddRange(deployments);updated.AddRange(battle.Skip(tail));after["battledata.dat"]=updated;
        }else if(deletedGroup is {ParentId:<0,IsArmyGroup:false} && survivors.Any(g=>g.ParentId==deletedGroup.GroupId))throw new InvalidDataException("Root HQ deletion requires deployment data.");
        if(after.TryGetValue("garrisonrefs.dat",out var forts) && deletedGroup is not null && !armyGroup){
            if(forts.Count!=1+int.Parse(forts[0])*8)throw new InvalidDataException("Unknown fort reference layout.");
            for(int i=0;i<int.Parse(forts[0]);i++){int p=1+i*8;if(forts[p+4]==deletedGroup.RawName && forts[p+7]==deletedGroup.CommanderId.ToString()){forts[p+4]="none";forts[p+5]="none";forts[p+6]="-1";forts[p+7]="-1";}}
        }
        if(after.TryGetValue("ships.dat",out var ships)) {
            int count=CreationRecords.FixedCount(ships,23,"ships.dat");
            for(int i=0;i<count;i++) {int line=3+i*23,id=int.Parse(ships[line]);if(id>=0)ships[line]=groupMap[id].ToString();}
        }
        CreationRecords.ValidateFieldTypes(after["groups.dat"],true);
        CreationRecords.ValidateFieldTypes(after["regiments.dat"],false);
        ValidateArmyReferences(after["armygrouprefs.dat"],survivors.Length,survivingUnits.Length);
        PathRecordParser.Parse(after["paths.dat"],true);
        void Apply(bool deleting){
            EnsureFilesUnchanged();var source=deleting?after:before;
            foreach(var (file,lines) in source)CreationBuffer(file).ReplaceLines(lines);
            if(source.TryGetValue("ships.dat",out var ships)) Management?.UpdateShipFleetReferences(ships);
            _groups.Clear();_units.Clear();
            foreach(var g in deleting?survivors:oldGroups){g.GroupId=deleting?groupMap[groupState[g].Id]:groupState[g].Id;g.ParentId=deleting?MapParent(groupState[g].Parent):groupState[g].Parent;_groups.Add(g.GroupId,g);}
            foreach(var u in deleting?survivingUnits:oldUnits){u.UnitId=deleting?unitMap[unitState[u].Id]:unitState[u].Id;u.ParentId=deleting?MapParent(unitState[u].Parent):unitState[u].Parent;_units.Add(u);}
            _sessionCreated.Clear();_sessionCreated.UnionWith(created.Where(n=>!deleting||n!=node));
            ReindexCreation();LoadArmyGroups();ResolveUnitContext();RefreshGroupAggregates();
        }
        void Set(bool deleting) {
            EnsureFilesUnchanged();
            try { Apply(deleting); } catch { Apply(!deleting); throw; }
        }
        session.ExecuteCreation("Delete "+node.Name,()=>Set(true),()=>Set(false));
    }
}
