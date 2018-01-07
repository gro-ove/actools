using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Windows.Forms;
using System.Windows.Input;
using AcManager.Internal;
using AcManager.Tools.Helpers.AcSettings;
using AcManager.Tools.SharedMemory;
using AcTools.DataFile;
using AcTools.Processes;
using AcTools.Utils;
using AcTools.Utils.Helpers;
using AcTools.Windows.Input;
using FirstFloor.ModernUI.Helpers;

namespace AcManager.Tools.GameProperties {
    public class ExtraHotkeysRaceHelper : Game.GameHandler {
        private class MemoryListener : IDisposable {
            private static readonly Dictionary<string, InternalUtils.AcControlPointCommand> ExtraCommands
                    = new  Dictionary<string, InternalUtils.AcControlPointCommand> {
                        ["__CM_START_SESSION"] = InternalUtils.AcControlPointCommand.StartGame,
                        ["__CM_RESET_SESSION"] = InternalUtils.AcControlPointCommand.RestartSession,
                        ["__CM_TO_PITS"] = InternalUtils.AcControlPointCommand.TeleportToPits,
                        ["__CM_SETUP_CAR"] = InternalUtils.AcControlPointCommand.TeleportToPitsWithConfig,
                        ["__CM_EXIT"] = InternalUtils.AcControlPointCommand.Shutdown,
                    };

            private KeyboardListener _keyboard;

            public MemoryListener() {
                var ini = new IniFile(AcPaths.GetCfgControlsFilename());

                foreach (var key in AcSettingsHolder.Controls.SystemButtonKeys) {
                    var section = ini[key];
                    if (section.GetInt("JOY", -1) != -1)
                }

                var keys = AcSettingsHolder.Controls.SystemButtonKeys.ToList();

                try {
                    _keyboard = new KeyboardListener();
                    _keyboard.KeyUp += OnKey;
                } catch (Exception e) {
                    Logging.Error(e);
                }
            }

            private static void OnKey(object sender, VirtualKeyCodeEventArgs e) {
                try {
                    if (e.Key == Keys.Escape && Keyboard.Modifiers == ModifierKeys.None && AcSharedMemory.Instance.IsPaused &&
                            (DateTime.Now - AcSharedMemory.Instance.PauseTime).TotalSeconds > 0.15) {
                        AcMousePretender.ClickContinueButton();
                    }
                } catch (Exception ex) {
                    Logging.Error(ex);
                }
            }

            public void Dispose() {
                try {
                    DisposeHelper.Dispose(ref _keyboard);
                } catch (Exception e) {
                    NonfatalError.NotifyBackground("Can’t remove events hook", e);
                }
            }
        }

        public override IDisposable Set(Process process) {
            return new MemoryListener();
        }
    }
}