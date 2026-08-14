using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;

namespace MarmaladeLauncher.Views.Dialogs;

public partial class InstallationSettingsWindow : Window {
    public InstallationSettingsWindow() {
        InitializeComponent();
    }
    
    private void OnCloseClick(object? sender, RoutedEventArgs e) {
        Close();
    }
}