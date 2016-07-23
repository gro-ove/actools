using System;
using System.Threading.Tasks;
using AcManager.Tools.Helpers;
using AcManager.Tools.Objects;
using AcManager.Tools.SemiGui;
using AcTools.Processes;
using FirstFloor.ModernUI.Presentation;

namespace AcManager.Pages.Drive {
    public abstract class QuickDriveModeViewModel : NotifyPropertyChanged {
        protected ISaveHelper Saveable { set; get; }


        public event EventHandler Changed;

        protected void SaveLater() {
            Saveable.SaveLater();
            Changed?.Invoke(this, new EventArgs());
        }

        public abstract Task Drive(Game.BasicProperties basicProperties,
                Game.AssistsProperties assistsProperties,
                Game.ConditionProperties conditionProperties, Game.TrackProperties trackProperties);

        protected Task StartAsync(Game.StartProperties properties) {
            return GameWrapper.StartAsync(properties);
        }

        public virtual void OnSelectedUpdated(CarObject selectedCar, TrackBaseObject selectedTrack) {
        }

        public string ToSerializedString() {
            return Saveable.ToSerializedString();
        }

        public void FromSerializedString(string data) {
            Saveable.FromSerializedString(data);
        }
    }
}