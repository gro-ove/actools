using System;
using AcManager.Tools.Managers.Plugins;
using AcTools.Utils;
using FirstFloor.ModernUI.Helpers;

namespace AcManager.Plugins {
    public class MagickPluginWrapper : IPluginWrapper {
        public string Id => @"Magick";

        public void Enable() {
            try {
                ImageUtils.LoadImageMagickAssembly(PluginsManager.Instance.GetPluginFilename(Id, "Magick.NET-x86.dll"));
                Logging.Write("[MagickPluginWrapper] Test: " + ImageUtils.TestImageMagick());
            } catch (Exception e) {
                Logging.Warning("[MagickPluginWrapper] Enable(): " + e);
            }
        }

        public void Disable() {
            ImageUtils.UnloadImageMagickAssembly();
        }
    }
}
