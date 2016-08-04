using System;
using System.Linq;
using AcTools.Processes;

namespace AcManager.Tools.Data.GameSpecific {
    public class PlaceConditions : Game.AdditionalProperties {
        public PlaceConditionsType Type;
        public int? FirstPlaceTarget, SecondPlaceTarget, ThirdPlaceTarget;

        public override IDisposable Set() {
            /* TODO: add some python app to display conditions in-game? */
            return null;
        }

        public const int UnremarkablePlace = 4;

        public int GetTakenPlace(int value) {
            switch (Type) {
                case PlaceConditionsType.Points:
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

            switch (Type) {
                case PlaceConditionsType.Points:
                    return UnremarkablePlace;
                case PlaceConditionsType.Position:
                    var place = result.Sessions.LastOrDefault(x => x.BestLaps.Any())?.CarPerTakenPlace?.FirstOrDefault();
                    return place.HasValue ? GetTakenPlace(place.Value) : UnremarkablePlace;
                case PlaceConditionsType.Time:
                    var time = result.Sessions.LastOrDefault(x => x.BestLaps.Any())?.BestLaps.FirstOrDefault(x => x.CarNumber == 0)?.Time;
                    return time.HasValue ? GetTakenPlace(time.Value.Milliseconds) : UnremarkablePlace;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }
    }
}