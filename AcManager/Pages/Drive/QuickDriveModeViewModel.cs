using System;
using System.Threading.Tasks;
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

        private bool _trackFits = true;

        public bool TrackFits {
            get { return _trackFits; }
            set {
                if (Equals(value, _trackFits)) return;
                _trackFits = value;
                OnPropertyChanged();
            }
        }

        /*// <summary>
        /// Get an additional tab for track’s selection window.
        /// </summary>
        /// <returns>Tab’s name and URI.</returns>
        public virtual Tuple<string, Uri> GetSpecificTrackSelectionPage() {
            return null;
        }*/

        public abstract Task Drive(Game.BasicProperties basicProperties,
                Game.AssistsProperties assistsProperties,
                Game.ConditionProperties conditionProperties, Game.TrackProperties trackProperties);

        protected Task StartAsync(Game.StartProperties properties) {
            return GameWrapper.StartAsync(properties);
        }

        public virtual void OnSelectedUpdated(CarObject selectedCar, TrackObjectBase selectedTrack) {
        }

        public string ToSerializedString() {
            return Saveable.ToSerializedString();
        }

        public void FromSerializedString([NotNull] string data) {
            Saveable.FromSerializedString(data);
        }
    }
}