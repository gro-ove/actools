using System;
using System.ComponentModel;
using System.Threading.Tasks;
using System.Windows;
using AcManager.Controls.ViewModels;
using AcManager.Tools;
using AcManager.Tools.Helpers;
using AcManager.Tools.Objects;
using AcManager.Tools.SemiGui;
using AcTools.Processes;
using FirstFloor.ModernUI.Presentation;
using JetBrains.Annotations;

namespace AcManager.Pages.Drive {
    public abstract class QuickDriveModeViewModel : NotifyPropertyChanged {
        protected ISaveHelper Saveable { set; get; }


        public event EventHandler Changed;

        protected void SaveLater() {
            Saveable.SaveLater();
            Changed?.Invoke(this, new EventArgs());
        }

        private Tuple<string, Action<TrackObjectBase>> _trackDoesNotFit;

        /// <summary>
        /// If not null, this Tuple should contain a description why track does not fit and a solution.
        /// </summary>
        [CanBeNull]
        public Tuple<string, Action<TrackObjectBase>> TrackDoesNotFit {
            get { return _trackDoesNotFit; }
            set {
                if (Equals(value, _trackDoesNotFit)) return;
                _trackDoesNotFit = value;
                OnPropertyChanged();
            }
        }

        [CanBeNull]
        protected Tuple<string, Action<TrackObjectBase>> TagRequired([Localizable(false),NotNull] string tag, [CanBeNull] TrackObjectBase track) {
            return track?.Tags.ContainsIgnoringCase(tag) != false ? null :
                        new Tuple<string, Action<TrackObjectBase>>(
                                string.Format(ToolsStrings.TagIsMissing_Format, tag),
                                t => t.Tags.Add(tag));
        }

        public abstract Task Drive(Game.BasicProperties basicProperties,
                Game.AssistsProperties assistsProperties,
                Game.ConditionProperties conditionProperties, Game.TrackProperties trackProperties);

        protected Task StartAsync(Game.StartProperties properties) {
            return GameWrapper.StartAsync(properties);
        }

        private TrackObjectBase _track;

        public virtual void CheckIfTrackFits([CanBeNull] TrackObjectBase track) {
            TrackDoesNotFit = null;
        }

        public virtual void OnSelectedUpdated(CarObject selectedCar, TrackObjectBase selectedTrack) {
            if (_track != null) {
                WeakEventManager<INotifyPropertyChanged, PropertyChangedEventArgs>.RemoveHandler(_track, nameof(INotifyPropertyChanged.PropertyChanged),
                        OnTrackPropertyChanged);
            }

            CheckIfTrackFits(selectedTrack);
            _track = selectedTrack;

            if (_track != null) {
                WeakEventManager<INotifyPropertyChanged, PropertyChangedEventArgs>.AddHandler(_track, nameof(INotifyPropertyChanged.PropertyChanged),
                        OnTrackPropertyChanged);
            }
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
    }
}