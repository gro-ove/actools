using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using AcManager.Tools.Helpers;
using AcManager.Tools.Objects;
using AcTools.DataFile;
using AcTools.Utils;
using AcTools.Utils.Helpers;

namespace AcManager.Tools.WorkshopPublishTools.Validators {
    public class WorkshopCarValidator : WorkshopBaseValidator<CarObject> {
        public WorkshopCarValidator(CarObject obj, bool isChildObject) : base(obj, isChildObject) {
            FlexibleId = false;
            DescriptionRequired = true;
            CountryRequired = true;
            YearRequired = true;
        }

        protected override Tuple<int, int> GetYearBoundaries() {
            return Tuple.Create(1880, 2100);
        }

        protected override string GuessCountry() {
            return base.GuessCountry() ?? (Target.Brand != null ? AcStringValues.CountryFromBrand(Target.Brand) : null);
        }

        private WorkshopValidatedItem TestCarBrand() {
            var originalBrand = Target.Brand;
            if (string.IsNullOrWhiteSpace(originalBrand)) {
                var brandFromName = AcStringValues.BrandFromName(Target.NameEditable ?? "");
                if (brandFromName != null) {
                    return new WorkshopValidatedItem($"Car brand will be “{brandFromName}”",
                            () => Target.Brand = brandFromName, () => Target.Brand = originalBrand);
                }
                return new WorkshopValidatedItem("Car brand is required", WorkshopValidatedState.Failed);
            }

            var trimmed = originalBrand.Trim();
            var knownBrand = FilesStorage.Instance.GetContentFiles(ContentCategory.BrandBadges).Select(x => x.Name).FirstOrDefault(x =>
                    string.Equals(x, trimmed, StringComparison.OrdinalIgnoreCase));
            if (knownBrand != null) {
                return knownBrand == originalBrand
                        ? new WorkshopValidatedItem("Car brand is correct")
                        : new WorkshopValidatedItem($"Car brand might contain a typo, will be changed to {knownBrand}",
                                () => Target.Brand = knownBrand, () => Target.Brand = originalBrand);
            }

            return new WorkshopValidatedItem("Car brand is unknown, car won’t be in any known car brand category", WorkshopValidatedState.Warning);
        }

        private string GuessCarClass() {
            return Target.AcdData?.GetIniFile("tyres.ini")
                    .GetSections("FRONT", -1).Select(x => x.GetNonEmpty("NAME")).NonNull()
                    .Select(x => Regex.IsMatch(x, @"(?<!semi[-\s]?)slick", RegexOptions.IgnoreCase) ? "race"
                            : Regex.IsMatch(x, "street", RegexOptions.IgnoreCase) ? "street" : null).NonNull().FirstOrDefault();
        }

        private WorkshopValidatedItem TestCarClass() {
            var originalCarClass = Target.CarClass;
            var carClass = GuessCarClass();
            if (carClass != null) {
                if (originalCarClass != "race" && originalCarClass != "street") {
                    return new WorkshopValidatedItem("Car class should be either “street” (for road-legal cars) or “race”",
                            () => Target.CarClass = carClass, () => Target.CarClass = originalCarClass);
                }
                if (originalCarClass != carClass) {
                    return new WorkshopValidatedItem($"Judging by data, it seems that car class should be “{carClass}”",
                            () => Target.CarClass = carClass, () => Target.CarClass = originalCarClass);
                }
                return new WorkshopValidatedItem("Car class is correct");
            }

            if (Target.CarClass != "race" && Target.CarClass != "street") {
                return new WorkshopValidatedItem("Car class should be either “street” (for road-legal cars) or “race”",
                        WorkshopValidatedState.Failed);
            }

            return new WorkshopValidatedItem("Car class is correct");
        }

        private WorkshopValidatedItem TestSpec(string specName, string specValue, Func<string> calculateTargetValue, Action<string> targetApplication) {
            var targetValue = calculateTargetValue();
            if (specValue == targetValue) {
                return new WorkshopValidatedItem($"{specName} information is correct");
            }

            return new WorkshopValidatedItem($"{specName} information will be shown as “{targetValue}”",
                    () => targetApplication(targetValue), () => targetApplication(specValue));
        }

        private double? GetSpecsWeight(out bool showRaw) {
            showRaw = Target.SpecsWeight?.IndexOf('*') > 0;
            var actualValue = FlexibleParser.TryParseDouble(Target.SpecsWeight) ?? 0d;
            var dataWeight = Target.AcdData?.GetIniFile("car.ini")["BASIC"].GetDouble("TOTALMASS", 0) ?? 0d;
            if (Target.SpecsWeight?.IndexOf("--") >= 0 || actualValue < 1 && dataWeight < 80) {
                showRaw = false;
                return null;
            }
            if (dataWeight < 80) {
                showRaw = false;
                return actualValue.Round();
            }
            if (showRaw) {
                return dataWeight.Round();
            }
            return (Math.Abs(actualValue - (dataWeight - 75)) <= 50d ? actualValue : dataWeight - 75d).Round();
        }

        private WorkshopValidatedItem TestCarSpecWeight() {
            return TestSpec("Car weight", Target.SpecsWeight, () => {
                var valueToShow = GetSpecsWeight(out var showRaw);
                return valueToShow == null ? "-- kg" : showRaw ? $"{valueToShow} kg*" : $"{valueToShow} kg";
            }, v => Target.SpecsWeight = v);
        }

        private WorkshopValidatedItem TestCarSpecPower() {
            return TestSpec("Car power", Target.SpecsBhp, () => {
                var actualValue = FlexibleParser.TryParseDouble(Target.SpecsBhp) ?? 0d;
                if (Target.SpecsBhp?.IndexOf("--") >= 0 || actualValue < 0.1) {
                    return "-- bhp";
                }

                var showRaw = Target.SpecsBhp?.IndexOf('*') > 0 || Regex.IsMatch(Target.SpecsBhp ?? "", @"\bw?hp\b");
                return showRaw ? $"{actualValue.Round()} whp" : $"{actualValue.Round()} bhp";
            }, v => Target.SpecsBhp = v);
        }

        private WorkshopValidatedItem TestCarSpecTorque() {
            return TestSpec("Car torque", Target.SpecsTorque, () => {
                var actualValue = FlexibleParser.TryParseDouble(Target.SpecsTorque) ?? 0d;
                var showRaw = Target.SpecsBhp?.IndexOf('*') > 0;
                return Target.SpecsTorque?.IndexOf("--") >= 0 || actualValue < 0.1 ? "-- Nm"
                        : showRaw ? $"{actualValue.Round()} Nm*" : $"{actualValue.Round()} Nm";
            }, v => Target.SpecsTorque = v);
        }

        private WorkshopValidatedItem TestCarSpecTopSpeed() {
            return TestSpec("Car top speed", Target.SpecsTopSpeed, () => {
                var actualValue = FlexibleParser.TryParseDouble(Target.SpecsTopSpeed) ?? 0d;
                return Target.SpecsTopSpeed?.IndexOf("--") >= 0 || actualValue < 0.1 ? "-- km/h" : $"{actualValue.Round()} km/h";
            }, v => Target.SpecsTopSpeed = v);
        }

        private WorkshopValidatedItem TestCarSpecAcceleration() {
            return TestSpec("Car top speed", Target.SpecsAcceleration, () => {
                var actualValue = FlexibleParser.TryParseDouble(Target.SpecsAcceleration) ?? 0d;
                return Target.SpecsAcceleration?.IndexOf("--") >= 0 || actualValue < 0.1 ? "-- s 0–100" : $"{actualValue.Round(0.1)} s 0–100";
            }, v => Target.SpecsAcceleration = v);
        }

        private WorkshopValidatedItem TestCarSpecPwRatio() {
            return TestSpec("Car P/W ratio", Target.SpecsPwRatio, () => {
                var carPower = FlexibleParser.TryParseDouble(Target.SpecsBhp) ?? 0d;
                var carWeight = GetSpecsWeight(out _);
                return carPower < 0.01 || carWeight == null ? "-- kg/hp" : $"{(carWeight.Value / carPower).ToString("F2", CultureInfo.InvariantCulture)} kg/hp";
            }, v => Target.SpecsPwRatio = v);
        }

        private static List<string> _prohibitedTags = new List<string> {
            "rwd",
            "fwd",
            "awd",
            "street",
            "race",
            "turbo",
            "manual",
            "open wheeler",
        };

        protected override bool IsTagAllowed(string tag) {
            if (tag.StartsWith("#")) return tag.Length > 3 || tag.Length > 1 && tag[1] != 'A';
            if (tag == "vintage" && Target.Year > 2005) return false;
            if (_prohibitedTags.Contains(tag)) return false;
            if (AcStringValues.CountryFromTag(tag) != null) return false;
            return base.IsTagAllowed(tag);
        }

        protected override IEnumerable<string> GetForcedTags() {
            var drivetrainIni = Target.AcdData?.GetIniFile("drivetrain.ini");
            var tractionType = drivetrainIni?["TRACTION"].GetNonEmpty("TYPE");
            if (tractionType == "RWD") yield return "rwd";
            else if (tractionType == "FWD") yield return "fwd";
            else if (tractionType == "AWD") yield return "awd";

            var gearsCount = drivetrainIni?["GEARS"].GetIntNullable("COUNT");
            var supportsShifter = drivetrainIni?["GEARBOX"].GetBoolNullable("SUPPORTS_SHIFTER");
            if (gearsCount > 1 && supportsShifter.HasValue) {
                yield return supportsShifter.Value ? "manual" : "semiautomatic";
            }

            if (Target.CarClass == "street") yield return "street";
            else if (Target.CarClass == "race") yield return "race";
            else yield return GuessCarClass();

            yield return Target.Country != null ? AcStringValues.CountryFromTag(Target.Country) : GuessCountry();

            if (Target.AcdData?.GetIniFile("engine.ini")["TURBO_0"].ContainsKey("MAX_BOOST") == true) {
                yield return "turbo";
            }

            if (Target.Year < 1990) {
                yield return "vintage";
            }
        }

        private WorkshopValidatedItem ValidateAudioGuids() {
            var fileName = "sfx/GUIDs.txt";
            var fileInfo = new FileInfo(Path.Combine(Target.Location, fileName));
            return !fileInfo.Exists
                    ? Target.Author == "Kunos"
                            ? new WorkshopValidatedItem($"File “{fileName}” is not required")
                            : new WorkshopValidatedItem($"File “{fileName}” is required", WorkshopValidatedState.Failed)
                    : fileInfo.Length > 100e3
                            ? new WorkshopValidatedItem($"File “{fileName}” is too large", WorkshopValidatedState.Failed)
                            : Target.SoundDonorId == @"tatuusfa1"
                                    ? new WorkshopValidatedItem("Soundbank GUIDs might cause conflict", WorkshopValidatedState.Warning)
                                    : new WorkshopValidatedItem($"File “{fileName}” exists");
        }

        public override IEnumerable<WorkshopValidatedItem> Validate() {
            foreach (var item in base.Validate()) {
                yield return item;
            }

            yield return TestCarBrand();
            yield return TestCarClass();
            yield return TestCarSpecWeight();
            yield return TestCarSpecPower();
            yield return TestCarSpecTorque();
            yield return TestCarSpecTopSpeed();
            yield return TestCarSpecAcceleration();
            yield return TestCarSpecPwRatio();
            yield return ValidateAudioGuids();

            yield return ValidateFileExistance("logo.png", 100e3);
            yield return ValidateFileExistance("body_shadow.png", 500e3);
            yield return ValidateFileExistance("tyre_0_shadow.png", 100e3);
            yield return ValidateFileExistance("tyre_1_shadow.png", 100e3);
            yield return ValidateFileExistance("tyre_2_shadow.png", 100e3);
            yield return ValidateFileExistance("tyre_3_shadow.png", 100e3);
            yield return ValidateFileExistance("collider.kn5", 100e3);
            yield return ValidateFileExistance("driver_base_pos.knh", 100e3);
            yield return ValidateFileExistance("ui/badge.png", 100e3);
            yield return ValidateFileExistance($"sfx/{Target.Id}.bank", 200e6);
        }
    }
}