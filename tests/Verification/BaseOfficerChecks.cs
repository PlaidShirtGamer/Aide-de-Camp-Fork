using AideDeCamp.Services;

internal static class BaseOfficerChecks
{
    public static void Run(Action<bool,string> check)
    {
        string root=Path.Combine(Path.GetTempPath(),"adc-base-officer-"+Guid.NewGuid().ToString("N")), config=Path.Combine(root,"Config");
        Directory.CreateDirectory(config);
        var officer=new OfficerBlueprint("Original","Officer",0,7,0,new(1820,1,1),new(1861,1,1));
        string catalog=Path.Combine(config,"commanders.txt");
        File.WriteAllLines(catalog,new[]{"1"}.Concat(OfficerRecords.Commander(0,officer,"")));
        byte[] original=File.ReadAllBytes(catalog);
        var plan=new BaseGameOfficerPlan(config,officer with {FirstName="New"});
        check(plan.OfficerId==1 && File.ReadAllBytes(catalog).SequenceEqual(original),"Base-game review stages no disk changes");
        string backup=plan.Commit();var lines=File.ReadAllLines(catalog);
        check(lines[0]=="2" && lines.Skip(1).Take(66).SequenceEqual(File.ReadAllLines(Path.Combine(backup,"commanders.txt")).Skip(1)) && lines[1+66+59]=="-1","Base-game append preserves original records and derives rank from dates");
        check(File.ReadAllBytes(Path.Combine(backup,"commanders.txt")).SequenceEqual(original) && !File.Exists(Path.Combine(config,"attributes.dat")),"Exact backup is retained; absent attributes remain absent");
        bool rejected=false;try{plan.Commit();}catch(InvalidOperationException){rejected=true;}check(rejected,"A reviewed base-game plan cannot be committed twice");
        rejected=false;try{new BaseGameOfficerPlan(config,officer);}catch(InvalidDataException){rejected=true;}check(rejected,"Duplicate base-game officer name is rejected");
        var stale=new BaseGameOfficerPlan(config,officer with {FirstName="Stale"});
        new BaseGameOfficerPlan(config,officer with {FirstName="Concurrent"}).Commit();byte[] concurrent=File.ReadAllBytes(catalog);
        rejected=false;try{stale.Commit();}catch(IOException){rejected=true;}check(rejected && concurrent.SequenceEqual(File.ReadAllBytes(catalog)),"External catalog edits invalidate review without overwriting them");
        string attrs=Path.Combine(config,"attributes.dat");File.WriteAllLines(attrs,new[]{"3"}.Concat(Enumerable.Range(0,3).SelectMany(OfficerRecords.AttributeRecord)));
        byte[] originalAttrs=File.ReadAllBytes(attrs);
        string portrait=Path.Combine(root,"test.png");File.WriteAllBytes(portrait,Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+j4WQAAAAASUVORK5CYII="));
        var failure=new BaseGameOfficerPlan(config,officer with {FirstName="Portrait",PortraitPath=portrait});
        using(var locked=new FileStream(attrs,FileMode.Open,FileAccess.Read,FileShare.Read)){
            rejected=false;try{failure.Commit();}catch(IOException){rejected=true;}
            check(rejected && File.ReadAllBytes(catalog).SequenceEqual(concurrent) && File.ReadAllBytes(attrs).SequenceEqual(originalAttrs),"Failure replacing attributes rolls back the commander catalog");
            check(!Directory.EnumerateFiles(Path.Combine(root,"Avatar")).Any(),"Failed base-game commit removes its imported portrait");
        }
        backup=failure.Commit();lines=File.ReadAllLines(catalog);OfficerRecords.ValidateAttributes(File.ReadAllLines(attrs),4);
        check(File.ReadAllBytes(Path.Combine(root,"Avatar",lines[1+3*66+63]+".png")).SequenceEqual(File.ReadAllBytes(portrait)),"Base-game commit writes matching officer attributes and exact PNG portrait");
    }
}
