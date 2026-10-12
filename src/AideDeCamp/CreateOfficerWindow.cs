using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using AideDeCamp.Services;

namespace AideDeCamp;

public sealed class CreateOfficerWindow : Window
{
    private readonly GrandTacticianDataService _data;
    private readonly TextBox _first = new(), _last = new();
    private readonly ComboBox _faction = new(), _rank = new(), _branch = new(), _state = new();
    private readonly DatePicker _birth = new(), _commission = new();
    private readonly CheckBox _veteran = new() { Content = "Veteran" }, _westPoint = new() { Content = "West Point" }, _political = new() { Content = "Political officer" };
    private readonly TextBox[] _attributes = Enumerable.Range(0, 6).Select(i => new TextBox { Text = i < 2 ? "0" : "50" }).ToArray();
    private readonly TextBlock _portraitName = new() { Text = "Default silhouette", TextWrapping = TextWrapping.Wrap }, _error = new() { TextWrapping = TextWrapping.Wrap };
    private readonly Image _preview = new() { Width = 115, Height = 140, Stretch = Stretch.Uniform, Visibility = Visibility.Collapsed };
    private string? _portraitPath;
    public OfficerBlueprint? Blueprint { get; private set; }

    public CreateOfficerWindow(GrandTacticianDataService data, int faction)
    {
        _data = data; Title = "Create Officer"; Width = 700; Height = 830; MinWidth = 580; MinHeight = 600;
        Background = new SolidColorBrush(Color.FromRgb(20,28,36)); Foreground = Brushes.Gainsboro;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        var root = new DockPanel { Margin = new(20) }; Content = root;
        var footer = new StackPanel(); DockPanel.SetDock(footer, Dock.Bottom); root.Children.Add(footer);
        footer.Children.Add(_error);
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
        var create = new Button { Content = "Review & Create", Margin = new(5), Padding = new(12,6,12,6) }; create.Click += Create;
        buttons.Children.Add(new Button { Content = "Cancel", IsCancel = true, Margin = new(5) }); buttons.Children.Add(create); footer.Children.Add(buttons);
        var body = new StackPanel(); root.Children.Add(new ScrollViewer { Content = body, VerticalScrollBarVisibility = ScrollBarVisibility.Auto });
        body.Children.Add(new TextBlock { Text = "Create an officer in this save", FontSize = 23, Margin = new(0,0,0,12) });
        body.Children.Add(new TextBlock { Text = "The officer starts available and unassigned. Creation is staged until Save changes; Ctrl+Z undoes it.", TextWrapping = TextWrapping.Wrap, Margin = new(0,0,0,12) });
        _faction.ItemsSource = new[] { "Union", "Confederacy" }; _faction.SelectedIndex = faction;
        _rank.ItemsSource = OfficerRecords.Ranks; _rank.SelectedIndex = 4;
        _branch.ItemsSource = new[] { "No specialization", "Infantry", "Cavalry", "Artillery", "Engineer" }; _branch.SelectedIndex = 1;
        _state.ItemsSource = new[] { new AideDeCamp.Models.StateOption(-1, "Unspecified", "") }.Concat(data.StateOptions).ToArray();
        _state.DisplayMemberPath = "Name"; _state.SelectedIndex = 0;
        foreach (var picker in new[] { _birth, _commission }) {
            var textStyle = new Style(typeof(System.Windows.Controls.Primitives.DatePickerTextBox));
            textStyle.Setters.Add(new Setter(Control.ForegroundProperty, Brushes.Gainsboro));
            textStyle.Setters.Add(new Setter(Control.BackgroundProperty, new SolidColorBrush(Color.FromRgb(22,40,51))));
            picker.Resources.Add(typeof(System.Windows.Controls.Primitives.DatePickerTextBox), textStyle);
            picker.Loaded += (_,_) => {
                picker.ApplyTemplate();
                if (picker.Template.FindName("PART_TextBox", picker) is Control dateText) dateText.Foreground = Brushes.Gainsboro;
            };
        }
        _birth.SelectedDate = new DateTime(1820,1,1); _commission.SelectedDate = data.GameDate;
        Row(body,"First name",_first); Row(body,"Last name",_last); Row(body,"Faction",_faction); Row(body,"Rank",_rank);
        Row(body,"Branch",_branch); Row(body,"Home state",_state); Row(body,"Date of birth",_birth); Row(body,"Date rank attained",_commission);
        var portrait = new StackPanel(); var browse = new Button { Content = "Choose PNG portrait…", HorizontalAlignment = HorizontalAlignment.Left };
        browse.Click += Browse; portrait.Children.Add(browse);
        var clear = new Button { Content = "Use silhouette", HorizontalAlignment = HorizontalAlignment.Left }; clear.Click += (_,_) => { _portraitPath = null; _preview.Source = null; _preview.Visibility = Visibility.Collapsed; _portraitName.Text = "Default silhouette"; };
        portrait.Children.Add(clear); portrait.Children.Add(_portraitName); portrait.Children.Add(_preview);
        portrait.Children.Add(new TextBlock { Text = "PNG up to 4096×4096 pixels and 10 MB. Saving copies it to the selected game's Avatar folder. Keep that image with the installation when sharing the save.", TextWrapping = TextWrapping.Wrap });
        Row(body,"Portrait",portrait);
        foreach (var trait in new[] { _veteran, _westPoint, _political }) trait.Margin = new Thickness(0,0,12,0);
        var traits = new WrapPanel(); traits.Children.Add(_veteran); traits.Children.Add(_westPoint); traits.Children.Add(_political); Row(body,"Background",traits);
        string[] labels = ["Experience", "Fame", "Leadership", "Initiative", "Administration", "Cunning"];
        for (int i = 0; i < labels.Length; i++) Row(body,labels[i] + " (0–100)",_attributes[i]);

    }
    private static void Row(Panel panel, string title, FrameworkElement control)
    {
        var row = new Grid { Margin = new(0,4,0,4) }; row.ColumnDefinitions.Add(new() { Width = new(180) }); row.ColumnDefinitions.Add(new());
        row.Children.Add(new TextBlock { Text = title, VerticalAlignment = VerticalAlignment.Center }); Grid.SetColumn(control,1); control.MinHeight = 28; row.Children.Add(control); panel.Children.Add(row);
    }
    private void Browse(object sender, RoutedEventArgs e)
    {
        var picker = new Microsoft.Win32.OpenFileDialog { Filter = "PNG portrait (*.png)|*.png", CheckFileExists = true };
        if (Directory.Exists(_data.PortraitDirectory)) picker.InitialDirectory = _data.PortraitDirectory;
        if (picker.ShowDialog(this) != true) return;
        try
        {
            if (new FileInfo(picker.FileName).Length > 10*1024*1024) throw new InvalidDataException("Choose a PNG smaller than 10 MB.");
            using var input = File.OpenRead(picker.FileName);
            var decoder = new PngBitmapDecoder(input, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
            var image = decoder.Frames[0];
            if (image.PixelWidth > 4096 || image.PixelHeight > 4096) throw new InvalidDataException("Portrait dimensions must not exceed 4096 pixels.");
            image.Freeze(); _preview.Source = image; _preview.Visibility = Visibility.Visible; _portraitPath = picker.FileName; _portraitName.Text = Path.GetFileName(picker.FileName); _error.Text = "";
        }
        catch (Exception error) { _error.Text = error.Message; }
    }
    private void Create(object sender, RoutedEventArgs e)
    {
        try
        {
            var stats = _attributes.Select(a => double.Parse(a.Text, CultureInfo.InvariantCulture)).ToArray();
            var blueprint = new OfficerBlueprint(_first.Text.Trim(), _last.Text.Trim(), _faction.SelectedIndex, _rank.SelectedIndex + 1, _branch.SelectedIndex - 1,
                _birth.SelectedDate ?? throw new InvalidDataException("Choose a birth date."), _commission.SelectedDate ?? throw new InvalidDataException("Choose a date of rank."),
                (_state.SelectedItem as AideDeCamp.Models.StateOption)?.Id ?? -1, _veteran.IsChecked == true, _westPoint.IsChecked == true, _political.IsChecked == true,
                stats[0],stats[1],stats[2],stats[3],stats[4],stats[5],_portraitPath);
            OfficerRecords.Validate(blueprint, _data.GameDate ?? throw new InvalidDataException("Load a dated save first."));
            string summary = $"{_rank.Text} {blueprint.FirstName} {blueprint.LastName}\n{_faction.Text} • {_branch.Text}\nPortrait: {_portraitName.Text}";
            if (new CreationReviewWindow(summary, ["Creates an available officer in the loaded save. Rank eligibility for specific commands is still enforced by the game."]) { Owner = this }.ShowDialog() != true) return;
            Blueprint = blueprint; DialogResult = true;
        }
        catch (Exception error) { _error.Text = error.Message; }
    }
}

public partial class MainWindow
{
    private void CreateOfficer_Click(object sender, RoutedEventArgs e)
    {
        if (_saveInProgress || !CommitTypedDrafts()) return;
        if (_data.SaveDirectory is null || _data.GameDate is null) { StatusText.Text = "Open a campaign save to create an officer."; return; }
        try
        {
            var dialog = new CreateOfficerWindow(_data, _nation) { Owner = this };
            if (dialog.ShowDialog() != true || dialog.Blueprint is null) return;
            int id = _data.CreateOfficer(dialog.Blueprint, _editSession);
            _management = null; OpenWorkspace(dialog.Blueprint.Faction, "Officers"); SearchBox.Text = ""; RefreshManagement(); UpdateDirtyState();
            StatusText.Text = $"Created officer #{id}. Save changes writes the officer and imports the chosen portrait. Ctrl+Z undoes creation.";
        }
        catch (Exception error) { MessageBox.Show(this,error.Message,"Unable to create officer",MessageBoxButton.OK,MessageBoxImage.Warning); }
    }
}
