using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MarmaladeLauncher.Services;

namespace MarmaladeLauncher.ViewModels;

public partial class ProjectsViewModel : ViewModelBase { }
public partial class InstallationsViewModel : ViewModelBase { }

public partial class MainViewModel : ViewModelBase {
    private readonly ProjectsViewModel _projectsViewModel = new();
    private readonly InstallationsViewModel _installationsViewModel = new();
    private readonly SettingsViewModel _settingsViewModel = new();

    [ObservableProperty] 
    private ViewModelBase _currentPage;
    
    public MainViewModel(SettingsService settingsService, LocalisationService localisationService) {
        _settingsViewModel = new SettingsViewModel(settingsService, localisationService);
        _currentPage = _projectsViewModel;
    }

    [RelayCommand]
    private void OpenProjects() => SwitchPage(_projectsViewModel);
    
    [RelayCommand]
    private void OpenInstallations() => SwitchPage(_installationsViewModel);
    
    [RelayCommand]
    private void OpenSettings() => SwitchPage(_settingsViewModel);
    
    private void SwitchPage(ViewModelBase page) {
        if (CurrentPage == page) return;
        CurrentPage = page;
    }
}