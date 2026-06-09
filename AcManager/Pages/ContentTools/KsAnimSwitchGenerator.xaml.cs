using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Data;
using AcManager.Tools.Data;
using AcManager.Tools.Lists;
using AcTools.Kn5File;
using AcTools.KsAnimFile;
using AcTools.Numerics;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI;
using FirstFloor.ModernUI.Commands;
using FirstFloor.ModernUI.Dialogs;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Presentation;
using FirstFloor.ModernUI.Windows;
using FirstFloor.ModernUI.Windows.Attached;
using Newtonsoft.Json;

namespace AcManager.Pages.ContentTools {
    public partial class KsAnimSwitchGenerator {
        protected override Task<bool> LoadAsyncOverride(IProgress<AsyncProgressEntry> progress, CancellationToken cancellation) {
            return Task.FromResult(true);
        }

        protected override void InitializeOverride(Uri uri) {
            Frames.Add(new FrameEntry());
            Frames.Add(new FrameEntry());
            Frames.ItemPropertyChanged += (sender, args) => {
                if (args.PropertyName == nameof(FrameEntry.Removed)) {
                    Frames.Remove(sender as FrameEntry);
                }
                if (args.PropertyName == string.Empty) {
                    UpdateSuggestedConfig();
                }
            };
            Frames.CollectionChanged += (sender, args) => {
                _saveCommand?.RaiseCanExecuteChanged();
                UpdateSuggestedConfig();
            };
        }

        private string _suggestedConfig;

        public string SuggestedConfig {
            get => _suggestedConfig;
            set => Apply(value, ref _suggestedConfig);
        }

        private void UpdateSuggestedConfig() {
            SuggestedConfig = "[WING_BASED_SWITCH_...]\nWING = \n" + Frames.Select((x, i) => new { x, i })
                    .Where(x => x.x.Nodes.Count > 0)
                    .Select(x => $"POINT_{x.i} = {x.i / (Frames.Count - 1d)}\nPOINT_{x.i}_NODES = {x.x.Nodes.JoinToString(", ")}")
                    .JoinToString("\n");
        }

        public static AutocompleteValuesList CarTagsList { get; } = new AutocompleteValuesList();
        public static ListCollectionView CarTagsListView => CarTagsList.View;

        private string _modelFilename;

        public string ModelFilename {
            get => _modelFilename;
            set => Apply(value, ref _modelFilename, () => {
                _saveCommand?.RaiseCanExecuteChanged();
                ModelFileName = string.IsNullOrWhiteSpace(value) ? null : Path.GetFileName(value);
            });
        }

        private string _modelFileName;

        public string ModelFileName {
            get => _modelFileName;
            set => Apply(value, ref _modelFileName);
        }

        public class FrameEntry : NotifyPropertyChanged, IDraggable, IDraggableCloneable {
            public TagsCollection Nodes { get; private set; } = new TagsCollection();

            public FrameEntry() {
                Nodes.CollectionChanged += (sender, args) => OnPropertyChanged(string.Empty);
            }

            private bool _removed;

            public bool Removed {
                get => _removed;
                set => Apply(value, ref _removed);
            }

            private DelegateCommand _removeCommand;

            public DelegateCommand RemoveCommand => _removeCommand ?? (_removeCommand = new DelegateCommand(() => { Removed = true; }));

            public bool CanBeCloned => true;

            public object Clone() {
                return new FrameEntry {
                    Nodes = new TagsCollection(Nodes)
                };
            }

            #region Draggable
            public const string DraggableFormat = "Data-KnAnimSwitchFrame";

            [JsonIgnore]
            string IDraggable.DraggableFormat => DraggableFormat;
            #endregion
        }

        public ChangeableObservableCollection<FrameEntry> Frames { get; } = new ChangeableObservableCollection<FrameEntry>();

        private DelegateCommand _addFrameCommand;

        public DelegateCommand AddFrameCommand => _addFrameCommand ?? (_addFrameCommand = new DelegateCommand(() => {
            Frames.Add(new FrameEntry());
        }));

        private DelegateCommand _convertCommand;

        public DelegateCommand ConvertCommand => _convertCommand ?? (_convertCommand = new DelegateCommand(() => {
            var input = FileRelatedDialogs.Open(new OpenDialogParams {
                DirectorySaveKey = "ksanimkn5",
                Filters = { DialogFilterPiece.Kn5Files, DialogFilterPiece.AllFiles },
                Title = "Select main car KN5"
            });
            if (input == null) return;
            ModelFilename = input;
            Task.Run(() => {
                try {
                    var nodes = Kn5.FromFile(ModelFilename).Nodes.Where(x => x.NodeClass == Kn5NodeClass.Base)
                            .Select(x => x.Name).Distinct().Where(x => !x.StartsWith(@"FBX:")).ToList();
                    ActionExtension.InvokeInMainThreadAsync(() => { CarTagsList.ReplaceEverythingBy(nodes); });
                } catch (Exception e) {
                    NonfatalError.Notify("Failed to collect node names", e);
                }
            });
        }));

        private DelegateCommand _saveCommand;

        public DelegateCommand SaveCommand => _saveCommand ?? (_saveCommand = new DelegateCommand(() => {
            if (string.IsNullOrWhiteSpace(ModelFilename)) return;
            var stored = CacheStorage.Get<string>($"ksanimkn5.{ModelFilename}");
            var output = FileRelatedDialogs.Save(new SaveDialogParams {
                InitialDirectory = Path.Combine(Path.GetDirectoryName(ModelFilename) ?? string.Empty, "animations"),
                RestoreDirectory = string.IsNullOrEmpty(stored),
                Filters = { new DialogFilterPiece("Animations", "*.ksanim"), DialogFilterPiece.AllFiles },
                Title = "Select destination file",
                DefaultFileName = string.IsNullOrEmpty(stored) ? "switch.ksanim" : Path.GetFileName(stored),
                OverwritePrompt = false
            });
            if (output == null) return;

            try {
                var targetKn5 = Kn5.FromFile(ModelFilename);
                var frameHide = Mat4x4.CreateScale(float.Epsilon, float.Epsilon, float.Epsilon);

                Mat4x4 FrameShow(string name) {
                    return (targetKn5.Nodes.FirstOrDefault(n => n.Name == name && n.NodeClass == Kn5NodeClass.Base)
                            ?? throw new Exception($"No node named “{name}”")).Transform;
                }

                KsAnim.FromEntries(Frames.SelectMany(x => x.Nodes).Select(x => new KsAnimEntryV1 {
                    NodeName = x,
                    Matrices = Enumerable.Range(0, Frames.Count * 3 - 2).Select(
                            i => (i + 1) / 3 == Frames.FindIndex(y => y.Nodes.Contains(x)) ? FrameShow(x) : frameHide).ToArray()
                })).Save(output);
            } catch (Exception e) {
                NonfatalError.Notify("Can’t generate animation", e);
            }
        }, () => Frames.Count >= 2 && ModelFilename != null));
    }
}