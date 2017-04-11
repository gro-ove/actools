using AcManager.Tools.Objects;
using AcTools.DataFile;

namespace AcManager.ContentRepair.Repairs {
    public class CarAiShiftingPointsRepair : CarSimpleRepairBase {
        public static readonly CarAeroDataRepair Instance = new CarAeroDataRepair();

        protected override void Fix(CarObject car, DataWrapper data) {
            var ai = data.GetIniFile(@"ai.ini");

            var engine = data.GetIniFile(@"engine.ini");
            var rpmMax = engine["ENGINE_DATA"].GetFloat("LIMITER", float.PositiveInfinity);
            var rpmMin = engine["ENGINE_DATA"].GetFloat("MINIMUM", float.NegativeInfinity);

            var aiMax = ai["GEARS"].GetFloat("UP", -1f);
            var aiMin = ai["GEARS"].GetFloat("DOWN", -1f);

            if (aiMax < rpmMin) aiMax = rpmMin + 120f;
            if (aiMax > rpmMax) aiMax = rpmMax - 120f;
            if (aiMin < rpmMin) aiMin = rpmMin + 120f;
            if (aiMin > rpmMax) aiMin = rpmMax - 120f;
            if (aiMin > aiMax) aiMin = aiMax - 500f;

            ai["GEARS"].Set("UP", aiMax);
            ai["GEARS"].Set("DOWN", aiMin);
            ai.Save();
        }

        protected override ContentRepairSuggestion GetObsoletableAspect(CarObject car, DataWrapper data) {
            var ai = car.AcdData?.GetIniFile(@"ai.ini");
            if (ai == null) return null;

            var engine = car.AcdData.GetIniFile(@"engine.ini");
            var rpmMax = engine["ENGINE_DATA"].GetFloat("LIMITER", float.PositiveInfinity);
            var rpmMin = engine["ENGINE_DATA"].GetFloat("MINIMUM", float.NegativeInfinity);

            var aiMax = ai["GEARS"].GetFloat("UP", -1f);
            var aiMin = ai["GEARS"].GetFloat("DOWN", -1f);

            if (rpmMin <= aiMax && aiMax <= rpmMax && rpmMin <= aiMin && aiMin <= rpmMax && aiMax > aiMin) return null;
            return new ContentObsoleteSuggestion("Invalid shifting values in ai.ini", "It might cause AI to stop working",
                    (p, c) => FixAsync(car, p, c)) {
                        AffectsData = true
                    };
        }
    }
}