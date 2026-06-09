using System;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using AcManager.Tools.Managers;
using AcManager.Tools.Objects;
using AcTools.Numerics;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Presentation;
using JetBrains.Annotations;

namespace AcManager.Tools.Data {
    public sealed class WeatherTypeWrapped : Displayable, IEquatable<WeatherTypeWrapped> {
        public WeatherType TypeOpt { get; }
        
        [CanBeNull]
        public string ControllerId { get; }
        
        [CanBeNull]
        public string ControllerSettings { get; set; }

        private WeatherFxControllerData _controllerRef;

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
            DisplayName = controller.DisplayName;
            controller.SubscribeWeak(OnControllerChanged);
        }

        private void OnControllerChanged(object sender, PropertyChangedEventArgs e) {
            if (e.PropertyName == nameof(DisplayName)) {
                DisplayName = _controllerRef.DisplayName;
            }
        }

        public WeatherTypeWrapped(string controllerId, string controllerName, string controllerSettings) {
            TypeOpt = WeatherType.None;
            ControllerId = controllerId;
            ControllerSettings = controllerSettings;
            _controllerRef = WeatherFxControllerData.Instance.Items.GetByIdOrDefault(controllerId);
            if (_controllerRef != null) {
                // _controllerRef.DeserializeSettings(controllerSettings);
                _controllerRef.SubscribeWeak(OnControllerChanged);
            /*} else if (WeatherFxControllerData.Instance.Items.Count == 0) {
                // TODO: Find a way to have individual settings per box?
                Task.Delay(200).ContinueWith(r => ActionExtension.InvokeInMainThreadAsync(() => {
                    _controllerRef = WeatherFxControllerData.Instance.Items.GetByIdOrDefault(controllerId);
                    if (_controllerRef != null) {
                        _controllerRef.DeserializeSettings(controllerSettings);
                        _controllerRef.SubscribeWeak(OnControllerChanged);
                    }
                }));*/
            }
            DisplayName = ControllerRef?.DisplayName ?? controllerName;
        }

        public void RefreshName() {
            if (ControllerRef != null) {
                DisplayName = ControllerRef.DisplayName;
            }
        }

        public void RefreshReference() {
            if (ControllerId != null && _controllerRef == null) {
                _controllerRef = WeatherFxControllerData.Instance.Items.GetByIdOrDefault(ControllerId);
                if (_controllerRef != null) {
                    OnPropertyChanged(nameof(ControllerRef));
                    _controllerRef.SubscribeWeak(OnControllerChanged);
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
            return lhs?.TypeOpt == rhs?.TypeOpt && lhs?.ControllerId == rhs?.ControllerId && lhs?.ControllerSettings == rhs?.ControllerSettings;
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
                return WeatherManager.Instance.Enabled.Where(x => x.Fits(weatherTypeWrapped.TypeOpt, time, temperature))
                        .RandomElementOrDefault();
            }

            return obj as WeatherObject;
        }

        [CanBeNull]
        public static string UnwrapDisplayName(object obj) {
            if (obj is WeatherTypeWrapped weatherTypeWrapped) {
                if (weatherTypeWrapped.ControllerId != null) {
                    return weatherTypeWrapped.DisplayName; 
                }
                if (weatherTypeWrapped.TypeOpt != WeatherType.None) {
                    return weatherTypeWrapped.TypeOpt.GetDescription();
                }
            }

            if (obj == null || obj == RandomWeather) {
                return ToolsStrings.RaceGrid_OpponentNationality_Random;
            }

            return (obj as WeatherObject)?.DisplayName;
        }

        [CanBeNull]
        public static string Serialize([CanBeNull] object obj) {
            if (obj is WeatherTypeWrapped wrapped) {
                if (wrapped.ControllerId != null) {
                    return $"*${wrapped.ControllerId}\t{wrapped.DisplayName.Replace("\t", " ")}\t{wrapped.ControllerRef?.SerializeSettings() ?? wrapped.ControllerSettings}";
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