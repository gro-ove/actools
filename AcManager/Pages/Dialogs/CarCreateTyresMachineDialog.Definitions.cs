using System.Text.RegularExpressions;
using AcManager.Tools.Helpers;
using FirstFloor.ModernUI.Helpers;

namespace AcManager.Pages.Dialogs {
    public partial class CarCreateTyresMachineDialog {
        private static string GetItemName(string key, out string category) {
            string name;

            var pieces = key.Split(new[] { '@' }, 2);
            if (pieces.Length == 2) {
                category = AcStringValues.NameFromId(pieces[0].ToLower());
                name = pieces[1];
            } else {
                category = null;
                name = key;
            }

            name = AcStringValues.NameFromId(name.ToLowerInvariant(), false);
            name = Regex.Replace(name, @"\bk\b", "coefficient");
            name = Regex.Replace(name, @"\bmult\b", "multiplier");
            name = Regex.Replace(name, @"\bref\b", "reference");
            name = Regex.Replace(name, @"\bd\b", "Δ");
            name = Regex.Replace(name, @"\b(?:Cx|Fz|Ls|rr|Xmu)\b", m => m.Value.ToUpperInvariant());
            name = Regex.Replace(name, @"\b(?<= )\d\b", m => $"#{m.Value}");
            name = Regex.Replace(name, @"\b[dD](x|y|camber)\b", m => $"Δ{m.Groups[1].Value.ToTitle()}");
            name = Regex.Replace(name, @"\bexp([xy])\b", m => $"exp({m.Groups[1].Value.ToUpperInvariant()})");
            return name;
        }

        private static SettingEntry GetOutputKeyEntry(string key) {
            var name = GetItemName(key, out var category);
            return new SettingEntry(key, name) { Tag = category };
        }

        private static void UpdateOutputType(string key, out string units, out int titleDigits, out int trackerDigits, out double multiplier) {
            double resultMultiplier;
            string resultUnits;
            int resultTitleDigits, resultTrackerDigits;

            switch (key) {
                case "ANGULAR_INERTIA": // 1.30; angular inertia of front rim+tyre+brake disc together
                    Return(" kg⋅m2", 1, 2);
                    break;
                case "DAMP": // 600; damping rate of front tyre in N⋅sec/m (values usualy from 200 to 1400)
                    Return(" N⋅sec/m", 0, 0);
                    break;
                case "RATE": // 360360; spring rate of front tyres in Nm
                    Return(" kNm", 0, 2, 1e-3);
                    break;
                case "PRESSURE_IDEAL": // 42; ideal pressure for grip
                case "PRESSURE_STATIC": // 36; static (cold) pressure
                    Return(" psi", 1, 2);
                    break;
                case "PRESSURE_SPRING_GAIN": // 10010; increase in N/m per psi (from 40psi reference)
                    Return(" N/m/psi", 1, 2, 1e-3);
                    break;
                case "RELAXATION_LENGTH": // 0.07057;
                    Return(" cm", 1, 3, 1e2);
                    break;
                case "FRICTION_LIMIT_ANGLE": // 8.1; friction limit angle
                    Return("°", 1);
                    break;
                case "CX_MULT": // 1.02
                case "DCAMBER_0": // 1.3
                case "DX0": // 1.1847
                case "DY0": // 1.2267
                case "DY_REF": // 1.15
                case "DX_REF": // 1.15
                case "FALLOFF_SPEED": // 3.5
                case "LS_EXPX": // 0.89
                case "LS_EXPY": // 0.89
                case "PRESSURE_FLEX_GAIN": // 0.6; increase in flex per psi
                case "PRESSURE_RR_GAIN": // 1; increase in RR RESISTENCE per psi
                case "THERMAL@COOL_FACTOR": // 1.77
                    Return("%", 0, 1, 1e2);
                    break;
                case "CAMBER_GAIN": // 0.157; camber gain value as slipangle multiplayer, default 1
                case "FLEX_GAIN": // 0.198
                case "DX1": // -0.053
                case "DY1": // -0.055
                case "BRAKE_DX_MOD": // 0.05
                case "FALLOFF_LEVEL": // 0.89
                case "XMU": // 0.24
                case "THERMAL@BLISTER_GAIN": // 0.3; gain for blistering, how much blistering raises with slip and temperature difference
                case "THERMAL@BLISTER_GAMMA": // 1; gamma for the curve blistering vs slip. higher number makes blistering more influenced by slip
                case "THERMAL@GRAIN_GAIN":
                // 0.4; gain for graining, how much gain raises with slip and temperature difference; 100 value = slipangle×(1+grain%)
                case "THERMAL@GRAIN_GAMMA": // 1; gamma for the curve grain vs slip. higher number makes grain more influenced by slip
                case "THERMAL@ROLLING_K": // 0.27; rolling resistance heat
                    Return("%", 1, 2, 1e2);
                    break;
                case "FLEX": // 0.000774; tire profile flex. the bigger the number the bigger the flex, the bigger the added slipangle with load
                    Return("‰", 1, 3, 1e3);
                    break;
                case "PRESSURE_D_GAIN": // 0.006; loss of tyre footprint with pressure rise
                case "RADIUS_ANGULAR_K": // 0.01
                case "ROLLING_RESISTANCE_1": // 0.000509; rolling resistance velocity (squared) component
                case "SPEED_SENSITIVITY": // 0.002567; speed sensitivity value
                case "SURFACE_ROLLING_K": // 1.0007
                case "THERMAL@CORE_TRANSFER": // 0.00024; how fast heat transfers from tyre to inner air and back, bidirectional
                case "THERMAL@FRICTION_K": // 0.03702; quantity of slip becoming heat
                case "THERMAL@INTERNAL_CORE_TRANSFER": // 0.00045
                case "THERMAL@PATCH_TRANSFER": // 0.00027; how fast heat transfers from one tyre location to the other: values 0…1
                case "THERMAL@SURFACE_TRANSFER": // 0.0200; how fast external sources heat the tyre tread touching the asphalt: values 0…1
                    Return("‰", 2, 3, 1e3);
                    break;
                case "ROLLING_RESISTANCE_0": // 12; rolling resistance constant component
                    Return(t: 1);
                    break;
                case "DCAMBER_1": // -13; D dependency on camber: D=D×(1−(camberRAD×DCAMBER_0+camberRAD²×DCAMBER_1)), camberRAD: absolute value of camber
                case "FZ0": // 2982
                case "ROLLING_RESISTANCE_SLIP": // 4251; rolling reistance slip angle component
                    Return(t: 0);
                    break;
                case "RADIUS": // 0.312; tyre radius in meters
                case "RIM_RADIUS": // 0.2286; rim radius in meters (use 1 inch more than nominal)
                case "WIDTH": // 0.245
                    goto default;
                default:
                    Return();
                    break;
            }

            void Return(string u = null, int t = 2, int? d = null, double m = 1d) {
                resultUnits = u ?? "";
                resultTitleDigits = t;
                resultTrackerDigits = d ?? t + 1;
                resultMultiplier = m;
            }

            units = resultUnits;
            titleDigits = resultTitleDigits;
            trackerDigits = resultTrackerDigits;
            multiplier = resultMultiplier;
        }
    }
}