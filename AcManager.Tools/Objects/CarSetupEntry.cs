using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using AcManager.Tools.Helpers;
using AcTools.DataFile;
using AcTools.Utils;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI.Commands;
using FirstFloor.ModernUI.Presentation;
using JetBrains.Annotations;

namespace AcManager.Tools.Objects {
    public sealed class CarSetupEntry : Displayable {
        [Localizable(false)]
        public string Key { get; }

        [Localizable(false)]
        public string TabKey { get; }

        [CanBeNull]
        public string HelpInformation { get; }

        public string UnitsPostfix { get; }

        public double Minimum { get; }
        public double Maximum { get; }
        public double Step { get; }

        public CarSetupStepsMode StepsMode { get; }

        public static string GetUnitsPostfix(string key) {
            switch (key) {
                case "DIFF_POWER":
                case "DIFF_COAST":
                case "FRONT_DIFF_COAST":
                case "FRONT_DIFF_POWER":
                case "REAR_DIFF_COAST":
                case "REAR_DIFF_POWER":
                case "CENTER_DIFF_PRELOAD":
                case "AWD_FRONT_TORQUE_DISTRIBUTION":
                case "ENGINE_LIMITER":
                case "FRONT_BIAS":
                case "BRAKE_POWER_MULT":
                    return "%";
            }

            switch (key.Split('_')[0]) {
                case "PRESSURE":
                    return ContentUtils.GetString("ControlsStrings", "Common_PsiPostfix");
                case "FUEL":
                    return " L";
                case "TOE":
                case "CAMBER":
                case "WING":
                case "PACKER":
                    return " mm";
                default:
                    return null;
            }
        }

        private double? GetDefaultValue(string key, DataWrapper data) {
            switch (key) {
                case "DIFF_POWER":
                    return data.GetIniFile("drivetrain.ini")["DIFFERENTIAL"].GetDouble("POWER", 0.5) * 100d;
                case "DIFF_COAST":
                    return data.GetIniFile("drivetrain.ini")["DIFFERENTIAL"].GetDouble("COAST", 0.5) * 100d;
                case "FINAL_GEAR_RATIO":
                    return data.GetIniFile("drivetrain.ini")["GEARS"].GetDouble("FINAL", 3.0);
                case "FUEL":
                    return data.GetIniFile("car.ini")["FUEL"].GetDouble("FUEL", 20);
                case "PRESSURE_LF":
                case "PRESSURE_RF":
                    return data.GetIniFile("tyres.ini")["FRONT"].GetDouble("PRESSURE_STATIC", 20);
                case "PRESSURE_LR":
                case "PRESSURE_RR":
                    return data.GetIniFile("tyres.ini")["REAR"].GetDouble("PRESSURE_STATIC", 20);
            }

            /*if (key.StartsWith("INTERNAL_GEAR_")) {
                    var gearKey = SavedKeyToSetupKey(key);
                    var value = gearKey == null ? null : data.GetIniFile("drivetrain.ini")["GEARS"].GetDoubleNullable(gearKey);
                    if (value == null) return null;

                    return Values.FindIndex(x => x.Value == value);
                }*/

            switch (key.Split('_')[0]) {
                case "GEAR":
                    var value = data.GetIniFile("drivetrain.ini")["GEARS"].GetDoubleNullable(key);
                    return value == null ? null : Values?.FindIndex(x => x.Value == value);

                default:
                    return null;
            }
        }

        private static double? FixedStep(string key) {
            switch (key.Split('_')[0]) {
                case "CAMBER":
                    return 0.1d;
                default:
                    return null;
            }
        }

        public CarSetupEntry([Localizable(false), NotNull] string key, [CanBeNull] AcLocaleProvider localeProvider, [NotNull] DataWrapper data)
                : this(key, data.GetIniFile("setup.ini")[key], localeProvider, data) { }

        public CarSetupEntry([NotNull] string key, IniFileSection section, [CanBeNull] AcLocaleProvider localeProvider, [NotNull] DataWrapper data) {
            Key = key;
            DisplayName = localeProvider?.GetString("SETUP", section.GetNonEmpty("NAME"))
                    ?? CarSetupObject.FixEntryName(section.GetNonEmpty("NAME"), false) ?? key;
            HelpInformation = localeProvider?.GetString(AcLocaleProvider.CategoryTag, section.GetNonEmpty("HELP")) ?? section.GetNonEmpty("HELP");

            var ratios = section.GetNonEmpty("RATIOS");
            if (ratios != null) {
                Values = data.GetRtoFile(ratios).Values;
                Minimum = 0;
                Maximum = Values.Count - 1;
                Step = 1;
                StepsMode = CarSetupStepsMode.Steps;
                TabKey = "GEARS";
            } else {
                Minimum = section.GetDouble("MIN", 0);
                Maximum = section.GetDouble("MAX", Minimum + 100);
                Step = FixedStep(key) ?? section.GetDouble("STEP", 1d);
                StepsMode = section.GetIntEnum("SHOW_CLICKS", CarSetupStepsMode.ActualValue);
                UnitsPostfix = StepsMode == CarSetupStepsMode.ActualValue ? GetUnitsPostfix(key) : null;
                TabKey = section.GetNonEmpty("TAB");
            }

            var defaultValue = GetDefaultValue(key, data);
            DefaultValue = defaultValue ?? (Minimum + Maximum) / 2f;
            HasDefaultValue = defaultValue.HasValue;

            var range = Maximum - Minimum;
            VisualStep = range / Step < 10 ? Step : range / 10;
        }

        private double? _value;

        public double Value {
            get => _value ?? 0d;
            set => Apply(value, ref _value, () => {
                if (Values != null) {
                    ValuePair = Values.ElementAtOrDefault(value.RoundToInt());
                }
            });
        }

        private KeyValuePair<string, double> _valuePair;

        public KeyValuePair<string, double> ValuePair {
            get => _valuePair;
            set => Apply(value, ref _valuePair, () => {
                if (Values != null) {
                    Value = Values.IndexOf(value);
                }
            });
        }

        [CanBeNull]
        public Dictionary<string, double> Values { get; }

        public void LoadValue(double? saved) {
            if (!saved.HasValue) {
                Value = DefaultValue;
            } else {
                switch (StepsMode) {
                    case CarSetupStepsMode.ActualValue:
                        Value = saved.Value * (FixedStep(Key) ?? 1d);
                        break;
                    case CarSetupStepsMode.StepsNormalized:
                        Value = saved.Value * Step;
                        break;
                    case CarSetupStepsMode.Steps:
                        Value = saved.Value * Step + Minimum;
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }
        }

        public double VisualStep { get; }

        public double DefaultValue { get; }

        public bool HasDefaultValue { get; }

        private double? _ratioMaxSpeed;

        public double? RatioMaxSpeed {
            get => _ratioMaxSpeed;
            set => Apply(value, ref _ratioMaxSpeed);
        }

        public static void UpdateRatioMaxSpeed([NotNull] IReadOnlyList<CarSetupEntry> entries, [NotNull] DataWrapper data, int? selectedTyres, bool skipFinal) {
            var baseValue = Lazier.Create(() => {
                var limiter = data.GetIniFile("engine.ini")["ENGINE_DATA"].GetDoubleNullable("LIMITER");
                var tyreRadius = data.GetIniFile("tyres.ini").GetSections("REAR", -1).Skip(selectedTyres ?? 0)
                                     .FirstOrDefault()?.GetDoubleNullable("RADIUS");
                var tyreLength = 2 * Math.PI * tyreRadius;
                return limiter * tyreLength / 1e3 * 60;
            });

            var lastGearRatio = Lazier.Create(() => {
                var gears = data.GetIniFile("drivetrain.ini")["GEARS"];
                var lastGearKey = @"GEAR_" + gears.GetInt("COUNT", 6);
                return entries.FirstOrDefault(x => x.Key == lastGearKey)?.ValuePair.Value ??
                        gears.GetDoubleNullable(lastGearKey);
            });

            var finalGearRatio = Lazier.Create(() => {
                return entries.FirstOrDefault(x => x.Key == "FINAL_GEAR_RATIO")?.ValuePair.Value ??
                        data.GetIniFile("drivetrain.ini")["GEARS"].GetDoubleNullable("FINAL");
            });

            foreach (var entry in entries) {
                if (entry == null) continue;
                double? resultGearRatio;
                if (entry.Key == "FINAL_GEAR_RATIO") {
                    if (skipFinal) continue;
                    resultGearRatio = lastGearRatio.Value;
                } else {
                    resultGearRatio = finalGearRatio.Value;
                }
                entry.RatioMaxSpeed = baseValue.Value / entry.ValuePair.Value / resultGearRatio;
            }
        }

        private DelegateCommand _resetCommand;

        public DelegateCommand ResetCommand => _resetCommand ?? (_resetCommand = new DelegateCommand(() => Value = DefaultValue));
    }
}