using System;
using System.IO;
using AcManager.Tools.Managers.Plugins;
using AcManager.Tools.Miscellaneous;
using AcTools.Utils;
using AcTools.Windows;
using FirstFloor.ModernUI.Helpers;

namespace AcManager.Plugins {
    public class MagickPluginWrapper : IPluginWrapper {
        public string Id => MagickPluginHelper.PluginId;
        
        public void Enable() {
            try {
                InitializeNative();
                ImageUtils.LoadImageMagickAssembly(PluginsManager.Instance.GetPluginFilename(Id, MagickPluginHelper.AssemblyName));
                Logging.Write("Test: " + ImageUtils.TestImageMagick());
            } catch (Exception e) {
                Logging.Warning(e);
            }
        }

        private static bool _nativeInitialized;

        public static bool InitializeNative() {
            if (!_nativeInitialized) {
                var filename = PluginsManager.Instance.GetPluginFilename(MagickPluginHelper.PluginId, MagickPluginHelper.NativeName);
                if (!File.Exists(filename)) return false;

                var libDirectory = Kernel32.GetDllDirectory();
                if (libDirectory == null) {
                    Kernel32.SetDllDirectory(Path.GetDirectoryName(filename));
                } else {
                    var lib = new FileInfo(filename);
                    var destination = new FileInfo(Path.Combine(libDirectory, lib.Name));
                    if (lib.Exists && (!destination.Exists || lib.LastWriteTime > destination.LastWriteTime)) {
                        lib.CopyTo(destination.FullName, true);
                        Logging.Debug("Lib copied");
                    }
                }

                _nativeInitialized = true;
            }

            return true;
        }

        public void Disable() {
            Logging.Here();
            ImageUtils.UnloadImageMagickAssembly();
        }
    }
}
