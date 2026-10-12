using AideDeCamp.Models;
using AideDeCamp.Services;

internal static class NewCreationChecks
{
    public static async Task RealCopy(string source, string config, Action<bool,string> check)
    {
        var copy=Path.Combine(Path.GetTempPath(),"adc-command-integration-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(copy);
        var hashes=Directory.GetFiles(source).ToDictionary(f=>f,f=>System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(f)));
        foreach(var file in Directory.GetFiles(source)) File.Copy(file,Path.Combine(copy,Path.GetFileName(file)));
        using var data=new GrandTacticianDataService();await data.LoadAsync(copy,config);var session=new EditSession();
        int armyOfficer=data.CreateOfficer(new("ADC","Army Test",0,7,0,new(1820,1,1),data.GameDate!.Value),session);
        int groupOfficer=data.CreateOfficer(new("ADC","Army Group Test",0,7,0,new(1820,1,1),data.GameDate!.Value, Experience:40),session);
        var armyRequest=new CreationRequest(new UnitBlueprint {Headquarters=true,Name="ADC New Independent Army",NativeTier=18,Faction=0,CommanderId=armyOfficer,Perks=[new(),new(),new(),new()]},new(null,data.GetCreationTowns().First(t=>t.Location.Owner==0)));
        var assessment=data.AssessCreation(armyRequest);check(assessment.CanCreate,"Real save copy accepts independent GTIP Army: "+string.Join("; ",assessment.Errors));
        var army=(GroupNode)data.CreateUnit(armyRequest,true,session);
        var groupRequest=new CreationRequest(new UnitBlueprint {Headquarters=true,ArmyGroup=true,Name="ADC New Army Group",Faction=0,CommanderId=groupOfficer,AttachedCommandIds=[army.GroupId]},new(null));
        data.CreateUnit(groupRequest,true,session);await data.SaveAsync();data.MarkSavedStates();
        using var reload=new GrandTacticianDataService();await reload.LoadAsync(copy,config);
        check(reload.Groups[army.GroupId].UnitTier==18 && reload.Groups[army.GroupId].ParentId==-1 && reload.ArmyGroups.Single(a=>a.Source.Name=="ADC New Army Group").AttachedGroupIds.Contains(army.GroupId),"Real save copy reloads newly created Army under new Army Group");
        check(reload.GetCreationOfficers().Any(o=>o.Id==armyOfficer)&&reload.GetCreationOfficers().Any(o=>o.Id==groupOfficer),"Both newly created generals survive combined real-save transaction");
        session.Undo(out _);session.Undo(out _);session.Undo(out _);session.Undo(out _);await data.SaveAsync();
        check(hashes.All(p=>p.Value.SequenceEqual(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(p.Key)))),"Source save remains unchanged during integration checks");
        check(hashes.All(p=>p.Value.SequenceEqual(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(Path.Combine(copy,Path.GetFileName(p.Key)))))),"Combined undo returns copied save to original file bytes");
    }

    public static async Task Run(Action<bool,string> check)
    {
        var root = Path.Combine(Path.GetTempPath(), "adc-officer-create-" + Guid.NewGuid().ToString("N"));
        var save = Path.Combine(root,"save"); var config = Path.Combine(root,"Config"); Directory.CreateDirectory(save); Directory.CreateDirectory(config);
        File.WriteAllLines(Path.Combine(config,"campaignprefs.txt"),["The shift in command hierarchy","-1"]);
        File.WriteAllLines(Path.Combine(config,"dlcwl_config.dat"),["minimum experience for assigning a commander to army group","0.4"]);
        File.WriteAllLines(Path.Combine(config,"ranks.txt"),Enumerable.Range(0,4).SelectMany(section => new[]{"Synthetic ranks"}.Concat(Enumerable.Range(0,19).SelectMany(t => new[]{"#Unit type "+t, (t >= 16 ? 7 : t >= 14 ? 6 : 2).ToString()}))));
        void Write(string name, IEnumerable<string> lines) => File.WriteAllLines(Path.Combine(save,name),lines);
        Write("version.dat",["1.142"]); Write("scenario.dat",["0","0","8","2","1863"]);
        var initial = new OfficerBlueprint("Existing","General",0,7,0,new(1820,1,1),new(1861,1,1), Experience:40);
        Write("commanders.txt",new[]{"1"}.Concat(OfficerRecords.Commander(0,initial,"")));
        Write("attributes.dat",new[]{"1"}.Concat(OfficerRecords.AttributeRecord(0)));
        Write("groups.dat",new[]{"1"}.Concat(CreationRecords.Group(0,new UnitBlueprint { Headquarters=true,Name="Independent Corps",CommanderId=0,NativeTier=16,Perks=[new(),new(),new(),new()] },null)));
        Write("regiments.dat",["0"]); Write("paths.dat",["0"]); Write("armygrouprefs.dat",["1","-1","1","0","True","0"]);
        var original = Directory.GetFiles(save).ToDictionary(f=>Path.GetFileName(f)!,File.ReadAllBytes);
        using var data = new GrandTacticianDataService(); await data.LoadAsync(save,config);
        var session = new EditSession();
        var rule=data.GetCreationCommanderRule(0,18,true);
        var candidate=new CreationOfficer(10,"Eligible",0,0,7,0,true,40);
        var none=new HashSet<int>();
        check(rule.MinimumRank==7 && rule.MinimumExperience==40 && rule.Problem(candidate,0,none,data.GameDate) is null,"Configured Army Group rank and experience boundary are honored");
        check(rule.Problem(candidate with {Experience=39.99},0,none,data.GameDate) is not null && rule.Problem(candidate with {Rank=8,Experience=0},0,none,data.GameDate) is null,"Minimum-rank experience check excludes just-below threshold, exempts higher ranks");
        check(new[]{candidate with {Rank=6},candidate with {Rank=10},candidate with {Branch=4},candidate with {Active=false},candidate with {Status=1},candidate with {Faction=1},candidate with {UnavailableFrom=data.GameDate}}.All(o=>rule.Problem(o,0,none,data.GameDate) is not null),"Unavailable, wrong-faction, naval, under-ranked and dummy officers are excluded");
        check(rule.Problem(candidate,0,new HashSet<int>{10},data.GameDate) is not null && rule.Problem(candidate with {UnavailableFrom=data.GameDate!.Value.AddDays(1)},0,none,data.GameDate) is null,"Assignments and effective unavailability dates are enforced");
        check(data.GetCreationCommanderRule(0,18,false).Problem(candidate with {Experience=0},0,none,data.GameDate) is null,"GTIP Army does not inherit Army Group experience requirement");
        check(data.GetCreationCommanderRule(0,16,false).MinimumRank==6 && data.GetCreationCommanderRule(0,14,false).MinimumRank==2,"Configured hierarchy shift maps Corps and Brigade rank requirements");
        check(CreationCommanderRule.RankName(6)=="Brigadier General" && CreationCommanderRule.RankName(7)=="Major General","Rank numbers use named army ranks");

        var portrait = Path.Combine(root,"portrait.png");
        File.WriteAllBytes(portrait,Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+j4WQAAAAASUVORK5CYII="));
        var blueprint = initial with { FirstName="New",LastName="Officer",PortraitPath=portrait };
        int officer = data.CreateOfficer(blueprint,session);
        check(officer==1 && data.GetCreationOfficers().Single(o=>o.Id==1).Rank==7 && data.HasUnsavedChanges,"New officer gets a unique ID and appears in assignment choices");
        check(original["commanders.txt"].SequenceEqual(File.ReadAllBytes(Path.Combine(save,"commanders.txt"))) && !Directory.Exists(Path.Combine(root,"Avatar")),"Officer and portrait remain staged until Save");
        var field = data.Management!.Records.Single(r=>r.Domain=="Officers"&&r.Id==1).Fields.Single(f=>f.Key=="Leadership");
        session.ExecuteManagement("Edit new officer",data.Management,()=>data.Management.Apply(new Dictionary<ManagementDocument.Field,string>{{field,"85"}}));
        using (var locked = new FileStream(Path.Combine(save,"commanders.txt"),FileMode.Open,FileAccess.Read,FileShare.Read))
        {
            bool failed = false; try { await data.SaveAsync(); } catch(Exception e) when(e is IOException or UnauthorizedAccessException) { failed=true; }
            check(failed && Directory.GetFiles(Path.Combine(root,"Avatar")).Length==0 && original["commanders.txt"].SequenceEqual(File.ReadAllBytes(Path.Combine(save,"commanders.txt"))),
                "Failed save removes newly imported portrait and preserves original officer file");
        }
        await data.SaveAsync(); data.MarkSavedStates();
        var portraitFile = Directory.GetFiles(Path.Combine(root,"Avatar")).Single();
        check(File.ReadAllBytes(portraitFile).SequenceEqual(File.ReadAllBytes(portrait)),"Save installs exact PNG under a unique content-addressed Avatar filename");
        using(var reload = new GrandTacticianDataService()) {
            await reload.LoadAsync(save,config);
            check(reload.GetCreationOfficers().Count==2 && reload.Management!.Records.Count(r=>r.Domain=="Officers")==2,"New officer survives save/reload and is editable");
            OfficerRecords.ValidateAttributes(File.ReadAllLines(Path.Combine(save,"attributes.dat")),2);
            check(reload.Management!.Numeric("commanders.txt",1+66+7)==85,"Edits to newly created officer survive the same save transaction");
        }
        session.Undo(out _); session.Undo(out _); await data.SaveAsync(); data.MarkSavedStates();
        check(original["commanders.txt"].SequenceEqual(File.ReadAllBytes(Path.Combine(save,"commanders.txt"))) && original["attributes.dat"].SequenceEqual(File.ReadAllBytes(Path.Combine(save,"attributes.dat"))),"Undo after Save restores original officer and attribute files byte-for-byte");
        session.Redo(out _); session.Redo(out _); await data.SaveAsync(); data.MarkSavedStates();
        check(data.Management.Numeric("commanders.txt",1+66+7)==85,"Officer creation and edited attributes redo after Save");
        var request = new CreationRequest(new UnitBlueprint { Headquarters=true,ArmyGroup=true,Name="New Army Group",Faction=0,CommanderId=1,AttachedCommandIds=[0] },new(null));
        check(data.AssessArmyGroup(request).CanCreate,"Army Group accepts independent command and unused created general");
        var group = (GroupNode)data.CreateUnit(request,true,session);
        check(group.IsArmyGroup && data.Groups[0].ParentId==-1 && data.Groups[0].ArmyGroupName=="New Army Group","Army Group creation preserves native HQ parent and adds explicit membership");
        check(!data.AssessArmyGroup(request with { Blueprint=request.Blueprint with { Name="Duplicate Assignment" } }).CanCreate,"Assigned Army Group commander and member cannot be reused");
        using (var locked = new FileStream(Path.Combine(save,"armygrouprefs.dat"),FileMode.Open,FileAccess.Read,FileShare.Read))
        {
            bool failed=false; try { await data.SaveAsync(); } catch(Exception e) when(e is IOException or UnauthorizedAccessException) {failed=true;}
            check(failed && !File.Exists(Path.Combine(save,"armygroups.dat")),"Failed first Army Group save rolls back newly created file");
        }
        await data.SaveAsync(); data.MarkSavedStates();
        using(var reload = new GrandTacticianDataService()) { await reload.LoadAsync(save,config); check(reload.ArmyGroups.Single().AttachedGroupIds.SequenceEqual(new[]{0}) && reload.Groups.Count==1,"Army Group round-trip uses separate records, not a counterfeit HQ"); }
        check(File.ReadAllLines(Path.Combine(save,"armygrouprefs.dat"))[1]=="1","Army Group commander assignment companion is updated");
        session.Undo(out _); await data.SaveAsync(); data.MarkSavedStates();
        check(data.ArmyGroups.Count==0 && original["armygrouprefs.dat"].SequenceEqual(File.ReadAllBytes(Path.Combine(save,"armygrouprefs.dat"))),"Undo saved Army Group restores attachment references");
        session.Redo(out _); await data.SaveAsync(); data.MarkSavedStates();
        check(data.ArmyGroups.Count==1,"Army Group redo survives a save boundary");
        bool rejected=false; try { data.CreateOfficer(blueprint with {FirstName="Invalid", Rank=10},session); } catch(InvalidDataException) {rejected=true;}
        check(rejected,"Dummy/internal officer rank is rejected");
        rejected=false; try { data.CreateOfficer(blueprint with {FirstName="Future", CommissionDate=new(1900,1,1)},session); } catch(InvalidDataException) {rejected=true;}
        check(rejected,"Future commission date is rejected");
        var rankLines=File.ReadAllLines(Path.Combine(config,"ranks.txt"));
        var factionLines=(string[])rankLines.Clone();factionLines[34]="8";factionLines[112]="8";
        File.WriteAllLines(Path.Combine(config,"ranks1.txt"),factionLines);
        check(data.GetCreationCommanderRule(1,18,true).MinimumRank==8 && data.GetCreationCommanderRule(0,18,true).MinimumRank==7,"Faction-specific rank table overrides only its own faction");
        factionLines[112]="9";File.WriteAllLines(Path.Combine(config,"ranks1.txt"),factionLines);
        check(data.GetCreationCommanderRule(1,18,true).Error is not null,"Unknown organization reform blocks ambiguous rank requirements");
        File.WriteAllLines(Path.Combine(config,"ranks1.txt"),["Malformed"]);
        check(data.GetCreationCommanderRule(1,18,true).Error is not null,"Malformed configuration fails closed instead of listing every officer");
        check(!data.SupportsGtipArmies && !data.IsCreationParent(new GroupNode {UnitTier=18,ParentId=-1,Nation=0},0),"Extra Army tier is unavailable without GTIP");
        string managed=Path.Combine(root,"The Civil War (1861-1865)_Data","Managed");Directory.CreateDirectory(managed);File.WriteAllText(Path.Combine(managed,"GTIP.Runtime.dll"),"synthetic marker");
        check(data.SupportsGtipArmies && data.IsCreationParent(new GroupNode {UnitTier=18,ParentId=-1,Nation=0},0),"GTIP Army is an eligible land parent; Fleet remains excluded");
        check(!data.IsCreationParent(new GroupNode {UnitTier=17,ParentId=-1,Nation=0},0),"Fleet cannot become a land creation parent");
    }
}
