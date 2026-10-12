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
        void Write(string file,IEnumerable<string> lines)=>File.WriteAllLines(Path.Combine(root,file),lines);
        Write("version.dat",["1.142"]);Write("scenario.dat",["0","0","8","2","1863"]);
        var officer=new OfficerBlueprint("Test","General",0,7,0,new(1820,1,1),new(1861,1,1));
        Write("commanders.txt",new[]{"2"}.Concat(OfficerRecords.Commander(0,officer,"")).Concat(OfficerRecords.Commander(1,officer with {FirstName="Unused"},"")));
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
        Capture(w,"army-group-creation.png");w.Close();
        w=new CreateUnitWindow(data,new NamingSchemeService(),catalog,0,null);
        Accept(w,()=>{Select(Field<ComboBox>(w,"_tier"),19);Field<TextBox>(w,"_name").Text="Created Army Group";Select(Field<ComboBox>(w,"_commander"),1);Field<ListBox>(w,"_armyMembers").SelectedIndex=0;},()=>typeof(CreateUnitWindow).GetMethod("ReviewCreate",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(w,[w,new RoutedEventArgs()]));
        Check(w.Request is { Blueprint.ArmyGroup:true }&&w.Request.Blueprint.AttachedCommandIds.SequenceEqual(new[]{0}),"Army Group modal review returns distinct native-record request");
        var ow=new CreateOfficerWindow(data,0);ow.Show();Flush(ow);
        Check(Field<ComboBox>(ow,"_rank").Items.Count==9&&Field<DatePicker>(ow,"_commission").SelectedDate==data.GameDate,"Officer dialog offers real ranks and defaults to campaign date");
        Check(Descendants(ow).OfType<Button>().Any(b=>b.Content?.ToString()=="Choose PNG portrait…"),"Officer dialog exposes PNG portrait selection");
        Capture(ow,"officer-creation.png");ow.Close();
        ow=new CreateOfficerWindow(data,1);
        Accept(ow,()=>{Field<TextBox>(ow,"_first").Text="New";Field<TextBox>(ow,"_last").Text="Officer";Field<ComboBox>(ow,"_rank").SelectedIndex=6;},()=>typeof(CreateOfficerWindow).GetMethod("Create",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(ow,[ow,new RoutedEventArgs()]));
        Check(ow.Blueprint is {Faction:1,Rank:7,FirstName:"New"},"Officer modal review returns selected faction and MG rank");

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
