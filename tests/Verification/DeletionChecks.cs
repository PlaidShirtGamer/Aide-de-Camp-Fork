using AideDeCamp.Services;
using AideDeCamp.Models;

internal static class DeletionChecks
{
    public static async Task Run(Action<bool,string> check)
    {
        string root=Path.Combine(Path.GetTempPath(),"adc-delete-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);
        void Write(string file,IEnumerable<string> lines)=>File.WriteAllLines(Path.Combine(root,file),lines);
        Write("version.dat",["1.142"]);Write("scenario.dat",["0","0","8","2","1863"]);
        var officer=new OfficerBlueprint("Test","Officer",0,7,0,new(1820,1,1),new(1861,1,1));
        Write("commanders.txt",new[]{"8"}.Concat(Enumerable.Range(0,8).SelectMany(i=>OfficerRecords.Commander(i,officer with {FirstName="Officer"+i},""))));
        UnitBlueprint H(string name,int tier,int cmd)=>new(){Headquarters=true,Name=name,CommanderId=cmd,NativeTier=tier,UnitType=14,Perks=[new(),new(),new(),new()]};
        var army=H("Army",18,0);var corps=H("Existing Corps",16,1);var division=H("Division",15,2);
        Write("groups.dat",new[]{"4"}.Concat(CreationRecords.Group(0,army,null)).Concat(CreationRecords.Group(1,corps,null)).Concat(CreationRecords.Group(2,division,1)).Concat(CreationRecords.Group(3,H("Fleet",17,6),null)));
        var a=new UnitBlueprint{Name="First Regiment",CommanderId=3,Strength=1000};var b=a with{Name="Second Regiment",CommanderId=4};
        Write("regiments.dat",new[]{"2"}.Concat(CreationRecords.Regiment(0,a,1,new(1863,2,8),(0,0,0))).Concat(CreationRecords.Regiment(1,b,2,new(1863,2,8),(0,0,0))));
        var paths=new[]{"5"}.Concat(new[]{army,corps,division,a,b}.SelectMany(x=>CreationRecords.Path(x,""))).ToList();
        // A surviving path refers to the regiment being removed.
        int reference=PathRecordParser.Parse(paths,true)[4].ReferenceStarts![0];
        paths[reference]=a.Name;paths[reference+1]=a.Name;paths[reference+2]="0";paths[reference+3]="3";
        Write("paths.dat",paths);
        Write("armygroups.dat",["1","0","Army Group","5","0000;0001;","18","001-002-003"]);
        Write("armygrouprefs.dat",["4","5","5","-1","-1","4","0","True","1","False","2","True","3","True","2","0","True","1","False"]);
        var battle=Enumerable.Repeat("0",43).ToList();battle[22]="0";battle[31]="-1";battle[42]="2";
        foreach(int id in new[]{0,1})battle.AddRange(new[]{id.ToString(),"1","100","200","0","0","0","-1","0","0","0","0","0","True","100"});
        battle.Add("0");Write("battledata.dat",battle);
        Write("ships.dat",["1","0","Test ship","3","-1","0","100","100","100","100","0","0","0","0","0","0","0","0","0","0","0","0","0","False"]);
        var original=Directory.GetFiles(root).ToDictionary(f=>Path.GetFileName(f)!,f=>File.ReadAllBytes(f));
        using var data=new GrandTacticianDataService();await data.LoadAsync(root);var session=new EditSession();
        var c=data.Groups[1];var d=data.Groups[2];var u=data.Units.Single(x=>x.UnitId==0);var v=data.Units.Single(x=>x.UnitId==1);
        check(data.CanMoveGroup(c,data.Groups[0]),"Existing Army Group member Corps accepts Army destination");
        data.ReparentCommand(c,data.Groups[0],session);
        check(c.ParentId==0 && c.ArmyGroupName is null && data.ArmyGroups[0].AttachedGroupIds.SequenceEqual(new[]{0}),"Existing Corps detaches Army Group membership when attaching to Army");
        await data.SaveAsync();data.MarkSavedStates();
        using(var reload=new GrandTacticianDataService()){await reload.LoadAsync(root);check(reload.Groups[1].ParentId==0 && reload.ArmyGroups[0].AttachedGroupIds.SequenceEqual(new[]{0}),"Existing Corps reparent survives save/reload");}
        data.DeleteUnit(c,session);
        check(data.Groups.Count==3 && d.GroupId==1 && d.ParentId==0 && u.ParentId==0 && v.ParentId==1,"HQ deletion promotes immediate children and remaps deeper branch IDs");
        check(!data.AssignedCommanderIds().Contains(1) && data.GetCreationOfficers().Any(o=>o.Id==1),"Deleted HQ commander remains available");
        await data.SaveAsync();data.MarkSavedStates();
        using(var reload=new GrandTacticianDataService()){await reload.LoadAsync(root);check(reload.Groups.Count==3 && reload.Groups[1].Name=="Division" && reload.Units.All(x=>reload.Groups.ContainsKey(x.ParentId)),"Deleted HQ hierarchy survives reload");}
        check(File.ReadAllLines(Path.Combine(root,"ships.dat"))[3]=="2","Land HQ deletion remaps fleet references in ships.dat");
        check(PathRecordParser.Parse(File.ReadAllLines(Path.Combine(root,"paths.dat")),true).Count==4,"Deleted HQ runtime path is removed");
        data.DeleteUnit(u,session);await data.SaveAsync();data.MarkSavedStates();
        check(v.UnitId==0 && data.Units.Count==1,"Combat deletion renumbers surviving combat IDs");
        var savedPaths=File.ReadAllLines(Path.Combine(root,"paths.dat"));var survivorPath=PathRecordParser.Parse(savedPaths,true).Single(x=>x.Name==b.Name);
        check(savedPaths[survivorPath.ReferenceStarts![0]+2]=="-1","Runtime reference to deleted regiment is cleared");
        session.Undo(out _);await data.SaveAsync();data.MarkSavedStates();
        check(data.Units.Count==2 && v.UnitId==1 && u.ParentId==0,"Undo combat deletion restores IDs and parent after save");
        session.Undo(out _);await data.SaveAsync();data.MarkSavedStates();
        check(c.GroupId==1 && d.GroupId==2 && d.ParentId==1 && u.ParentId==1,"Undo HQ deletion restores prior hierarchy after save");
        session.Undo(out _);await data.SaveAsync();data.MarkSavedStates();
        check(c.ParentId==-1 && data.ArmyGroups[0].AttachedGroupIds.SequenceEqual(new[]{0,1}),"Undo reparent restores Army Group and independent Corps");
        check(original.All(p=>p.Value.SequenceEqual(File.ReadAllBytes(Path.Combine(root,p.Key!)))),"Undo full structural sequence restores all original save bytes");
        session.Redo(out _);session.Redo(out _);session.Redo(out _);await data.SaveAsync();data.MarkSavedStates();
        check(data.Groups.Count==3 && data.Units.Count==1 && v.UnitId==0,"Structural sequence redoes across save boundaries");
        session.Undo(out _);session.Undo(out _);session.Undo(out _);
        bool blocked=false;try{data.DeleteUnit(c,session);}catch(InvalidOperationException){blocked=true;}
        check(blocked && data.Groups.Count==4,"Root HQ with direct combat units requires higher HQ before deletion");
        data.DeleteUnit(data.ArmyGroups[0].Source,session);
        check(data.ArmyGroups.Count==0 && c.ParentId==-1 && c.ArmyGroupName is null && data.Units.Count==2,"Deleting Army Group retains all commands independently");
        session.Undo(out _);
        data.ReparentCommand(c,data.Groups[0],session);
        data.DeleteUnit(data.Groups[0],session);await data.SaveAsync();data.MarkSavedStates();
        check(c.ParentId==-1 && data.ArmyGroups[0].AttachedGroupIds.SequenceEqual(new[]{0}) && d.ParentId==0,"Deleting root Army promotes Corps to the same Army Group");
        var promotedBattle=File.ReadAllLines(Path.Combine(root,"battledata.dat"));
        check(promotedBattle[42]=="1" && promotedBattle[43]=="0","Promoted root retains one campaign deployment without duplicates");
        session.Undo(out _);session.Undo(out _);
        // Preserve an unrelated staged combat edit during structural change.
        session.Execute("Edit strength",new[]{v},()=>v.TotalMenRaw=900);
        data.DeleteUnit(u,session);await data.SaveAsync();data.MarkSavedStates();
        using(var reload=new GrandTacticianDataService()){await reload.LoadAsync(root);check(reload.Units.Single().TotalMenRaw==900,"Deletion preserves unrelated staged semantic edits");}
        session.Undo(out _);session.Undo(out _);await data.SaveAsync();data.MarkSavedStates();
        check(v.TotalMenRaw==1000 && data.Units.Count==2,"Undo deletion and earlier semantic edit restores both");
        var doc=data.Management!;var condition=doc.Records.Single(r=>r.Domain=="Navy").Fields.Single(f=>f.Key=="Condition");
        session.ExecuteManagement("Ship condition",doc,()=>doc.Apply(new Dictionary<ManagementDocument.Field,string>{{condition,"75"}}));
        // An empty root Army can be deleted without deleting its Army Group.
        data.DeleteUnit(data.Groups[0],session);await data.SaveAsync();
        check(c.GroupId==0 && d.GroupId==1 && data.ArmyGroups[0].AttachedGroupIds.SequenceEqual(new[]{0}),"Deleting empty root remaps Army Group members");
        check(File.ReadAllLines(Path.Combine(root,"ships.dat"))[6]=="75" && File.ReadAllLines(Path.Combine(root,"ships.dat"))[3]=="2","Ship property edits and remapped fleet reference save together");
        session.Undo(out _);await data.SaveAsync();data.MarkSavedStates();
        check(File.ReadAllLines(Path.Combine(root,"ships.dat"))[6]=="75" && File.ReadAllLines(Path.Combine(root,"ships.dat"))[3]=="3","Undo land deletion preserves ship edits and restores fleet reference");
        session.Undo(out _);await data.SaveAsync();
        check(File.ReadAllLines(Path.Combine(root,"ships.dat"))[6]=="100","Earlier ship edit still undoes after deletion and save");
        string bad=Path.Combine(Path.GetTempPath(),"adc-delete-invalid-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(bad);
        foreach(var f in Directory.GetFiles(root))File.Copy(f,Path.Combine(bad,Path.GetFileName(f)));
        var ambiguous=File.ReadAllLines(Path.Combine(bad,"paths.dat")).ToList();var duplicate=PathRecordParser.Parse(ambiguous,true).Single(r=>r.Name==a.Name);
        ambiguous.AddRange(ambiguous.Skip(duplicate.Start).Take(duplicate.End-duplicate.Start).ToArray());ambiguous[0]="6";File.WriteAllLines(Path.Combine(bad,"paths.dat"),ambiguous);
        using var invalid=new GrandTacticianDataService();await invalid.LoadAsync(bad);var invalidSession=new EditSession();
        bool rejected=false;try{invalid.DeleteUnit(invalid.Units.Single(x=>x.Name==a.Name),invalidSession);}catch(InvalidDataException){rejected=true;}
        check(rejected && invalid.Units.Count==2 && !invalidSession.CanUndo && !invalid.HasCreationChanges,"Ambiguous path rejects deletion without staging partial changes");
    }
}
