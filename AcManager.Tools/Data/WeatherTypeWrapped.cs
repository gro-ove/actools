using System;
using System.Linq;
using AcManager.Tools.Managers;
using AcManager.Tools.Objects;
using AcTools.Numerics;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Presentation;
using JetBrains.Annotations;

namespace AcManager.Tools.Data {
    public sealed class WeatherTypeWrapped : Displayable, IEquatable<WeatherTypeWrapped> {
        public WeatherType TypeOpt { get; }
        
        [CanBeNull]
        public string ControllerId { get; }
        
        [CanBeNull]
        public string ControllerSettings { get; }

        private WeatherFxControllerData _controllerRef;
        private string _controllerName;

        [CanBeNull]
        public WeatherFxControllerData ControllerRef => ControllerId == null ? null 
                : _controllerRef ?? (_controllerRef = WeatherFxControllerData.Instance.Items.GetByIdOrDefault(ControllerId));

        public WeatherTypeWrapped(WeatherType type) {
            TypeOpt = type;
            DisplayName = type.GetDescription();
        }

        public WeatherTypeWrapped(WeatherFxControllerData controller) {
            TypeOpt = WeatherType.None;
            ControllerId = controller.Id;
            ControllerSettings = null;
            _controllerRef = controller;
            _controllerName = controller.DisplayName;
            DisplayName = $"Dynamic: {_controllerName}";
        }

        public WeatherTypeWrapped(string controllerId, string controllerName, string controllerSettings) {
            TypeOpt = WeatherType.None;
            ControllerId = controllerId;
            ControllerSettings = controllerSettings;
            _controllerRef = WeatherFxControllerData.Instance.Items.GetByIdOrDefault(controllerId);
            if (_controllerRef != null) {
                _controllerRef.DeserializeSettings(controllerSettings);
            }
            _controllerName = ControllerRef?.DisplayName ?? controllerName;
            DisplayName = $"Dynamic: {_controllerName}";
        }

        public void RefreshReference() {
            if (ControllerId != null && _controllerRef == null) {
                _controllerRef = WeatherFxControllerData.Instance.Items.GetByIdOrDefault(ControllerId);
                if (_controllerRef != null) {
                    OnPropertyChanged(nameof(ControllerRef));
                }
            }
        }

        public bool Equals(WeatherTypeWrapped other) {
            return TypeOpt == other?.TypeOpt && ControllerId == other.ControllerId;
        }

        public override bool Equals(object obj) {
            return obj is WeatherTypeWrapped w && w.TypeOpt == TypeOpt && w.ControllerId == ControllerId;
        }

        public override int GetHashCode() {
            return HashCodeHelper.CombineHashCodes((int)TypeOpt, ControllerId?.GetHashCode() ?? 0);
        }

        public static bool operator ==(WeatherTypeWrapped lhs, WeatherTypeWrapped rhs) {
            return lhs?.TypeOpt == rhs?.TypeOpt && lhs?.ControllerId == rhs?.ControllerId;
        }

        public static bool operator !=(WeatherTypeWrapped lhs, WeatherTypeWrapped rhs) {
            return !(lhs == rhs);
        }

        public override string ToString() {
            return $@"WeatherTypeWrapper({TypeOpt}, {ControllerId ?? @"?"})";
        }

        public static readonly Displayable RandomWeather = new Displayable { DisplayName = ToolsStrings.Weather_Random };

        [CanBeNull]
        public static WeatherObject Unwrap(object obj, int? time, double? temperature) {
            if (obj is WeatherTypeWrapped weatherTypeWrapped) {
                if (weatherTypeWrapped.ControllerId != null) {
                    return null; // TODO?
                }
                
                WeatherManager.Instance.EnsureLoaded();
                return WeatherManager.Instance.Enabled.Where(x => x.Fits(weatherTypeWrapped.TypeOpt, time, temperature)).RandomElementOrDefault();
            }

            return obj as WeatherObject;
        }

        [CanBeNull]
        public static string Serialize([CanBeNull] object obj) {
            if (obj is WeatherTypeWrapped wrapped) {
                if (wrapped.ControllerId != null) {
                    return $"*${wrapped.ControllerId}\t{wrapped._controllerName.Replace("\t", " ")}\t{wrapped.ControllerRef?.SerializeSettings() ?? wrapped.ControllerSettings}";
                }
                return $@"*{((int)wrapped.TypeOpt).ToInvariantString()}";
            }
            return (obj as WeatherObject)?.Id;
        }

        [CanBeNull]
        public static object Deserialize([CanBeNull] string serialized) {
            if (serialized == null) {
                return RandomWeather;
            }

            if (serialized.StartsWith(@"*")) {
                try {
                    if (serialized.StartsWith(@"*$")) {
                        var pieces = serialized.Substring(2).Split(new []{ '\t' }, 3, StringSplitOptions.None);
                        if (pieces.Length != 3) {
                            return null;
                        }
                        return new WeatherTypeWrapped(pieces[0], pieces[1], pieces[2]);
                    }

                    return new WeatherTypeWrapped((WeatherType)(FlexibleParser.TryParseInt(serialized.Substring(1)) ?? 0));
                } catch (Exception e) {
                    Logging.Error(e);
                    return null;
                }
            }

            return WeatherManager.Instance.GetById(serialized);
        }

        public void PublishSettings() {
            if (ControllerRef is WeatherFxControllerData controller) {
                controller.PublishSettings();
            } else {
                WeatherFxControllerData.PublishGenSettings(ControllerId, ControllerSettings);
            }
        }
    }
}