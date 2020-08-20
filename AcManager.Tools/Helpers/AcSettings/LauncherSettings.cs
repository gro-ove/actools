using System.Globalization;
using System.IO;
using System.Linq;
using AcManager.Tools.Managers;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Presentation;
using JetBrains.Annotations;

namespace AcManager.Tools.Helpers.AcSettings {
    public class LauncherSettings : IniSettings {
        internal LauncherSettings() : base("launcher") {
            LanguageEntries = new BetterObservableCollection<AcLanguageEntry>(
                    Directory.GetFiles(Path.Combine(AcRootDirectory.Instance.Value ?? "", "system", "locales"), "*.ini")
                            .Select(Path.GetFileNameWithoutExtension)
                            .Where(x => x.Length >= 2 && x.Length <= 5 && x.All(y => char.IsLetter(y) || y == '-'))
                            .Prepend(@"en").Distinct().Select(x => new AcLanguageEntry(x)));
        }

        public sealed class AcLanguageEntry : Displayable, IWithId {
            [NotNull]
            public string Id { get; }

            public AcLanguageEntry([NotNull] string id) {
                Id = id;

                try {
                    DisplayName = new CultureInfo(id == @"chs" ? @"zh-CN" : id == @"cht" ? @"zh-TW" : id).NativeName.ToTitle();
                } catch (CultureNotFoundException) {
                    DisplayName = id;
                }
            }

            private bool Equals(AcLanguageEntry other) {
                return Id == other.Id;
            }

            public override bool Equals(object obj) {
                if (ReferenceEquals(null, obj)) {
                    return false;
                }
                if (ReferenceEquals(this, obj)) {
                    return true;
                }
                if (obj.GetType() != GetType()) {
                    return false;
                }
                return Equals((AcLanguageEntry)obj);
            }

            public override int GetHashCode() {
                return Id.GetHashCode();
            }
        }

        public BetterObservableCollection<AcLanguageEntry> LanguageEntries { get; }

        [CanBeNull]
        public AcLanguageEntry LanguageEntry {
            get => LanguageEntries.GetByIdOrDefault(Language) ?? LanguageEntries.GetByIdOrDefault("en");
            set => Language = value?.Id;
        }

        private string _language;

        [CanBeNull]
        public string Language {
            get => _language;
            set {
                if (Equals(value, _language)) return;
                _language = value;
                OnPropertyChanged();
            }
        }

        protected override void LoadFromIni() {
            Language = Ini["WINDOW"].GetNonEmpty("LANGUAGE", "en");
        }

        protected override void SetToIni() {
            Ini["WINDOW"].Set("LANGUAGE", Language);
        }
    }
}