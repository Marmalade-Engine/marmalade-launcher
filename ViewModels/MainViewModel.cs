using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace MarmaladeLauncher.ViewModels;

public partial class ProjectsViewModel : ViewModelBase { }
public partial class InstallationsViewModel : ViewModelBase { }

public partial class MainViewModel : ViewModelBase {
    [ObservableProperty] 
    private ViewModelBase _currentPage;

    public MainViewModel() {
        _currentPage = new ProjectsViewModel();
    }

    [RelayCommand]
    private void OpenProjects() {
        CurrentPage = new ProjectsViewModel();
    }
    
    [RelayCommand]
    private void OpenInstallations() {
        CurrentPage = new InstallationsViewModel();
    }
    
    [RelayCommand]
    private void OpenSettings() {
        CurrentPage = new SettingsViewModel();
    }
}