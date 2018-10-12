using System;
using System.Linq;
using AcManager.Tools.Managers;
using AcManager.Tools.Objects;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Presentation;
using JetBrains.Annotations;

namespace AcManager.Tools.Data {
    public sealed class WeatherTypeWrapped : Displayable, IEquatable<WeatherTypeWrapped> {
        public WeatherType Type { get; }

        public WeatherTypeWrapped(WeatherType type) {
            Type = type;
            DisplayName = type.GetDescription();
        }

        public bool Equals(WeatherTypeWrapped other) {
            return Type == other?.Type;
        }

        public override bool Equals(object obj) {
            return obj is WeatherTypeWrapped w && w.Type == Type;
        }

        public override int GetHashCode() {
            return (int)Type;
        }

        public static bool operator ==(WeatherTypeWrapped lhs, WeatherTypeWrapped rhs)
        {
            return lhs?.Type == rhs?.Type;
        }

        public static bool operator !=(WeatherTypeWrapped lhs, WeatherTypeWrapped rhs)
        {
            return !(lhs == rhs);
        }

        public override string ToString() {
            return $@"WeatherTypeWrapper({Type})";
        }

        public static readonly Displayable RandomWeather = new Displayable { DisplayName = ToolsStrings.Weather_Random };

        [CanBeNull]
        public static WeatherObject Unwrap(object obj, int? time, double? temperature) {
            if (obj is WeatherTypeWrapped weatherTypeWrapped) {
                WeatherManager.Instance.EnsureLoaded();
                return WeatherManager.Instance.Enabled.Where(x => x.Fits(weatherTypeWrapped.Type, time, temperature)).RandomElementOrDefault();
            }

            return obj as WeatherObject;
        }

        [CanBeNull]
        public static string Serialize([CanBeNull] object obj) {
            return obj is WeatherTypeWrapped wrapped ? $@"*{((int)wrapped.Type).ToInvariantString()}" : (obj as WeatherObject)?.Id;
        }

        [CanBeNull]
        public static object Deserialize([CanBeNull] string serialized) {
            if (serialized == null) {
                return RandomWeather;
            }

            if (serialized.StartsWith(@"*")) {
                try {
                    return new WeatherTypeWrapped((WeatherType)(FlexibleParser.TryParseInt(serialized.Substring(1)) ?? 0));
                } catch (Exception e) {
                    Logging.Error(e);
                    return null;
                }
            }

            return WeatherManager.Instance.GetById(serialized);
        }
    }
}