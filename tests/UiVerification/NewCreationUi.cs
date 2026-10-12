using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using AideDeCamp;
using AideDeCamp.Models;
using AideDeCamp.Services;

internal static partial class Program
{
    private static void NewCreationUi(string output)
    {
        Directory.CreateDirectory(output);
        string root=Path.Combine(Path.GetTempPath(),"adc-create-ui-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);
        string config=Path.Combine(root,"Config");Directory.CreateDirectory(config);
        string managed=Path.Combine(root,"The Civil War (1861-1865)_Data","Managed");Directory.CreateDirectory(managed);File.WriteAllText(Path.Combine(managed,"GTIP.Runtime.dll"),"synthetic marker");
        File.WriteAllLines(Path.Combine(config,"campaignprefs.txt"),["The shift in command hierarchy","-1"]);
        File.WriteAllLines(Path.Combine(config,"dlcwl_config.dat"),["minimum experience for assigning a commander to army group","0.4"]);
        File.WriteAllLines(Path.Combine(config,"ranks.txt"),Enumerable.Range(0,4).SelectMany(section => new[]{"Synthetic ranks"}.Concat(Enumerable.Range(0,19).SelectMany(t => new[]{"#Unit type "+t, (t >= 16 ? 7 : t >= 14 ? 6 : 2).ToString()}))));
        void Write(string file,IEnumerable<string> lines)=>File.WriteAllLines(Path.Combine(root,file),lines);
        Write("version.dat",["1.142"]);Write("scenario.dat",["0","0","8","2","1863"]);
        var officer=new OfficerBlueprint("Test","General",0,7,0,new(1820,1,1),new(1861,1,1), Experience:40);
        var officers=new[]{officer,officer with {FirstName="Unused"},officer with {FirstName="Junior",Rank=6},officer with {FirstName="Inexperienced",Experience=39},officer with {FirstName="Other",Faction=1},officer with {FirstName="Inactive"},officer with {FirstName="Unavailable"},officer with {FirstName="Naval"}};
        var records=officers.Select((o,id)=>OfficerRecords.Commander(id,o,"").ToArray()).ToArray();
        records[5][65]="False";records[6][60]="1";records[7][13]="4";
        Write("commanders.txt",new[]{records.Length.ToString()}.Concat(records.SelectMany(r=>r)));
        Write("groups.dat",new[]{"1"}.Concat(CreationRecords.Group(0,new UnitBlueprint {Headquarters=true,Name="Test Corps",CommanderId=0,NativeTier=16,Perks=[new(),new(),new(),new()]},null)));
        Write("regiments.dat",["0"]);Write("armygrouprefs.dat",["1","-1","1","0","True","0"]);
        using var data=new GrandTacticianDataService();data.LoadAsync(root,config).GetAwaiter().GetResult();
        OobPresentation.RegimentalScale=true;
        var catalog=new CreateUnitWindow.Catalog(data.GetCreationOfficers(),[],data.StateOptions,new Dictionary<int,int>{{0,1000},{1,600},{2,90}},[],"",[],[],data.Groups.Values.ToArray(),data.AssignedCommanderIds(),[new(0,"000-049-083","135-206-235","000-000-000")],[]);
        var w=new CreateUnitWindow(data,new NamingSchemeService(),catalog,0,null);
        T Field<T>(object obj,string name)=>(T)obj.GetType().GetField(name,BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(obj)!;
        void Select(ComboBox c,int id)=>c.SelectedItem=c.Items.Cast<object>().Single(o=>(int)o.GetType().GetProperty("Id")!.GetValue(o)! == id);
        w.Show();Flush(w);
        var tier=Field<ComboBox>(w,"_tier");
        Check(tier.Items.Cast<object>().Any(o=>o.ToString()!.Contains("Army (GTIP)"))&&tier.Items.Cast<object>().Any(o=>o.ToString()=="Army Group"),"Creation tier picker offers both Army and Army Group");
        Select(tier,18);Flush(w);Check(Field<StackPanel>(w,"_location").IsVisible && Field<ComboBox>(w,"_parent").IsEnabled,"Independent Army retains town placement");
        Select(tier,19);Flush(w);Check(!Field<StackPanel>(w,"_location").IsVisible&&!Field<ComboBox>(w,"_parent").IsEnabled&&Field<ListBox>(w,"_armyMembers").Items.Count==1,"Army Group replaces town placement with eligible command attachments");
        var commander=Field<ComboBox>(w,"_commander");
        Check(commander.Items.Count==2 && commander.Items[1].ToString()!.Contains("Major General Unused") && !commander.Items[1].ToString()!.Contains("rank 7"),"Army Group dropdown shows only eligible officers with named ranks");
        Select(commander,1);Select(tier,18);Flush(w);
        Check(commander.Items.Count==3 && commander.SelectedItem!.ToString()!.Contains("Unused"),"Army accepts inexperienced MG and preserves eligible selection");
        Select(commander,3);Select(tier,19);Flush(w);
        Check(commander.SelectedIndex==0 && commander.Items.Count==2,"Switching to Army Group removes and clears inexperienced commander");
        Select(Field<ComboBox>(w,"_faction"),1);Flush(w);
        Check(commander.Items.Count==2 && commander.Items[1].ToString()!.Contains("Other"),"Faction changes immediately rebuild eligible commanders");
        Select(Field<ComboBox>(w,"_faction"),0);Flush(w);
        Capture(w,"army-group-creation.png");w.Close();
        w=new CreateUnitWindow(data,new NamingSchemeService(),catalog,0,null);
        Accept(w,()=>{Select(Field<ComboBox>(w,"_tier"),19);Field<TextBox>(w,"_name").Text="Created Army Group";Select(Field<ComboBox>(w,"_commander"),1);Field<ListBox>(w,"_armyMembers").SelectedIndex=0;},()=>typeof(CreateUnitWindow).GetMethod("ReviewCreate",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(w,[w,new RoutedEventArgs()]));
        Check(w.Request is { Blueprint.ArmyGroup:true }&&w.Request.Blueprint.AttachedCommandIds.SequenceEqual(new[]{0}),"Army Group modal review returns distinct native-record request");
        var ow=new CreateOfficerWindow(data,0);ow.Show();Flush(ow);
        Check(Field<ComboBox>(ow,"_rank").Items.Count==9&&Field<DatePicker>(ow,"_commission").SelectedDate==data.GameDate,"Officer dialog offers real ranks and defaults to campaign date");
        Check(Descendants(ow).OfType<Button>().Any(b=>b.Content?.ToString()=="Choose PNG portrait…"),"Officer dialog exposes PNG portrait selection");
        var randomButton=Descendants(ow).OfType<Button>().Single(b=>b.Content?.ToString()=="Randomize name");
        randomButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        string randomFirst=Field<TextBox>(ow,"_first").Text,randomLast=Field<TextBox>(ow,"_last").Text;
        randomButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Check(randomFirst.Length>0 && randomLast.Length>0 && randomFirst+" "+randomLast!=Field<TextBox>(ow,"_first").Text+" "+Field<TextBox>(ow,"_last").Text,"Randomize name fills editable names and avoids repeating the current name");
        foreach(string pickerName in new[]{"_birth","_commission"}) {
            var picker=Field<DatePicker>(ow,pickerName);picker.IsDropDownOpen=true;Flush(ow);
            var popup=(System.Windows.Controls.Primitives.Popup)picker.Template.FindName("PART_Popup",picker);
            var calendar=Descendants(popup.Child).OfType<Calendar>().Single();
            Check(calendar.CalendarItemStyle is not null && calendar.CalendarDayButtonStyle is not null,"Officer date popup applies dark calendar and day styles");
            var item=Descendants(calendar).OfType<System.Windows.Controls.Primitives.CalendarItem>().Single();
            var day=Descendants(item).OfType<System.Windows.Controls.Primitives.CalendarDayButton>().First(d=>!d.IsInactive && d.Content?.ToString()=="15");
            Check(((SolidColorBrush)day.Foreground).Color==Color.FromRgb(232,237,242),"Calendar day text has explicit high-contrast foreground");
            var popupImage=new RenderTargetBitmap((int)calendar.ActualWidth,(int)calendar.ActualHeight,96,96,PixelFormats.Pbgra32);popupImage.Render(calendar);
            var popupEncoder=new PngBitmapEncoder();popupEncoder.Frames.Add(BitmapFrame.Create(popupImage));using(var file=File.Create(Path.Combine(output,pickerName+"-calendar.png")))popupEncoder.Save(file);
            day.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));Flush(ow);
            Check(picker.SelectedDate?.Day==15,"Date popup day selection still updates its DatePicker");
            picker.IsDropDownOpen=true;Flush(ow);
            var header=(Button)item.Template.FindName("PART_HeaderButton",item);header.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));Flush(ow);
            Check(calendar.DisplayMode==CalendarMode.Year,"Dark calendar header switches to month/year selection");
            calendar.DisplayMode=CalendarMode.Month;
            picker.IsDropDownOpen=false;
        }
        Capture(ow,"officer-creation.png");ow.Close();
        ow=new CreateOfficerWindow(data,1);
        Accept(ow,()=>{Field<TextBox>(ow,"_first").Text="New";Field<TextBox>(ow,"_last").Text="Officer";Field<ComboBox>(ow,"_rank").SelectedIndex=6;},()=>typeof(CreateOfficerWindow).GetMethod("Create",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(ow,[ow,new RoutedEventArgs()]));
        Check(ow.Blueprint is {Faction:1,Rank:7,FirstName:"New"},"Officer modal review returns selected faction and MG rank");

        using(var empty=new GrandTacticianDataService()) {
            File.WriteAllLines(Path.Combine(config,"commanders.txt"),new[]{"1"}.Concat(OfficerRecords.Commander(0,officer,"")));
            var baseWindow=new CreateOfficerWindow(empty,0,config);
            Accept(baseWindow,()=>{Field<TextBox>(baseWindow,"_first").Text="Base";Field<TextBox>(baseWindow,"_last").Text="Officer";},()=>typeof(CreateOfficerWindow).GetMethod("Create",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(baseWindow,[baseWindow,new RoutedEventArgs()]));
            Check(baseWindow.BaseGamePlan is {OfficerId:1} && File.ReadAllLines(Path.Combine(config,"commanders.txt"))[0]=="1","Base-game officer creation works without a loaded save and waits for reviewed commit");
        }
        var main=new MainWindow(true);main.Show();
        var mainData=Field<GrandTacticianDataService>(main,"_data");
        var hq=new GroupNode {GroupId=380,Name="",Nation=1,ParentId=-1,UnitTier=16,IsExpanded=false};
        var unnamed=new CombatUnitNode {UnitId=380,Name="",Nation=1,ParentId=380,UnitTier=13,UnitType=0,TotalMenRaw=1000};
        ((Dictionary<int,GroupNode>)mainData.Groups).Add(hq.GroupId,hq);((List<CombatUnitNode>)mainData.Units).Add(unnamed);
        typeof(MainWindow).GetMethod("RebuildSide",BindingFlags.NonPublic|BindingFlags.Instance)!.Invoke(main,[true]);
        ((TextBox)main.FindName("SearchBox")).Text="does not match";
        var navigationTimer=new DispatcherTimer {Interval=TimeSpan.FromMilliseconds(50)};
        navigationTimer.Tick+=(_,_)=>{
            var review=Application.Current.Windows.OfType<ValidationReviewWindow>().FirstOrDefault();if(review is null)return;
            navigationTimer.Stop();review.Width=550;review.Height=300;Flush(review);Capture(review,"warning-navigation.png");
            Descendants(review).OfType<Button>().Single(b=>b.Content?.ToString()=="Go to unit").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        };
        navigationTimer.Start();
        var method=typeof(MainWindow).GetMethods(BindingFlags.NonPublic|BindingFlags.Instance).Single(m=>m.Name=="ShowValidationReview"&&m.GetParameters()[0].ParameterType==typeof(List<ValidationReviewItem>));
        bool saved=(bool)method.Invoke(main,[new List<ValidationReviewItem>{new("[ID 380] — Name: The unit name is blank.",unnamed)},true,"Review warnings",false])!;
        navigationTimer.Stop();Flush(main);
        Check(!saved && ReferenceEquals(Field<OobNode>(main,"_selectedNode"),unnamed) && hq.IsExpanded,"Warning jump cancels save and selects the exact combat record beneath expanded ancestors");
        Check(Field<int>(main,"_nation")==1 && ((TextBox)main.FindName("SearchBox")).Text=="","Warning jump switches faction and clears a hiding search");
        typeof(MainWindow).GetMethod("NavigateToValidationTarget",BindingFlags.NonPublic|BindingFlags.Instance)!.Invoke(main,[hq]);
        Check(ReferenceEquals(Field<OobNode>(main,"_selectedNode"),hq),"HQ navigation remains distinct from combat unit with the same numeric ID");
        main.Close();

        Write("groups.dat",new[]{"1"}.Concat(CreationRecords.Group(0,new UnitBlueprint {Headquarters=true,Name="Created Army",CommanderId=0,NativeTier=18,Perks=[new(),new(),new(),new()]},null)));
        Write("armygroups.dat",["2","0","First Army Group","1","0000;","18","001-002-003","1","Second Army Group","2","","18","004-005-006"]);
        Write("armygrouprefs.dat",["1","1","1","0","True","0"]);
        var dragWindow=new MainWindow(true);var dragData=Field<GrandTacticianDataService>(dragWindow,"_data");dragData.LoadAsync(root,config).GetAwaiter().GetResult();
        var flags=BindingFlags.Instance|BindingFlags.NonPublic;
        typeof(MainWindow).GetMethod("RebuildSide",flags)!.Invoke(dragWindow,[true]);dragWindow.Show();Flush(dragWindow);
        var army=dragData.Groups[0];var destination=dragData.ArmyGroups[1].Source;
        Check((bool)typeof(MainWindow).GetMethod("ValidGroupDropParent",flags)!.Invoke(dragWindow,[army,destination])!,"Drag target validation accepts Army Group with same native tier as Army");
        void SetField(string name,object value)=>typeof(MainWindow).GetField(name,flags)!.SetValue(dragWindow,value);
        SetField("_activeGroupDrag",army);SetField("_dropParent",destination);SetField("_dropIndex",0);
        SetField("_beforeDrag",Field<DisplayOobModel>(dragWindow,"_displayModel"));
        var dragType=typeof(MainWindow).GetNestedType("GroupDrag",BindingFlags.NonPublic)!;
        var payload=new DataObject();payload.SetData(dragType,Activator.CreateInstance(dragType,army)!);
        var viewport=(FrameworkElement)dragWindow.FindName("OobViewport");
        double zoom=Field<double>(dragWindow,"_zoom");
        var pointer=viewport.TranslatePoint(new Point(Field<double>(dragWindow,"_panX")+(destination.CanvasX+20)*zoom,Field<double>(dragWindow,"_panY")+(destination.CanvasY+20)*zoom),dragWindow);
        var args=(DragEventArgs)typeof(DragEventArgs).GetConstructors(flags).Single().Invoke([payload,DragDropKeyStates.LeftMouseButton,DragDropEffects.Move,dragWindow,pointer]);
        args.RoutedEvent=DragDrop.DragOverEvent;
        SetField("_dropIndex",-1);
        typeof(MainWindow).GetMethod("PreviewGroupDrag",flags)!.Invoke(dragWindow,[args]);
        Check(ReferenceEquals(Field<GroupNode>(dragWindow,"_dropParent"),destination) && args.Effects==DragDropEffects.Move,"Army Group card previews as a parent despite sharing Army native tier");
        args.RoutedEvent=DragDrop.DropEvent;
        typeof(MainWindow).GetMethod("OobDrop",flags)!.Invoke(dragWindow,[dragWindow,args]);Flush(dragWindow);
        Check(args.Handled && dragData.ArmyGroups[1].AttachedGroupIds.Contains(0) && army.ParentId==-1,"Actual OOB drop handler stages Army Group membership without changing native parent");
        Field<EditSession>(dragWindow,"_editSession").Undo(out _);dragWindow.Close();

        void Capture(Window window,string name)
        {
            var image=new RenderTargetBitmap((int)window.ActualWidth,(int)window.ActualHeight,96,96,PixelFormats.Pbgra32);image.Render(window);
            var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(image));using var file=File.Create(Path.Combine(output,name));encoder.Save(file);
        }
        void Accept(Window window,Action setup,Action click)
        {
            var timer=new DispatcherTimer {Interval=TimeSpan.FromMilliseconds(50)};int ticks=0;bool reviewed=false;
            timer.Tick+=(_,_)=>{var review=Application.Current.Windows.OfType<CreationReviewWindow>().FirstOrDefault();if(review is not null){reviewed=true;timer.Stop();review.DialogResult=true;}else if(++ticks>100){timer.Stop();window.Close();}};
            window.ContentRendered+=(_,_)=>{setup();timer.Start();click();};
            bool? result=window.ShowDialog();timer.Stop();Check(reviewed&&result==true,"Creation modal reaches and accepts review without disabled-field errors");
        }
    }
}
