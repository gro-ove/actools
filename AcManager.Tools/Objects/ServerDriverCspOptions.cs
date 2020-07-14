using System.Linq;
using System.Windows;
using System.Windows.Media;
using AcTools.Utils;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI.Commands;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Presentation;
using JetBrains.Annotations;

namespace AcManager.Tools.Objects {
    public class ServerDriverCspOptions : NotifyPropertyChanged {
        private bool _blockKeyboard;

        public bool BlockKeyboard {
            get => _blockKeyboard;
            set => Apply(value, ref _blockKeyboard, () => {
                OnPropertyChanged(nameof(CanBlockJoystick));
                OnPropertyChanged(nameof(CanBlockSteeringWheel));
            });
        }

        private bool _blockJoystick;

        public bool BlockJoystick {
            get => _blockJoystick;
            set => Apply(value, ref _blockJoystick, () => {
                OnPropertyChanged(nameof(CanBlockKeyboard));
                OnPropertyChanged(nameof(CanBlockSteeringWheel));
            });
        }

        private bool _blockSteeringWheel;

        public bool BlockSteeringWheel {
            get => _blockSteeringWheel;
            set => Apply(value, ref _blockSteeringWheel, () => {
                OnPropertyChanged(nameof(CanBlockKeyboard));
                OnPropertyChanged(nameof(CanBlockJoystick));
            });
        }

        public bool CanBlockKeyboard => !BlockJoystick || !BlockSteeringWheel;
        public bool CanBlockJoystick => !BlockKeyboard || !BlockSteeringWheel;
        public bool CanBlockSteeringWheel => !BlockKeyboard || !BlockJoystick;

        private bool _forceHeadlights;

        public bool ForceHeadlights {
            get => _forceHeadlights;
            set => Apply(value, ref _forceHeadlights);
        }

        private bool _allowColorChange;

        public bool AllowColorChange {
            get => _allowColorChange;
            set => Apply(value, ref _allowColorChange);
        }

        private bool _allowTeleporting;

        public bool AllowTeleporting {
            get => _allowTeleporting;
            set => Apply(value, ref _allowTeleporting);
        }

        private bool _allowImmediateRepair;

        public bool AllowImmediateRepair {
            get => _allowImmediateRepair;
            set => Apply(value, ref _allowImmediateRepair);
        }

        private bool _allowImmediateRefuel;

        public bool AllowImmediateRefuel {
            get => _allowImmediateRefuel;
            set => Apply(value, ref _allowImmediateRefuel);
        }

        private static readonly Lazier<string> ErrorColor = Lazier.Create(() => ((Color)Application.Current.Resources[@"ErrorColor"]).ToHexString());

        private static string DescriptionWarning(string s) {
            return string.IsNullOrEmpty(s) ? null : $"[color={ErrorColor.Value}]{s}[/color]";
        }

        public bool BlockingAnyInputScheme => BlockKeyboard || BlockJoystick || BlockSteeringWheel;

        private string _description;

        public string Description {
            get {
                if (_description == null) {
                    string controllersTweak = null;
                    if (BlockKeyboard) {
                        controllersTweak = BlockJoystick ? "Steering wheels only" : BlockSteeringWheel ? "Joysticks only" : "Keyboards are not allowed";
                    } else if (BlockJoystick) {
                        controllersTweak = BlockSteeringWheel ? "Keyboards only" : "Joysticks are not allowed";
                    } else if (BlockSteeringWheel) {
                        controllersTweak = "Steering wheels are not allowed";
                    }

                    var list = new[] {
                        DescriptionWarning(controllersTweak),
                        ForceHeadlights ? "Forced headlights" : null,
                        AllowColorChange ? "Color change is allowed" : null,
                        AllowTeleporting ? "Teleportation is allowed" : null,
                        AllowImmediateRepair ? "Immediate repair is allowed" : null,
                        AllowImmediateRefuel ? "Immediate refuel is allowed" : null,
                    }.NonNull().ToList();
                    if (list.Count == 1) {
                        _description = list[0];
                    } else {
                        _description = list.Select(x => $@"• {x}").JoinToString(";\n") + @".";
                    }
                }

                return _description;
            }
        }

        public bool Pack(out string result) {
            byte flag0 = 0;
            flag0 |= (byte)(BlockKeyboard ? 1 << 0 : 0);
            flag0 |= (byte)(BlockJoystick ? 1 << 1 : 0);
            flag0 |= (byte)(BlockSteeringWheel ? 1 << 2 : 0);
            flag0 |= (byte)(ForceHeadlights ? 1 << 3 : 0);
            flag0 |= (byte)(AllowColorChange ? 1 << 4 : 0);
            flag0 |= (byte)(AllowTeleporting ? 1 << 5 : 0);
            flag0 |= (byte)(AllowImmediateRepair ? 1 << 6 : 0);
            flag0 |= (byte)(AllowImmediateRefuel ? 1 << 7 : 0);

            result = new byte[] {
                0, // version,
                flag0,
                (byte)(flag0 ^ 0x17)
            }.ToCutBase64();
            return flag0 != 0;
        }

        public void LoadPacked([CanBeNull] string packed) {
            var data = packed?.FromCutBase64();
            if (data?.Length == 3 && data[0] == 0 && data[2] == (byte)(data[1] ^ 0x17)) {
                BlockKeyboard = (data[1] & (1 << 0)) != 0;
                BlockJoystick = (data[1] & (1 << 1)) != 0;
                BlockSteeringWheel = (data[1] & (1 << 2)) != 0;
                ForceHeadlights = (data[1] & (1 << 3)) != 0;
                AllowColorChange = (data[1] & (1 << 4)) != 0;
                AllowTeleporting = (data[1] & (1 << 5)) != 0;
                AllowImmediateRepair = (data[1] & (1 << 6)) != 0;
                AllowImmediateRefuel = (data[1] & (1 << 7)) != 0;
            } else {
                BlockKeyboard = false;
                BlockJoystick = false;
                BlockSteeringWheel = false;
                ForceHeadlights = false;
                AllowColorChange = false;
                AllowTeleporting = false;
                AllowImmediateRepair = false;
                AllowImmediateRefuel = false;
            }
        }

        private DelegateCommand _resetCommand;

        public DelegateCommand ResetCommand => _resetCommand ?? (_resetCommand = new DelegateCommand(() => {
            LoadPacked(null);
        }));
    }
}