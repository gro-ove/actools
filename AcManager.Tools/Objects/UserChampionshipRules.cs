using System;
using AcTools.Processes;
using AcTools.Utils;
using FirstFloor.ModernUI.Presentation;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace AcManager.Tools.Objects {
    public class UserChampionshipRules : NotifyPropertyChanged {
        private int _practice = 90;

        /// <summary>
        /// Duration in minutes.
        /// </summary>
        [JsonProperty(@"practice")]
        public int Practice {
            get { return _practice; }
            set {
                value = value.Clamp(0, 3000);
                if (Equals(value, _practice)) return;
                _practice = value;
                OnPropertyChanged();
            }
        }

        private int _qualifying = 60;

        /// <summary>
        /// Duration in minutes.
        /// </summary>
        [JsonProperty(@"qualifying")]
        public int Qualifying {
            get { return _qualifying; }
            set {
                value = value.Clamp(0, 3000);
                if (Equals(value, _qualifying)) return;
                _qualifying = value;
                OnPropertyChanged();
            }
        }

        private int[] _points = { 10, 8, 6, 3, 2, 1 };

        [JsonProperty(@"points")]
        public int[] Points {
            get { return _points; }
            set {
                if (Equals(value, _points)) return;
                _points = value;
                OnPropertyChanged();
            }
        }

        private bool _penalties = true;

        [JsonProperty(@"penalties")]
        public bool Penalties {
            get { return _penalties; }
            set {
                if (Equals(value, _penalties)) return;
                _penalties = value;
                OnPropertyChanged();
            }
        }

        private Game.JumpStartPenaltyType _jumpStartPenalty = Game.JumpStartPenaltyType.DriveThrough;

        [JsonProperty(@"jumpstart")]
        public Game.JumpStartPenaltyType JumpStartPenalty {
            get { return _jumpStartPenalty; }
            set {
                if (Equals(value, _jumpStartPenalty)) return;
                _jumpStartPenalty = value;
                OnPropertyChanged();
            }
        }

        public void SaveTo(JObject obj) {
            foreach (var pair in JObject.FromObject(this)) {
                obj[pair.Key] = pair.Value;
            }
        }
    }
}