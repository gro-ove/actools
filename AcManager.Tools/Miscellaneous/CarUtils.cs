using System.Linq;
using System.Text.RegularExpressions;
using AcManager.Tools.Objects;
using AcTools.DataFile;
using AcTools.Kn5File;
using AcTools.Utils;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI.Helpers;

namespace AcManager.Tools.Miscellaneous {
    public static class CarUtils {
        private enum GuessedCarClass {
            Street, Race
        }

        private static string TryToGuessCarClassResult(CarObject car, GuessedCarClass guessed, string reason){
            Logging.Debug($"{car.DisplayName} = {guessed}: {reason}");
            return guessed == GuessedCarClass.Street ? "street" : "race";
        }

        private static readonly Regex RaceRegex = new Regex(@"(?:\b|_)slick|\b(?:soft|hard)\b",
                RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static readonly Regex StreetRegex = new Regex(@"semislick|street|\b(?:road)\b",
                RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static readonly Regex DriftRegex = new Regex(@"drift",
                RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static readonly Regex DriftSpecyRegex = new Regex(@"\(.*\d{3}\)$|\d{3}/\d{3}",
                RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static readonly Regex RaceBackupRegex = new Regex(@"\b(?:vintage|formula truck)\b|^g[pt]\d+$",
                RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static readonly Regex StreetBackupRegex = new Regex(@"\b(?:cinturato)\b",
                RegexOptions.Compiled | RegexOptions.IgnoreCase);

        // Trained on Kunos cars
        public static string TryToGuessCarClass(CarObject car) {
            var tyresIni = car.AcdData?.GetIniFile("tyres.ini");
            if (tyresIni == null) return null;

            var tyres = tyresIni.GetSections("FRONT", -1).Concat(tyresIni.GetSections("REAR", -1))
                                .Select(x => x.GetPossiblyEmpty("NAME")).NonNull().Distinct().ToList();
            if (tyres.Count == 0) return null;

            // Suspension stiffness
            var suspensionIni = car.AcdData.GetIniFile("suspensions.ini");
            var weight = car.AcdData.GetIniFile("car.ini")["BASIC"].GetFloat("TOTALMASS", 0f);
            var cg = suspensionIni["BASIC"].GetFloat("CG_LOCATION", 0.5f);
            var stiffness = suspensionIni["FRONT"].GetFloat("SPRING_RATE", 0f) /
                    (weight * cg - suspensionIni["FRONT"].GetFloat("HUB_MASS", 90));

            // DX ref value
            var minDxRef = tyresIni.GetSections("FRONT", -1).Concat(tyresIni.GetSections("REAR", -1)).Select(x => x.GetFloat("DX_REF", 100f)).MinOrDefault();

            // Special case for Honda S800
            if (stiffness > 0f && stiffness < 50f){
                return TryToGuessCarClassResult(car, GuessedCarClass.Street, "weak suspension: " + stiffness);
            }

            // Special case for drift cars
            if (tyres.Any(tyre => DriftSpecyRegex.IsMatch(tyre))) {
                return TryToGuessCarClassResult(car, GuessedCarClass.Street, "specy drift tyres");
            }

            // At first, check some valid marks of street tyre
            if (tyres.Any(tyre => StreetRegex.IsMatch(tyre))) {
                return TryToGuessCarClassResult(car, GuessedCarClass.Street, "sure street mark in tyres");
            }

            // Then, some valid marks of race tyre (this way, race+street tyres car will be
            // considered street car)
            if (tyres.Any(tyre => RaceRegex.IsMatch(tyre))) {
                return TryToGuessCarClassResult(car, GuessedCarClass.Race, $"sure race mark in tyres [{stiffness}, {minDxRef}]");
            }

            // Nothing? Well, let’s check using more wobbly marks
            if (tyres.Any(tyre => StreetBackupRegex.IsMatch(tyre))) {
                return TryToGuessCarClassResult(car, GuessedCarClass.Street, "likely street mark in tyres");
            }

            if (tyres.Any(tyre => RaceBackupRegex.IsMatch(tyre))) {
                return TryToGuessCarClassResult(car, GuessedCarClass.Race, $"likely race mark in tyres [{stiffness}, {minDxRef}]");
            }

            // Maybe there are postfixes?
            if (new[]{ @"\bSS$", @"\bS$", @"\bM$", @"\bH$" }.Count(x => tyres.Any(y => Regex.IsMatch(y, x))) > 2){
                return TryToGuessCarClassResult(car, GuessedCarClass.Race, "postfixes");
            }

            // Check for license plate
            var textureNames = Kn5.FromFile(AcPaths.GetMainCarFilename(car.Location, car.AcdData),
                    SkippingTextureLoader.Instance, SkippingMaterialLoader.Instance, SkippingNodeLoader.Instance).TexturesData.Keys.ToList();
            if (textureNames.Contains("Plate_D.dds") || textureNames.Contains("plate.dds")){
                return TryToGuessCarClassResult(car, GuessedCarClass.Street, "license plate");
            }

            // Drift cars are considered street-legal: for instance, D1 only allows street-legal cars to compete
            if (tyres.Any(tyre => DriftRegex.IsMatch(tyre))) {
                return TryToGuessCarClassResult(car, GuessedCarClass.Street, "drift car");
            }

            // Suspension is too stiff for car’s weight? Might be a race car
            if (stiffness >= 125f){
                return TryToGuessCarClassResult(car, GuessedCarClass.Race, "stiff suspension: " + stiffness);
            }

            // Street-legal cars should have at least one headlight or brake light
            var lightsIni = car.AcdData.GetIniFile("lights.ini");
            if (!lightsIni.GetSections("BRAKE").Any(x => x.GetVector3("COLOR").Any(y => y > 5f)) ||
                    !lightsIni.GetSections("LIGHT").Any(x => x.GetVector3("COLOR").Any(y => y > 5f))){
                return TryToGuessCarClassResult(car, GuessedCarClass.Race, "lights are missing");
            }

            // DX ref?
            if (minDxRef != 0f && minDxRef != 100f){
                return TryToGuessCarClassResult(car, minDxRef < 1.54f ? GuessedCarClass.Street : GuessedCarClass.Race, "dx ref " + minDxRef);
            }

            return TryToGuessCarClassResult(car, GuessedCarClass.Street, $"nothing left: {stiffness}, {minDxRef}");
        }
    }
}