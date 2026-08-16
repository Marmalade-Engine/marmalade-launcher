using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace MarmaladeLauncher.Models;

public class LocalisationEntry {
    public enum LocalisationStatus {
        LocalisationStatus_UNSUPPORTED,
        LocalisationStatus_PARTIAL,
        LocalisationStatus_FULL,
    }

    public class LanguageMetadata {
        public string LocaleKey { get; private set; }
        public LocalisationStatus Status { get; private set; }

        public string DisplayName {
            get {
                try {
                    var culture = new CultureInfo(LocaleKey);
                    return culture.DisplayName;
                }
                catch {
                    return LocaleKey;
                }
            }
        }

        public bool IsPartial => Status == LocalisationStatus.LocalisationStatus_PARTIAL;

        public string StatusSuffix => IsPartial ? " (Partial Support)" : string.Empty;

        public LanguageMetadata(string localeKey, LocalisationStatus status) {
            LocaleKey = localeKey;
            Status = status;
        }
    }

    public class LanguageDisplay {
        private static readonly List<LanguageMetadata> _allLanguages = new List<LanguageMetadata>() {
            new LanguageMetadata("en-GB", LocalisationStatus.LocalisationStatus_FULL),
            new LanguageMetadata("en-US", LocalisationStatus.LocalisationStatus_PARTIAL),
        };

        public static List<LanguageMetadata> languages => _allLanguages
            .Where(l => l.Status != LocalisationStatus.LocalisationStatus_UNSUPPORTED)
            .ToList();
    }
}