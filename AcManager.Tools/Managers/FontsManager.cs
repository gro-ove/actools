using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AcManager.Tools.AcManagersNew;
using AcManager.Tools.AcObjectsNew;
using AcManager.Tools.Helpers;
using AcManager.Tools.Managers.Directories;
using AcManager.Tools.Objects;
using AcManager.Tools.SemiGui;
using AcTools.DataFile;
using AcTools.Utils;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Presentation;

namespace AcManager.Tools.Managers {
    public class FontsManager : AcManagerFileSpecific<FontObject> {
        public static FontsManager Instance { get; private set; }

        public static FontsManager Initialize() {
            if (Instance != null) throw new Exception("already initialized");
            return Instance = new FontsManager();
        }

        public FontsManager() {
            SettingsHolder.Content.PropertyChanged += Content_PropertyChanged;
        }

        private void Content_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e) {
            if (e.PropertyName == nameof(SettingsHolder.ContentSettings.FontIconCharacter)) {
                foreach (var fontObject in LoadedOnly) {
                    fontObject.ResetIconBitmap();
                }
            }
        }

        public override string SearchPattern => "*.txt";

        public override FontObject GetDefault() {
            var v = WrappersList.FirstOrDefault(x => x.Value.Id.Contains("arial"));
            return v == null ? base.GetDefault() : EnsureWrapperLoaded(v);
        }

        public override BaseAcDirectories Directories => AcRootDirectory.Instance.FontsDirectories;

        protected override FontObject CreateAcObject(string id, bool enabled) {
            return new FontObject(this, id, enabled);
        }

        protected override bool ShouldSkipFile(string objectLocation, string filename) {
            return !FontObject.BitmapExtensions.Any(x => filename.EndsWith(x, StringComparison.OrdinalIgnoreCase)) &&
                   !filename.EndsWith(FontObject.FontExtension, StringComparison.OrdinalIgnoreCase);
        }

        protected override string GetObjectLocation(string filename, out bool inner) {
            var minLength = Math.Min(Directories.EnabledDirectory.Length,
                Directories.DisabledDirectory?.Length ?? int.MaxValue);

            inner = false;
            while (filename.Length > minLength) {
                var parent = Path.GetDirectoryName(filename);
                if (parent == null) return null;

                if (parent == Directories.EnabledDirectory || parent == Directories.DisabledDirectory) {
                    var special = FontObject.BitmapExtensions.FirstOrDefault(x => filename.EndsWith(x, StringComparison.OrdinalIgnoreCase));
                    if (special == null) return filename;

                    inner = true;
                    return filename.ApartFromLast(special, StringComparison.OrdinalIgnoreCase) + FontObject.FontExtension;
                }

                inner = true;
                filename = parent;
            }

            return null;
        }

        public override void Toggle(string id) {
            if (!Directories.Actual) return;
            if (id == null) throw new ArgumentNullException(nameof(id));

            var wrapper = GetWrapperById(id);
            if (wrapper == null) {
                throw new ArgumentException(@"ID is wrong", nameof(id));
            }

            var currentLocation = ((AcCommonObject)wrapper.Value).Location;
            var currentBitmapLocation = ((FontObject)wrapper.Value).FontBitmap;
            var path = wrapper.Value.Enabled ? Directories.DisabledDirectory : Directories.EnabledDirectory;
            if (path == null) {
                throw new Exception("Object can't be toggled");
            }

            var newLocation = Path.Combine(path, wrapper.Value.Id);
            var newBitmapLocation = currentBitmapLocation == null ? null : Path.Combine(path, Path.GetFileName(currentBitmapLocation));

            if (File.Exists(newLocation) || currentBitmapLocation != null && File.Exists(newBitmapLocation)) {
                throw new ToggleException("Place is taken");
            }

            try {
                using (IgnoreChanges()) {
                    FileUtils.Move(currentLocation, newLocation);

                    if (currentBitmapLocation != null) {
                        FileUtils.Move(currentBitmapLocation, newBitmapLocation);
                    }

                    RemoveFromList(id);
                    var obj = CreateAndLoadAcObject(id, Directories.CheckIfEnabled(newLocation));
                    InnerWrappersList.Add(new AcItemWrapper(this, obj));
                    UpdateList();
                }
            } catch (Exception e) {
                throw new ToggleException(e.Message);
            }
        }

        public override void Delete(string id) {
            if (!Directories.Actual) return;
            if (id == null) throw new ArgumentNullException(nameof(id));

            var obj = GetById(id);
            if (obj == null) throw new ArgumentException(@"ID is wrong", nameof(id));

            using (IgnoreChanges()) {
                FileUtils.Recycle(obj.Location, obj.FontBitmap);
                if (!FileUtils.Exists(obj.Location)) {
                    RemoveFromList(id);
                }
            }
        }

        public DateTime? LastUsingsRescan {
            get { return ValuesStorage.GetDateTime("FontsManager.LastUsingsRescan"); }
            set {
                if (Equals(value, LastUsingsRescan)) return;

                if (value.HasValue) {
                    ValuesStorage.Set("FontsManager.LastUsingsRescan", value.Value);
                } else {
                    ValuesStorage.Remove("FontsManager.LastUsingsRescan");
                }

                OnPropertyChanged();
            }
        }

        public async Task<List<string>> UsingsRescan(IProgress<string> progress = null, CancellationToken cancellation = default(CancellationToken)) {
            try {
                await EnsureLoadedAsync();
                if (cancellation.IsCancellationRequested) return null;

                await CarsManager.Instance.EnsureLoadedAsync();
                if (cancellation.IsCancellationRequested) return null;

                var list = (await TaskExtension.WhenAll(CarsManager.Instance.LoadedOnly.Select(async car => {
                    if (cancellation.IsCancellationRequested) return null;

                    progress?.Report(car.Id);
                    return new {
                        CarId = car.Id,
                        FontIds = (await Task.Run(() => new IniFile(car.Location, "digital_instruments.ini"), cancellation))
                                .Values.Select(x => x.Get("FONT")?.ToLowerInvariant()).Where(x => !string.IsNullOrWhiteSpace(x)).ToList()
                    };
                }), 12)).Where(x => x != null && x.FontIds.Count > 0).ToListIfItsNot();

                if (cancellation.IsCancellationRequested) return null;
                foreach (var fontObject in LoadedOnly) {
                    fontObject.UsingsCarsIds = list.Where(x => x.FontIds.Contains(fontObject.AcId)).Select(x => x.CarId).ToArray();
                }

                return list.SelectMany(x => x.FontIds).Distinct().Where(id => GetWrapperById(id + FontObject.FontExtension) == null).ToList();
            } catch (TaskCanceledException) {
                return null;
            } catch (Exception e) {
                NonfatalError.Notify("Can’t rescan cars", e);
                return null;
            } finally {
                LastUsingsRescan = DateTime.Now;
            }
        }

        private AsyncCommand _usedRescanCommand;

        public AsyncCommand UsingsRescanCommand => _usedRescanCommand ?? (_usedRescanCommand = new AsyncCommand(o => UsingsRescan()));
    }
}