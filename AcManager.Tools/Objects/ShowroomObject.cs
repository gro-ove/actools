using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Windows.Input;
using AcManager.Tools.AcErrors;
using AcManager.Tools.AcManagersNew;
using AcManager.Tools.AcObjectsNew;
using AcManager.Tools.Data;
using AcManager.Tools.Helpers;
using AcManager.Tools.Lists;
using AcManager.Tools.Managers;
using AcTools.Utils;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI.Commands;
using FirstFloor.ModernUI.Helpers;
using Newtonsoft.Json.Linq;

namespace AcManager.Tools.Objects {
    public class ShowroomObject : AcJsonObjectNew {
        public ShowroomObject(IFileAcManager manager, string id, bool enabled) : base(manager, id, enabled) {}

        public override void Reload() {
            base.Reload();
            OnImageChangedValue(PreviewImage);
        }

        public override bool HandleChangedFile(string filename) {
            if (base.HandleChangedFile(filename)) {
                return true;
            }

            if (FileUtils.IsAffected(filename, PreviewImage)) {
                OnImageChangedValue(PreviewImage);
                return true;
            }

            if (FileUtils.IsAffected(filename, Kn5Filename)) {
                CheckKn5();
                return true;
            }

            var tail = Path.GetFileName(filename).ToLower();
            if (tail.StartsWith(Id + @".bank") || tail.StartsWith(@"track.wav")) {
                CheckSound();
            }

            return true;
        }

        protected override AutocompleteValuesList GetTagsList() {
            return SuggestionLists.ShowroomTagsList;
        }

        #region Sound
        private bool _hasSound;

        public bool HasSound {
            get { return _hasSound; }
            private set {
                if (Equals(value, _hasSound)) return;
                _hasSound = value;
                OnPropertyChanged();
                _toggleSoundCommand?.OnCanExecuteChanged();
            }
        }

        private bool _soundEnabled;

        public bool SoundEnabled {
            get { return _soundEnabled; }
            private set {
                if (Equals(value, _soundEnabled)) return;
                _soundEnabled = value;
                OnPropertyChanged();
            }
        }
        #endregion

        protected override void InitializeLocations() {
            base.InitializeLocations();
            JsonFilename = Path.Combine(Location, @"ui", @"ui_showroom.json");
            Kn5Filename = Path.Combine(Location, Id + ".kn5");
            SoundbankFilename = Path.Combine(Location, Id + ".bank");
            TrackFilename = Path.Combine(Location, "track.wav");
        }

        private string _previewImage;

        public string PreviewImage => _previewImage ?? (_previewImage = GetPreviewImage());

        private bool _previewProcessed;

        private string GetPreviewImage() {
            var path = Path.Combine(Location, "preview.jpg");
            if (!SettingsHolder.Content.DownloadShowroomPreviews || _previewProcessed) return path;

            _previewProcessed = true;
            if (!File.Exists(path)) {
                DownloadPreview();
            }

            return path;
        }

        private async void DownloadPreview() {
            string url;
            if (!DataProvider.Instance.ShowroomsPreviews.TryGetValue(Id.ToLowerInvariant(), out url)) return;

            try {
                using (var client = new WebClient()) {
                    await client.DownloadFileTaskAsync(url, PreviewImage);
                }
            } catch (Exception e) {
                Logging.Warning("Can’t download showroom’s preview: " + e);
            }
        }

        public string Kn5Filename { get; private set; }

        public string SoundbankFilename { get; private set; }

        public string TrackFilename { get; private set; }

        private void CheckKn5() {
            ErrorIf(!File.Exists(Kn5Filename), AcErrorType.Showroom_Kn5IsMissing);
        }

        private void CheckSound() {
            SoundEnabled = File.Exists(SoundbankFilename) || File.Exists(TrackFilename);
            HasSound = SoundEnabled || File.Exists(SoundbankFilename + '~') || File.Exists(TrackFilename + '~');
        }

        protected override void LoadOrThrow() {
            base.LoadOrThrow();
            CheckKn5();
            CheckSound();
        }

        protected override void LoadYear(JObject json) {
            Year = json.GetIntValueOnly("year");
            if (Year.HasValue) return;

            int year;
            if (DataProvider.Instance.ShowroomYears.TryGetValue(Id, out year)) {
                Year = year;
            } else if (Name != null) {
                Year = AcStringValues.GetYearFromName(Name) ?? AcStringValues.GetYearFromId(Name);
            }
        }

        protected override bool TestIfKunos() {
            return base.TestIfKunos() || (DataProvider.Instance.KunosContent[@"showrooms"]?.Contains(Id) ?? false);
        }

        public void ToggleSound() {
            var disabledSoundbankFilename = SoundbankFilename + '~';
            var disabledTrackFilename = TrackFilename + '~';
            if (SoundEnabled) {
                if (File.Exists(disabledSoundbankFilename)) {
                    FileUtils.Recycle(disabledSoundbankFilename);
                }

                if (File.Exists(disabledTrackFilename)) {
                    FileUtils.Recycle(disabledTrackFilename);
                }

                if (File.Exists(SoundbankFilename)) {
                    File.Move(SoundbankFilename, disabledSoundbankFilename);
                }

                if (File.Exists(TrackFilename)) {
                    File.Move(TrackFilename, disabledTrackFilename);
                }
            } else {
                if (File.Exists(disabledSoundbankFilename)) {
                    File.Move(disabledSoundbankFilename, SoundbankFilename);
                }

                if (File.Exists(disabledTrackFilename)) {
                    File.Move(disabledTrackFilename, TrackFilename);
                }
            }
        }

        private ICommandExt _toggleSoundCommand;

        public ICommand ToggleSoundCommand => _toggleSoundCommand ?? (_toggleSoundCommand = new DelegateCommand(() => {
            try {
                ToggleSound();
            } catch (ToggleException ex) {
                NonfatalError.Notify(ToolsStrings.ShowroomObject_CannotToggleSound,
                    ToolsStrings.ShowroomObject_CannotToggleSound_Commentary, ex);
            }
        }, () => HasSound));
    }
}
