using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using AideDeCamp.Models;

namespace AideDeCamp;

public sealed record ValidationReviewItem(string Message, OobNode? Target = null);

public sealed class ValidationReviewWindow : Window
{
    public OobNode? Target { get; private set; }
    public bool BrowseUnclassified { get; private set; }
    public ValidationReviewWindow(IEnumerable<ValidationReviewItem> messages, bool allowOverride, string title, bool browseUnclassified = false)
    {
        Title = title; Width = 800; Height = 550; MinWidth = 550; MinHeight = 300;
        Background = new SolidColorBrush(Color.FromRgb(20,28,36)); Foreground = Brushes.White;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        var panel = new DockPanel { Margin = new(16) }; Content = panel;
        var bottom = new StackPanel(); DockPanel.SetDock(bottom,Dock.Bottom); panel.Children.Add(bottom);
        bottom.Children.Add(new TextBlock { Text = "Go to unit closes this review without saving, so you can inspect or edit the record.", TextWrapping = TextWrapping.Wrap, Margin = new(0,8,0,8) });
        if (browseUnclassified) {
            var browse = new Button { Content = "Browse unclassified records for current faction" };
            browse.Click += (_,_) => { BrowseUnclassified = true; DialogResult = false; }; bottom.Children.Add(browse);
        }
        var ack = new CheckBox { Content = "I understand these valid but risky values could damage this campaign.", Visibility = allowOverride ? Visibility.Visible : Visibility.Collapsed, Margin = new(0,10,0,8) };
        var proceed = new Button { Content = allowOverride ? "Acknowledge and save with backup" : "Return to editing", IsEnabled = !allowOverride };
        ack.Checked += (_,_) => proceed.IsEnabled = true; ack.Unchecked += (_,_) => proceed.IsEnabled = false;
        proceed.Click += (_,_) => DialogResult = allowOverride; bottom.Children.Add(ack); bottom.Children.Add(proceed);
        var rows = new StackPanel(); panel.Children.Add(new ScrollViewer { Content = rows, VerticalScrollBarVisibility = ScrollBarVisibility.Auto });
        foreach (var item in messages) {
            var row = new DockPanel { Margin = new(0,0,0,12) };
            if (item.Target is not null) {
                var jump = new Button { Content = item.Target is GroupNode ? "Go to HQ" : "Go to unit", Margin = new(10,0,0,0), VerticalAlignment = VerticalAlignment.Top };
                jump.Click += (_,_) => { Target = item.Target; DialogResult = false; };
                DockPanel.SetDock(jump,Dock.Right); row.Children.Add(jump);
            }
            row.Children.Add(new TextBlock { Text = item.Message, TextWrapping = TextWrapping.Wrap }); rows.Children.Add(row);
        }
    }
}
