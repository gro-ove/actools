using System;
using AcManager.Tools.AcManagersNew;
using AcManager.Tools.Data.GameSpecific;
using AcManager.Tools.Managers;
using AcManager.Tools.SemiGui;
using AcTools.DataFile;
using AcTools.Processes;
using AcTools.Utils;
using FirstFloor.ModernUI.Presentation;

namespace AcManager.Tools.Objects {
    public class SpecialEventObject : KunosEventObjectBase {
        public SpecialEventObject(IFileAcManager manager, string id, bool enabled)
                : base(manager, id, enabled) {}

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
            DisplayDescription = string.Format("{0} at {1}.", CarObject?.DisplayName ?? CarId, TrackObject?.Name ?? TrackId);
        }

        protected override void LoadConditions(IniFile ini) {
            if (string.Equals(ini["CONDITION_0"].Get("TYPE"), @"AI", StringComparison.OrdinalIgnoreCase)) {
                ConditionType = null;
                FirstPlaceTarget = SecondPlaceTarget = ThirdPlaceTarget = null;
            } else {
                base.LoadConditions(ini);
            }
        }

        public override void LoadProgress() {
            TakenPlace = 4;
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
                    // new KunosCareerManager.CareerProperties { CareerId = KunosCareerId, EventId = Id }
                },
                PreparedConfig = ConvertConfig(new IniFile(IniFilename)),
                AssistsProperties = o as Game.AssistsProperties
            });
        }));
    }
}