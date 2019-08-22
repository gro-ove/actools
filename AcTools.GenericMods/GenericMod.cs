using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using AcTools.Utils;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI;
using FirstFloor.ModernUI.Commands;
using FirstFloor.ModernUI.Dialogs;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Presentation;
using FirstFloor.ModernUI.Windows;
using JetBrains.Annotations;

namespace AcTools.GenericMods {
    public class GenericMod : NotifyPropertyChanged, IDraggable {
        public static readonly string DescriptionExtension = ".jsgme";

        private readonly GenericModsEnabler _enabler;

        public string DisplayName { get; }
        public string ModDirectory { get; }

        internal GenericMod(GenericModsEnabler enabler, string modDirectory) {
            _enabler = enabler;
            DisplayName = Path.GetFileName(modDirectory);
            ModDirectory = modDirectory;
            Description = new Lazier<string>(() => Task.Run(() => {
                var d = Directory.GetFiles(ModDirectory, "*" + DescriptionExtension, SearchOption.TopDirectoryOnly);
                return d.Length > 0 ? File.ReadAllText(d[0]) : null;
            }));
        }

        public Lazier<string> Description { get; }

        private DelegateCommand _exploreCommand;

        public DelegateCommand ExploreCommand
            => _exploreCommand ?? (_exploreCommand = new DelegateCommand(() => { ProcessExtension.Start(@"explorer", new[] { ModDirectory }); }));

        private DelegateCommand _deleteCommand;

        public DelegateCommand DeleteCommand => _deleteCommand ?? (_deleteCommand = new DelegateCommand(() => {
            try {
                _enabler.DeleteMod(this);
            } catch (Exception e) {
                NonfatalError.Notify("Can’t delete mod", e);
            }
        }));

        private DelegateCommand _renameCommand;

        public DelegateCommand RenameCommand => _renameCommand ?? (_renameCommand = new DelegateCommand(() => {
            try {
                var newName = Prompt.Show("New name:", "Rename mod", DisplayName, required: true, maxLength: 120, placeholder: "?");
                if (newName == null) return;

                newName = FileUtils.EnsureFileNameIsValid(newName, false);
                if (string.IsNullOrEmpty(newName)) return;

                var newLocation = Path.Combine(Path.GetDirectoryName(ModDirectory) ?? "", newName);
                if (FileUtils.Exists(newLocation)) {
                    throw new InformativeException("Can’t rename mod", "Place is taken.");
                }

                _enabler.RenameMod(this, newLocation);
            } catch (Exception e) {
                NonfatalError.Notify("Can’t rename mod", e);
            }
        }));

        private GenericModFile[] _files;
        internal GenericModFile[] Files => _files ?? (_files = LoadFilesList().ToArray());

        private IEnumerable<GenericModFile> LoadFilesList() {
            foreach (var filename in FileUtils.GetFilesRecursive(ModDirectory)) {
                var relative = FileUtils.GetRelativePath(filename.ApartFromLast("-remove"), ModDirectory);
                if (relative.StartsWith("documentation\\", StringComparison.OrdinalIgnoreCase)
                        || relative.EndsWith(DescriptionExtension, StringComparison.OrdinalIgnoreCase)) continue;

                var destination = Path.Combine(_enabler.RootDirectory, relative);
                var backup = GenericModsEnabler.GetBackupFilename(_enabler.ModsDirectory, DisplayName, relative);

                if (filename.EndsWith("-remove")) {
                    yield return new GenericModFile(null, destination, backup, relative, DisplayName);
                } else {
                    yield return new GenericModFile(filename, destination, backup, relative, DisplayName);
                }
            }
        }

        public Task<GenericModFile[]> GetFilesAsync() {
            return Task.Run(() => Files);
        }

        private int _appliedOrder = -1;

        public int AppliedOrder {
            get => _appliedOrder;
            internal set {
                if (Equals(value, _appliedOrder)) return;

                ActionExtension.InvokeInMainThread(() => {
                    _appliedOrder = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(IsEnabled));
                });
            }
        }

        public bool IsEnabled => _appliedOrder != -1;

        private string[] _dependsOn;

        [CanBeNull]
        public string[] DependsOn {
            get => _dependsOn;
            internal set {
                if (value?.Length == 0) value = null;
                if (Equals(value, _dependsOn)) return;
                _dependsOn = value;
                OnPropertyChanged();
                DisplayDependsOn = value?.Select(x => $"“{x}”").JoinToReadableString();
            }
        }

        private string _displayDependsOn;

        public string DisplayDependsOn {
            get => _displayDependsOn;
            private set => Apply(value, ref _displayDependsOn);
        }

        public const string DraggableFormat = "X-GenericMod";

        string IDraggable.DraggableFormat => DraggableFormat;
    }
}