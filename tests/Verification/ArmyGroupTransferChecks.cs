using AideDeCamp.Services;

internal static class ArmyGroupTransferChecks
{
    public static async Task Run(Action<bool,string> check)
    {
        string root=Path.Combine(Path.GetTempPath(),"adc-transfer-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);
        void Write(string file,IEnumerable<string> lines)=>File.WriteAllLines(Path.Combine(root,file),lines);
        Write("version.dat",["1.142"]);Write("scenario.dat",["0","0","8","2","1863"]);
        var officer=new OfficerBlueprint("Test","Officer",0,7,0,new(1820,1,1),new(1861,1,1));
        Write("commanders.txt",new[]{"4"}.Concat(Enumerable.Range(0,4).SelectMany(i=>OfficerRecords.Commander(i,officer with {FirstName="Officer"+i},""))));
        Write("groups.dat",new[]{"2"}.Concat(CreationRecords.Group(7,new UnitBlueprint {Headquarters=true,Name="Army",CommanderId=0,NativeTier=18,Perks=[new(),new(),new(),new()]},null)).Concat(CreationRecords.Group(8,new UnitBlueprint {Headquarters=true,Name="Corps",CommanderId=3,NativeTier=16,Perks=[new(),new(),new(),new()]},7)));
        Write("regiments.dat",["0"]);Write("paths.dat",["0"]);
        Write("armygroups.dat",["2","0","First","1","0007;","18","001-002-003","1","Second","2","","18","004-005-006"]);
        Write("armygrouprefs.dat",["2","1","-1","2","0","True","1","True","0"]);
        byte[] before=File.ReadAllBytes(Path.Combine(root,"armygroups.dat")), refs=File.ReadAllBytes(Path.Combine(root,"armygrouprefs.dat")), hqs=File.ReadAllBytes(Path.Combine(root,"groups.dat"));
        using var data=new GrandTacticianDataService();await data.LoadAsync(root);var session=new EditSession();
        check(data.CanAssignArmyGroup(data.Groups[7],data.ArmyGroups[1].Source),"Attached Army may transfer to another Army Group");
        check(!data.CanAssignArmyGroup(data.Groups[8],data.ArmyGroups[1].Source),"Nonroot Corps cannot bypass its Army through an Army Group transfer");
        var target=new AideDeCamp.Models.GroupNode {IsArmyGroup=true,GroupId=-3,Nation=1,CommanderId=2};
        check(!data.CanAssignArmyGroup(data.Groups[7],target),"Army Group transfer rejects the other faction");
        data.AssignArmyGroup(data.Groups[7],data.ArmyGroups[1].Source,0,session);
        check(data.ArmyGroups[0].AttachedGroupIds.Count==0 && data.ArmyGroups[1].AttachedGroupIds.SequenceEqual(new[]{7}) && data.Groups[7].ParentId==-1 && data.Groups[8].ParentId==7 && data.Groups[7].CommanderId==0,"Transfer changes membership without changing commanders or native hierarchy");
        await data.SaveAsync();data.MarkSavedStates();
        using(var reload=new GrandTacticianDataService()){await reload.LoadAsync(root);check(reload.ArmyGroups[1].AttachedGroupIds.SequenceEqual(new[]{7}) && File.ReadAllLines(Path.Combine(root,"armygrouprefs.dat"))[1]=="2","Transfer survives save/reload and updates assignment companion");}
        check(hqs.SequenceEqual(File.ReadAllBytes(Path.Combine(root,"groups.dat"))),"Army transfer leaves native HQ records byte-identical");
        session.Undo(out _);await data.SaveAsync();data.MarkSavedStates();
        check(before.SequenceEqual(File.ReadAllBytes(Path.Combine(root,"armygroups.dat"))) && refs.SequenceEqual(File.ReadAllBytes(Path.Combine(root,"armygrouprefs.dat"))),"Undo after Save restores exact Army Group and reference bytes");
        session.Redo(out _);await data.SaveAsync();check(data.ArmyGroups[1].AttachedGroupIds.Contains(7),"Army Group transfer redoes after a Save boundary");
        var generated=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for(int i=0;i<600;i++){var n=OfficerNameGenerator.Generate(generated,new Random(i));generated.Add(n.First+" "+n.Last);}
        check(generated.Count==600,"Officer randomizer avoids existing full names across its entire pool");
        bool blocked=false;try{OfficerNameGenerator.Generate(generated);}catch(InvalidOperationException){blocked=true;}
        check(blocked,"Exhausted officer name pool asks for manual input");
    }
}
