using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Markup.Xaml;
using MarmaladeLauncher.ViewModels;

namespace MarmaladeLauncher.Views.Pages;

public partial class InstallationsView : UserControl {
    public InstallationsView() {
        InitializeComponent();
    }

    private void OnInstallBackdropPointerPressed(object? sender, PointerPressedEventArgs e) {
        if (DataContext is InstallationsViewModel vm) {
            vm.CloseInstallCommand.Execute(null);
        }
    }
    
    private void OnSettingsBackdropPointerPressed(object? sender, PointerPressedEventArgs e) {
        if (DataContext is InstallationsViewModel vm) {
            vm.CloseSettingsCommand.Execute(null);
        }
    }
}