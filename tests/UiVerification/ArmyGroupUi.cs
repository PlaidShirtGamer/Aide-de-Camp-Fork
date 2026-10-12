using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using AideDeCamp;
using AideDeCamp.Models;
using AideDeCamp.Services;

internal static partial class Program
{
    private static void ArmyGroupUi(string output)
    {
        Directory.CreateDirectory(output);
        var window = new MainWindow(true) { Width = 1600, Height = 1000 };
        ((CheckBox)window.FindName("RegimentalScaleCheck")).IsChecked = true;
        OobPresentation.RegimentalScale = true;
        var flags = BindingFlags.Instance | BindingFlags.NonPublic;
        var data = (GrandTacticianDataService)typeof(MainWindow).GetField("_data", flags)!.GetValue(window)!;
        var groups = (Dictionary<int, GroupNode>)data.Groups;
        int[] tiers = [18, 16, 15, 14];
        string[] names = ["Test Army", "Test Corps", "Test Division", "Test Brigade"];
        for (int i = 0; i < 4; i++) groups.Add(i, new GroupNode { GroupId = i, Name = names[i], RawName = names[i], ParentId = i - 1,
            UnitTier = tiers[i], Nation = 0, IsExpanded = true, CommanderDisplayName = "MG Test Officer" });
        ((List<CombatUnitNode>)data.Units).Add(new CombatUnitNode { UnitId = 0, ParentId = 3, UnitType = 0, UnitTier = 13,
            Name = "Test Regiment", Nation = 0, TotalMenRaw = 600 });
        var record = ArmyGroupDisplay.Parse(["1", "0", "Test Army Group", "1", "0000;", "18", "001-002-003"], groups, new Dictionary<int,int>()).Single();
        ((List<ArmyGroupDisplay>)data.ArmyGroups).Add(record);
        groups[0].ArmyGroupName = record.Source.Name;
        typeof(MainWindow).GetMethod("RebuildSide", flags)!.Invoke(window, [true]);
        foreach (var g in groups.Values) g.CaptureOriginalState();
        foreach (var u in data.Units) u.CaptureOriginalState();
        window.Show(); Flush(window);
        typeof(MainWindow).GetMethod("FitOob", flags)!.Invoke(window, null); Flush(window);
        var model = (DisplayOobModel)typeof(MainWindow).GetField("_displayModel", flags)!.GetValue(window)!;
        Check(model.Roots.Count == 1 && model.CommandsById.Count == 5, "WPF shows all five HQ tiers above the regiment");
        Check(model.CommandsById[3].Depth == 4 && groups[0].TierName == "Army", "Army Group adds a sixth level without relabeling the Army");
        typeof(MainWindow).GetMethod("ShowGroup", flags)!.Invoke(window, [record.Source]); Flush(window);
        var panel = (Panel)window.FindName("SharedDetailPanel");
        Check(!Descendants(panel).OfType<TextBox>().Any() && !Descendants(panel).OfType<Button>().Any(), "Army Group detail panel exposes inspection without write controls");
        Check(groups[0].IdentitySecondary.Contains("Under Test Army Group"), "Army detail identifies the saved Army Group membership");
        var bitmap = new RenderTargetBitmap((int)window.ActualWidth, (int)window.ActualHeight, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(window);
        var png = new PngBitmapEncoder(); png.Frames.Add(BitmapFrame.Create(bitmap));
        using (var stream = File.Create(Path.Combine(output, "army-group-tree.png"))) png.Save(stream);
        typeof(MainWindow).GetMethod("ShowGroup", flags)!.Invoke(window, [groups[0]]); Flush(window);
        Check(Descendants(panel).OfType<TextBox>().Any(), "Regular Army HQ retains its editing controls");
        window.Close();
    }
}
