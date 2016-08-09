using System;
using System.Linq;
using AcManager.Tools.AcManagersNew;
using AcManager.Tools.Data;
using AcManager.Tools.Data.GameSpecific;
using AcManager.Tools.Helpers;
using AcManager.Tools.Managers;
using AcManager.Tools.SemiGui;
using AcTools.DataFile;
using AcTools.Processes;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Presentation;

namespace AcManager.Tools.Objects {
    public class KunosCareerEventObject : KunosEventObjectBase {
        public string KunosCareerId { get; }

        public KunosCareerObjectType KunosCareerType { get; }

        /// <summary>
        /// Starting from 0, like in career.ini!
        /// </summary>
        public int EventNumber { get; }

        public KunosCareerEventObject(string kunosCareerId, KunosCareerObjectType type, IFileAcManager manager, string id, bool enabled)
                : base(manager, id, enabled) {
            KunosCareerId = kunosCareerId;
            EventNumber = FlexibleParser.ParseInt(id.Substring(@"event".Length)) - 1;
            KunosCareerType = type;
        }

        #region From main ini file
        private int _userAiLevel;

        private string UserAiLevelKey => $"KunosCareerEventObject.UserAiLevel_{KunosCareerId}__{Id}";

        public int UserAiLevel {
            get { return _userAiLevel; }
            set {
                if (Equals(value, _userAiLevel)) return;
                _userAiLevel = value;
                OnPropertyChanged();
                ResetUserAiLevelCommand.OnCanExecuteChanged();

                if (value == AiLevel) {
                    ValuesStorage.Remove(UserAiLevelKey);
                } else {
                    ValuesStorage.Set(UserAiLevelKey, value);
                }
            }
        }
        #endregion

        #region Progress values
        private bool _isAvailable;

        public bool IsAvailable {
            get { return _isAvailable; }
            set {
                if (Equals(value, _isAvailable)) return;
                _isAvailable = value;
                OnPropertyChanged();
                GoCommand.OnCanExecuteChanged();
            }
        }

        private bool _isPassed;

        public bool IsPassed {
            get { return _isPassed; }
            set {
                if (Equals(value, _isPassed)) return;
                _isPassed = value;
                OnPropertyChanged();
            }
        }
        #endregion

        protected override void LoadData(IniFile ini) {
            base.LoadData(ini);
            UserAiLevel = ValuesStorage.GetInt(UserAiLevelKey, AiLevel);
        }

        protected override void LoadConditions(IniFile ini) {
            if (KunosCareerType == KunosCareerObjectType.SingleEvents) {
                base.LoadConditions(ini);
            } else {
                ConditionType = null;
                FirstPlaceTarget = SecondPlaceTarget = ThirdPlaceTarget = null;
            }
        }

        public override void LoadProgress() {
            var entry = KunosCareerProgress.Instance.Entries.GetValueOrDefault(KunosCareerId);
            if (entry == null) {
                TakenPlace = 0;
                IsAvailable = KunosCareerType == KunosCareerObjectType.SingleEvents || EventNumber == 0;
                IsPassed = false;
                return;
            }

            var takenPlace = entry.EventsResults.ElementAtOrDefault(EventNumber);
            if (KunosCareerType == KunosCareerObjectType.SingleEvents) {
                if (takenPlace > 0 && takenPlace < 4) {
                    takenPlace = 4 - takenPlace;
                } else {
                    takenPlace = PlaceConditions.UnremarkablePlace;
                }
            }
            TakenPlace = takenPlace;

            if (KunosCareerType == KunosCareerObjectType.SingleEvents) {
                IsAvailable = true;
                IsPassed = false;
            } else {
                IsAvailable = TakenPlace == 0 && entry.SelectedEvent == EventNumber;
                IsPassed = TakenPlace != 0;
            }
        }

        private RelayCommand _resetUserAiLevelCommand;

        public RelayCommand ResetUserAiLevelCommand => _resetUserAiLevelCommand ?? (_resetUserAiLevelCommand = new RelayCommand(o => {
            UserAiLevel = AiLevel;
        }, o => UserAiLevel != AiLevel));

        protected override void SetCustomSkinId(IniFile ini) {
            if (SettingsHolder.Drive.KunosCareerUserSkin) {
                base.SetCustomSkinId(ini);
            }
        }

        protected override IniFile ConvertConfig(IniFile ini) {
            ini = base.ConvertConfig(ini);

            if (SettingsHolder.Drive.KunosCareerUserAiLevel) {
                ini["RACE"].Set("AI_LEVEL", UserAiLevel);
            }

            IniFile opponentsIniFile = null;
            foreach (var i in Enumerable.Range(0, ini["RACE"].GetInt("CARS", 0)).Skip(1)) {
                var sectionKey = @"CAR_" + i;
                if (!ini.ContainsKey(sectionKey) || string.IsNullOrWhiteSpace(ini[sectionKey].GetPossiblyEmpty("DRIVER_NAME"))) {
                    if (opponentsIniFile == null) {
                        var career = KunosCareerManager.Instance.GetById(KunosCareerId);
                        if (career == null) throw new Exception(string.Format("Can’t find parent career with ID={0}", KunosCareerId));

                        opponentsIniFile = new IniFile(career.OpponentsIniFilename);
                        if (opponentsIniFile.IsEmptyOrDamaged()) break;
                    }

                    ini[sectionKey] = opponentsIniFile["AI" + i];
                }

                ini[sectionKey].SetId("MODEL", ini[sectionKey].GetPossiblyEmpty("MODEL"));
                ini[sectionKey].SetId("SKIN", ini[sectionKey].GetPossiblyEmpty("SKIN"));
            }

            return ini;
        }

        private RelayPropertyCommand _goCommand;

        public RelayPropertyCommand GoCommand => _goCommand ?? (_goCommand = new RelayPropertyCommand(async o => {
            await GameWrapper.StartAsync(new Game.StartProperties {
                AdditionalPropertieses = {
                    ConditionType.HasValue ? new PlaceConditions {
                        Type = ConditionType.Value,
                        FirstPlaceTarget = FirstPlaceTarget,
                        SecondPlaceTarget = SecondPlaceTarget,
                        ThirdPlaceTarget = ThirdPlaceTarget
                    } : null,
                    new KunosCareerManager.CareerProperties { CareerId = KunosCareerId, EventId = Id }
                },
                PreparedConfig = ConvertConfig(new IniFile(IniFilename)),
                AssistsProperties = o as Game.AssistsProperties
            });
        }, o => IsAvailable));
    }
}
