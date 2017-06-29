using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using AcTools.Render.Kn5Specific;
using AcTools.SoundbankPlayer;
using AcTools.Utils;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI.Helpers;
using SlimDX;

namespace AcManager.AcSound {
    public class AcCarSound : IAcCarSound {
        private readonly string _prefix;
        private readonly AcCarPlayer _player;

        public AcCarSound(AcCarPlayer player) {
            _player = player;

            var carId = _player.GetSoundbackKeys().Select(x => x.StartsWith("/cars/") ? x.ApartFromFirst("/cars/").Split('/')[0] : null)
                               .NonNull().First();
            _prefix = $"/cars/{carId}/";
        }

        private readonly Busy _doorBusy = new Busy();
        private bool _doorWarning;

        public void Door(bool open, TimeSpan delay) {
            _doorBusy.DoDelay(() => {
                try {
                    _player.SetParam("state", open ? 1 : 0);
                    _player.ToggleEvent(_prefix + "door", true);
                } catch (Exception e) {
                    if (!_doorWarning) {
                        _doorWarning = true;
                        Logging.Warning(e);
                    }
                }
            }, delay);
        }

        private bool? _engineExternal;
        private bool _engineWarning;

        public void Engine(bool? external, float rpm, float throttle) {
            try {
                if (_engineExternal != external) {
                    if (_engineExternal == true) {
                        _player.ToggleEvent(_prefix + "engine_ext", false);
                    } else if (_engineExternal == false) {
                        _player.ToggleEvent(_prefix + "engine_int", false);
                    }

                    if (external == true) {
                        _player.ToggleEvent(_prefix + "engine_ext", true);
                    } else if (external == false) {
                        _player.ToggleEvent(_prefix + "engine_int", true);
                    }

                    _engineExternal = external;
                }

                _player.SetParam("rpms", rpm);
                _player.SetParam("throttle", throttle);
            } catch (Exception e) {
                if (!_engineWarning) {
                    _engineWarning = true;
                    Logging.Warning(e);
                }
            }
        }

        private bool _boostActive;
        private bool _boostWarning;

        public void Turbo(float? value) {
            try {
                if (_boostActive != value.HasValue) {
                    _player.ToggleEvent(_prefix + "turbo", value.HasValue);
                    _boostActive = value.HasValue;
                }

                _player.SetParam("boost", value ?? 0f);
            } catch (Exception e) {
                if (!_boostWarning) {
                    _boostWarning = true;
                    Logging.Warning(e);
                }
            }
        }

        private bool _limiterActive;
        private bool _limiterWarning;

        public void Limiter(float? value) {
            try {
                var on = value.HasValue && value > 0f;
                if (_limiterActive != on) {
                    _player.ToggleEvent(_prefix + "limiter", on);
                    _limiterActive = on;
                }

                _player.SetParam("decay", 1f - (value ?? 0f));
            } catch (Exception e) {
                if (!_limiterWarning) {
                    _limiterWarning = true;
                    Logging.Warning(e);
                }
            }
        }

        public void UpdateCarPosition(Vector3 forward, Vector3 position, Vector3 up, Vector3 velocity) {
            _player.Set3DAttributes(_prefix + "door", forward, position, up, velocity);
        }

        private bool _hornWarning;

        public void Horn(bool active) {
            try {
                _player.ToggleEvent(_prefix + "horn", active);
            } catch (Exception e) {
                if (!_hornWarning) {
                    _hornWarning = true;
                    Logging.Warning(e);
                }
            }
        }

        public void UpdateHornPosition(Vector3 forward, Vector3 position, Vector3 up, Vector3 velocity) {
            _player.Set3DAttributes(_prefix + "horn", forward, position, up, velocity);
        }

        public void UpdateEnginePosition(Vector3 forward, Vector3 position, Vector3 up, Vector3 velocity) {
            _player.Set3DAttributes(new[] {
                _prefix + "engine_ext",
                _prefix + "engine_int",
                _prefix + "turbo",
                _prefix + "limiter",
            }, forward, position, up, velocity);
        }

        public void Dispose() {
            _player.Dispose();
        }
    }

    internal static class SoundVectorExtension {
        public static unsafe SoundVector ToSoundVector(this Vector3 vec) {
            return *((SoundVector*)&vec);
        }

        public static void Set3DAttributes(this AcCarPlayer player, string path, Vector3 forward, Vector3 position, Vector3 up, Vector3 velocity) {
            var forwardSound = forward.ToSoundVector();
            var positionSound = position.ToSoundVector();
            var upSound = up.ToSoundVector();
            var velocitySound = velocity.ToSoundVector();
            player.Set3DAttributes(path, forwardSound, positionSound, upSound, velocitySound);
        }

        public static void Set3DAttributes(this AcCarPlayer player, string[] path, Vector3 forward, Vector3 position, Vector3 up, Vector3 velocity) {
            var forwardSound = forward.ToSoundVector();
            var positionSound = position.ToSoundVector();
            var upSound = up.ToSoundVector();
            var velocitySound = velocity.ToSoundVector();
            for (var i = 0; i < path.Length; i++) {
                var p = path[i];
                player.Set3DAttributes(p, forwardSound, positionSound, upSound, velocitySound);
            }
        }
    }
}
