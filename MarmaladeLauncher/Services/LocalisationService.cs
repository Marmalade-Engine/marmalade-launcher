using System;
using System.Diagnostics;
using System.Globalization;

namespace MarmaladeLauncher.Services;

public class LocalisationService {
    private readonly SettingsService _settingsService;

    private string _currentLocale = "en-GB";
    
    public string CurrentLocale {
        get => _currentLocale;
        set {
            if (!string.IsNullOrEmpty(value)) {
                _currentLocale = value;
                SetLocale(_currentLocale);
            }
        }
    }

    public LocalisationService(SettingsService settingsService) {
        _settingsService = settingsService;

        SyncLocale();
    }

    private void SetLocale(string locale) {
        var formattedLocale = locale.Replace('_', '-');
        var culture = new CultureInfo(formattedLocale);

        CultureInfo.CurrentCulture = culture;
        CultureInfo.CurrentUICulture = culture;
    }

    private void SyncLocale() {
        CurrentLocale = _settingsService.Settings.CurrentLocale;
    }
}