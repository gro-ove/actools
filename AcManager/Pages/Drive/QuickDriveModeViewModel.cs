using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Threading.Tasks;
using System.Windows;
using AcManager.Tools;
using AcManager.Tools.Helpers;
using AcManager.Tools.Objects;
using AcManager.Tools.SemiGui;
using AcTools.Processes;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Presentation;
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
    }

    public interface IRaceGridModeViewModel {
        void SetRaceGridData([NotNull] string serializedRaceGrid);
    }

    public abstract class QuickDriveModeViewModel : NotifyPropertyChanged {
        public static Action<CarObject> EmptyCarAction = a => { };
        public static Action<TrackObjectBase> EmptyTrackAction = a => { };

        protected ISaveHelper Saveable { set; get; }

        public event EventHandler Changed;

        protected void SaveLater() {
            // Sometimes Saveable might not be yet created, for example when populating defaults during initialization of a custom mode
            if (Saveable?.SaveLater() == true) {
                Changed?.Invoke(this, EventArgs.Empty);
            }
        }

        private Tuple<string, Action<CarObject>> _carDoesNotFit;

        /// <summary>
        /// If not null, this Tuple should contain a description why track does not fit and a solution.
        /// </summary>
        [CanBeNull]
        public Tuple<string, Action<CarObject>> CarDoesNotFit {
            get => _carDoesNotFit;
            set => Apply(value, ref _carDoesNotFit, () => OnPropertyChanged(nameof(CarOrTrackDoesNotFit)));
        }

        private Tuple<string, Action<TrackObjectBase>> _trackDoesNotFit;

        /// <summary>
        /// If not null, this Tuple should contain a description why track does not fit and a solution.
        /// </summary>
        [CanBeNull]
        public Tuple<string, Action<TrackObjectBase>> TrackDoesNotFit {
            get => _trackDoesNotFit;
            set => Apply(value, ref _trackDoesNotFit, () => OnPropertyChanged(nameof(CarOrTrackDoesNotFit)));
        }

        public bool CarOrTrackDoesNotFit => _carDoesNotFit != null || _trackDoesNotFit != null;

        [CanBeNull]
        protected Tuple<string, Action<TrackObjectBase>> TagRequired([Localizable(false), NotNull] string tag, [CanBeNull] TrackObjectBase track) {
            return track?.Tags.ContainsIgnoringCase(tag) != false ? null :
                        new Tuple<string, Action<TrackObjectBase>>(
                                string.Format(ToolsStrings.TagIsMissing_Format, tag),
                                t => t.Tags.Add(tag));
        }

        public abstract Task Drive(Game.BasicProperties basicProperties, Game.AssistsProperties assistsProperties,
                Game.ConditionProperties conditionProperties, Game.TrackProperties trackProperties,
                string serializedQuickDrivePreset, IList<object> additionalProperties);

        protected Task StartAsync(Game.StartProperties properties) {
            Logging.Here();
            return GameWrapper.StartAsync(properties);
        }

        private CarObject _car;
        private TrackObjectBase _track;

        protected virtual void CheckIfCarFits([CanBeNull] CarObject track) {
            CarDoesNotFit = null;
        }

        protected virtual void CheckIfTrackFits([CanBeNull] TrackObjectBase track) {
            TrackDoesNotFit = null;
        }

        public virtual void OnSelectedUpdated(CarObject selectedCar, TrackObjectBase selectedTrack) {
            if (_car != null) {
                WeakEventManager<INotifyPropertyChanged, PropertyChangedEventArgs>.RemoveHandler(_car, nameof(INotifyPropertyChanged.PropertyChanged),
                        OnCarPropertyChanged);
            }
            if (_track != null) {
                WeakEventManager<INotifyPropertyChanged, PropertyChangedEventArgs>.RemoveHandler(_track, nameof(INotifyPropertyChanged.PropertyChanged),
                        OnTrackPropertyChanged);
            }

            CheckIfCarFits(selectedCar);
            CheckIfTrackFits(selectedTrack);
            _car = selectedCar;
            _track = selectedTrack;

            if (_car != null) {
                WeakEventManager<INotifyPropertyChanged, PropertyChangedEventArgs>.AddHandler(_car, nameof(INotifyPropertyChanged.PropertyChanged),
                        OnCarPropertyChanged);
            }
            if (_track != null) {
                WeakEventManager<INotifyPropertyChanged, PropertyChangedEventArgs>.AddHandler(_track, nameof(INotifyPropertyChanged.PropertyChanged),
                        OnTrackPropertyChanged);
            }
        }

        private void OnCarPropertyChanged(object sender, PropertyChangedEventArgs e) {
            CheckIfCarFits(_car);
        }

        private void OnTrackPropertyChanged(object sender, PropertyChangedEventArgs e) {
            CheckIfTrackFits(_track);
        }

        public string ToSerializedString() {
            return Saveable.ToSerializedString();
        }

        public void FromSerializedString([NotNull] string data) {
            Saveable.FromSerializedString(data);
        }

        [CanBeNull]
        public virtual Tuple<string, string> GetDefaultCarFilter() {
            return null; 
        }

        [CanBeNull]
        public virtual Tuple<string, string> GetDefaultTrackFilter() {
            return null; 
        }
    }
}