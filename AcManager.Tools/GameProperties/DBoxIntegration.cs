using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using AcManager.Tools.Helpers;
using AcTools.Processes;

namespace AcManager.Tools.GameProperties {
    public class DBoxIntegration : Game.AdditionalProperties {
        public override IDisposable Set() {
            if (SettingsHolder.Integrated.DBoxIntegration && !string.IsNullOrWhiteSpace(SettingsHolder.Integrated.DBoxLocation)
                    && File.Exists(SettingsHolder.Integrated.DBoxLocation)) {
                Process.Start(SettingsHolder.Integrated.DBoxLocation);
                Thread.Sleep(TimeSpan.FromSeconds(SettingsHolder.Integrated.DBoxDelay));
            }
            return null;
        }
    }
}