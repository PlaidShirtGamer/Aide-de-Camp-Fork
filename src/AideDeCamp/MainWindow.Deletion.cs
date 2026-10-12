using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using AideDeCamp.Models;

namespace AideDeCamp;

public partial class MainWindow
{
    private void DeleteUnit_Click(object sender, RoutedEventArgs e)
    {
        if (_saveInProgress || _data.SaveDirectory is null || !CommitTypedDrafts()) return;
        var element = sender as FrameworkElement;
        OobNode? node = Equals(element?.Tag, "RosterDelete") ? _creationRosterContext
            : sender is Button ? _selectedNode : element?.DataContext as OobNode;
        if (node is null) { StatusText.Text = "Select one land formation to delete."; return; }
        try {
            var review = new DeleteFormationWindow(node.Name, _data.DeletionSummary(node)) { Owner = this };
            if (review.ShowDialog() != true) return;
            _data.DeleteUnit(node, _editSession);
            if (node is CombatUnitNode unit) _typedDrafts.Remove(unit);
            if (node is GroupNode group) _groupNameDrafts.Remove(group);
            ClearBatchSelection(); ClearDetails(); _selectedUnit = null; _selectedNode = null;
            _creationRosterContext = null; _management = null;
            RebuildSide(); UpdateDirtyState();
            StatusText.Text = $"Deleted {node.Name} in ADC. Ctrl+Z restores it. Save changes writes the deletion with a full backup.";
        } catch (Exception error) {
            Services.ErrorLog.Write("Delete formation", error);
            MessageBox.Show(this, error.Message, "Unable to delete formation", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void ClearRemovedSelection()
    {
        bool removed = _selectedUnit is not null && !_data.Units.Contains(_selectedUnit);
        if (_selectedNode is GroupNode g) removed |= g.IsArmyGroup
            ? !_data.ArmyGroups.Any(a => ReferenceEquals(a.Source, g))
            : !_data.Groups.Values.Contains(g);
        if (!removed) return;
        ClearBatchSelection(); ClearDetails(); _selectedUnit = null; _selectedNode = null;
    }
}

public sealed class DeleteFormationWindow : Window
{
    public DeleteFormationWindow(string name, string summary)
    {
        Title = "Delete formation"; Width = 600; SizeToContent = SizeToContent.Height;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background = new SolidColorBrush(Color.FromRgb(20, 28, 36)); Foreground = Brushes.Gainsboro;
        var panel = new StackPanel { Margin = new Thickness(22) }; Content = panel;
        panel.Children.Add(new TextBlock { Text = "Delete " + name + "?", FontSize = 22, TextWrapping = TextWrapping.Wrap });
        panel.Children.Add(new TextBlock { Text = summary, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 16, 0, 16) });
        panel.Children.Add(new TextBlock { Text = "This stages one undoable deletion. Save changes writes it with a full backup.", TextWrapping = TextWrapping.Wrap });
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 18, 0, 0) };
        buttons.Children.Add(new Button { Content = "Cancel", IsCancel = true });
        var confirm = new Button { Content = "Delete formation", Margin = new Thickness(8, 0, 0, 0) };
        confirm.Click += (_, _) => DialogResult = true; buttons.Children.Add(confirm); panel.Children.Add(buttons);
    }
}
