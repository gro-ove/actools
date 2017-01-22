using System;
using System.Linq;
using AcTools.Processes;
using AcTools.Utils.Helpers;

namespace AcManager.Tools.Data.GameSpecific {
    public class PlaceConditions : Game.AdditionalProperties {
        public PlaceConditionsType Type;
        public int? FirstPlaceTarget, SecondPlaceTarget, ThirdPlaceTarget;

        public override IDisposable Set() {
            /* TODO: add some python app to display conditions in-game? */
            return null;
        }

        public const int UnremarkablePlace = 4;

        /// <summary>
        /// For some of place condition types, better is more, for others — fewer (less).
        /// </summary>
        private int GetTakenPlace(int value) {
            switch (Type) {
                case PlaceConditionsType.Points:
                case PlaceConditionsType.Wins:
                    return FirstPlaceTarget.HasValue && value >= FirstPlaceTarget ? 1 :
                            SecondPlaceTarget.HasValue && value >= SecondPlaceTarget ? 2 :
                                    ThirdPlaceTarget.HasValue && value >= ThirdPlaceTarget ? 3 :
                                            UnremarkablePlace;
                case PlaceConditionsType.Position:
                case PlaceConditionsType.Time:
                    return FirstPlaceTarget.HasValue && value <= FirstPlaceTarget ? 1 :
                            SecondPlaceTarget.HasValue && value <= SecondPlaceTarget ? 2 :
                                    ThirdPlaceTarget.HasValue && value <= ThirdPlaceTarget ? 3 :
                                            UnremarkablePlace;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        public int GetTakenPlace(Game.Result result) {
            if (result == null) return UnremarkablePlace;

            var drift = result.GetExtraByType<Game.ResultExtraDrift>();
            if (drift != null && Type == PlaceConditionsType.Points) {
                return GetTakenPlace(drift.Points);
            }

            var timeAttack = result.GetExtraByType<Game.ResultExtraTimeAttack>();
            if (timeAttack != null && Type == PlaceConditionsType.Points) {
                return GetTakenPlace(timeAttack.Points);
            }

            var drag = result.GetExtraByType<Game.ResultExtraDrag>();
            if (drag != null && Type == PlaceConditionsType.Wins) {
                return GetTakenPlace(drag.Wins);
            }

            switch (Type) {
                case PlaceConditionsType.Points:
                case PlaceConditionsType.Wins:
                    return UnremarkablePlace;
                case PlaceConditionsType.Position:
                    var place = result.Sessions.LastOrDefault(x => x.BestLaps.Any())?.CarPerTakenPlace?.IndexOf(0);
                    return place.HasValue ? GetTakenPlace(place.Value + 1) : UnremarkablePlace;
                case PlaceConditionsType.Time:
                    var time = result.Sessions.LastOrDefault(x => x.BestLaps.Any())?.BestLaps.FirstOrDefault(x => x.CarNumber == 0)?.Time;
                    return time.HasValue ? GetTakenPlace((int)time.Value.TotalMilliseconds) : UnremarkablePlace;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        public string GetDescription() {
            return $@"({Type}, Targets=[{FirstPlaceTarget}, {SecondPlaceTarget}, {ThirdPlaceTarget}])";
        }
    }
}