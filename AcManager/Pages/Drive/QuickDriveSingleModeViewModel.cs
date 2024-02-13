using AcManager.Tools.Helpers;
using JetBrains.Annotations;

namespace AcManager.Pages.Drive {
    public abstract class QuickDriveSingleModeViewModel : QuickDriveModeViewModel {
        protected void Initialize(string key, bool initialize) {
            Saveable = CreateSaveable(key);
            if (initialize) {
                Saveable.Initialize();
            } else {
                Saveable.Reset();
            }
        }

        [NotNull]
        protected virtual ISaveHelper CreateSaveable(string key) {
            return new SaveHelper<SaveableData>(key, () => Save(new SaveableData()), Load);
        }

        private bool _penalties;

        public bool Penalties {
            get => _penalties;
            set {
                if (value == _penalties) return;
                _penalties = value;
                OnPropertyChanged();
                SaveLater();
            }
        }

        private int _playerBallast;

        public int PlayerBallast {
            get => _playerBallast;
            set {
                if (Equals(value, _playerBallast)) return;
                _playerBallast = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(HasAnyRestrictions));
                SaveLater();
            }
        }

        private int _playerRestrictor;

        public int PlayerRestrictor {
            get => _playerRestrictor;
            set {
                if (Equals(value, _playerRestrictor)) return;
                _playerRestrictor = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(HasAnyRestrictions));
                SaveLater();
            }
        }

        public class SaveableData {
            public bool Penalties = true;
            public int PlayerBallast, PlayerRestrictor;
        }

        protected SaveableData Save(SaveableData data) {
            data.Penalties = Penalties;
            data.PlayerBallast = PlayerBallast;
            data.PlayerRestrictor = PlayerRestrictor;
            return data;
        }

        protected void Load(SaveableData data) {
            Penalties = data.Penalties;
            PlayerBallast = data.PlayerBallast;
            PlayerRestrictor = data.PlayerRestrictor;
        }

        public override bool HasAnyRestrictions => PlayerRestrictor != 0 || PlayerBallast != 0;
    }
}