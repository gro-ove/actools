using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using AcManager.Tools.AcErrors;
using AcManager.Tools.AcManagersNew;
using AcManager.Tools.Data.GameSpecific;
using AcManager.Tools.Helpers;
using AcManager.Tools.Managers;
using AcManager.Tools.SemiGui;
using AcTools.DataFile;
using AcTools.Processes;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI.Commands;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Presentation;
using FirstFloor.ModernUI.Windows.Controls;
using JetBrains.Annotations;

namespace AcManager.Tools.Objects {
    public class SpecialEventObject : KunosEventObjectBase {
        public sealed class AiLevelEntry : Displayable, IWithId<int> {
            private static readonly string[] DisplayNames = {
                ToolsStrings.DifficultyLevel_Easy, ToolsStrings.DifficultyLevel_Medium,
                ToolsStrings.DifficultyLevel_Hard, ToolsStrings.DifficultyLevel_Alien
            };

            public AiLevelEntry(int index, int aiLevel) {
                Place = 4 - index;
                AiLevel = aiLevel;
                DisplayName = DisplayNames[index];
            }

            public int Place { get; }

            public int AiLevel { get; }

            public int Id => AiLevel;
        }

        private string _guid;

        [CanBeNull]
        public string Guid {
            get { return _guid; }
            set {
                if (Equals(value, _guid)) return;
                _guid = value;
                OnPropertyChanged();
            }
        }

        public SpecialEventObject(IFileAcManager manager, string id, bool enabled)
                : base(manager, id, enabled) {}

        private AiLevelEntry[] _aiLevels;

        public AiLevelEntry[] AiLevels {
            get { return _aiLevels; }
            set {
                if (Equals(value, _aiLevels)) return;
                _aiLevels = value;
                OnPropertyChanged();
            }
        }

        private AiLevelEntry _selectedLevel;

        public AiLevelEntry SelectedLevel {
            get { return _selectedLevel; }
            set {
                if (Equals(value, _selectedLevel)) return;
                _selectedLevel = value;
                OnPropertyChanged();
                SpecialEventsManager.ProgressStorage.Set(KeySelectedLevel, value.AiLevel);
                AiLevel = value.AiLevel;
            }
        }

        private string KeyTakenPlace => $@"{Id}:TakenPlace";

        private string KeySelectedLevel => $@"{Id}:SelectedLevel";

        private string _displayDescription;

        public string DisplayDescription {
            get { return _displayDescription; }
            set {
                if (Equals(value, _displayDescription)) return;
                _displayDescription = value;
                OnPropertyChanged();
            }
        }

        protected override void LoadObjects() {
            base.LoadObjects();
            DisplayDescription = string.Format(ToolsStrings.SpecialEvent_Description, CarObject?.DisplayName ?? CarId, TrackObject?.Name ?? TrackId);
        }

        protected override void LoadData(IniFile ini) {
            base.LoadData(ini);
            Guid = ini["SPECIAL_EVENT"].GetNonEmpty("GUID");
        }

        protected override void LoadConditions(IniFile ini) {
            if (string.Equals(ini["CONDITION_0"].GetNonEmpty("TYPE"), @"AI", StringComparison.OrdinalIgnoreCase)) {
                var aiLevels = ini.GetSections("CONDITION").Take(4).Select((x, i) => new AiLevelEntry(i, x.GetInt("OBJECTIVE", 100))).Reverse().ToArray();
                if (aiLevels.Length != 4) {
                    AddError(AcErrorType.Data_KunosCareerConditions, $"NOT {aiLevels.Length}");
                    AiLevels = null;
                } else {
                    RemoveError(AcErrorType.Data_KunosCareerConditions);
                    AiLevels = aiLevels;
                }

                ConditionType = null;
                FirstPlaceTarget = SecondPlaceTarget = ThirdPlaceTarget = null;
            } else {
                AiLevels = null;
                base.LoadConditions(ini);
            }
        }

        protected override void TakenPlaceChanged() {
            SpecialEventsManager.ProgressStorage.Set(KeyTakenPlace, TakenPlace);
        }

        public override void LoadProgress() {
            TakenPlace = SpecialEventsManager.ProgressStorage.GetInt(KeyTakenPlace, 5);
            if (AiLevels != null) {
                SelectedLevel = AiLevels.GetByIdOrDefault(SpecialEventsManager.ProgressStorage.GetInt(KeySelectedLevel)) ??
                        AiLevels.ElementAtOrDefault(1);
            }
        }

        protected override IniFile ConvertConfig(IniFile ini) {
            ini = base.ConvertConfig(ini);

            if (SelectedLevel != null) {
                ini["RACE"].Set("AI_LEVEL", SelectedLevel.AiLevel);
            }

            foreach (var section in ini.GetSections("CONDITION")) {
                section.Set("TYPE", section.GetPossiblyEmpty("TYPE")?.ToLowerInvariant());
                section.Set("ACHIEVED", false);
            }

            return ini;
        }

        private static bool ShowStarterDoesNotFitMessage() {
            var dlg = new ModernDialog {
                Title = ToolsStrings.Common_Warning,
                Content = new ScrollViewer {
                    Content = new SelectableBbCodeBlock {
                        BbCode =
                            $"You’re using {SettingsHolder.Drive.SelectedStarterType.DisplayName} Starter. With it, you won’t get a Steam achievment, so progress won’t be saved. Are you sure you want to continue?",
                        Margin = new Thickness(0, 0, 0, 8)
                    },
                    VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                    HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled
                },
                MinHeight = 0,
                MinWidth = 0,
                MaxHeight = 480,
                MaxWidth = 640
            };

            dlg.Buttons = new[] {
                dlg.YesButton,
                dlg.CreateCloseDialogButton("Switch To Official Starter", false, false, MessageBoxResult.OK),
                dlg.NoButton
            };

            dlg.ShowDialog();

            switch (dlg.MessageBoxResult) {
                case MessageBoxResult.Yes:
                    return true;
                case MessageBoxResult.OK:
                    SettingsHolder.Drive.SelectedStarterType = SettingsHolder.Drive.StarterTypes.First();
                    return true;
                case MessageBoxResult.None:
                case MessageBoxResult.Cancel:
                case MessageBoxResult.No:
                    return false;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        private AsyncCommand<Game.AssistsProperties> _goCommand;

        // TODO: async command
        public AsyncCommand<Game.AssistsProperties> GoCommand => _goCommand ?? (_goCommand = new AsyncCommand<Game.AssistsProperties>(async o => {
            if (SettingsHolder.Drive.SelectedStarterType.Id == "SSE" && !ShowStarterDoesNotFitMessage()) {
                return;
            }

            await GameWrapper.StartAsync(new Game.StartProperties {
                AdditionalPropertieses = {
                    ConditionType.HasValue ? new PlaceConditions {
                        Type = ConditionType.Value,
                        FirstPlaceTarget = FirstPlaceTarget,
                        SecondPlaceTarget = SecondPlaceTarget,
                        ThirdPlaceTarget = ThirdPlaceTarget
                    } : null,
                    new SpecialEventsManager.EventProperties { EventId = Id }
                },
                PreparedConfig = ConvertConfig(new IniFile(IniFilename)),
                AssistsProperties = o
            });
        }));
    }
}