using System;
using System.ComponentModel;
using System.Windows.Input;
using AcTools.Utils.Helpers;
using JetBrains.Annotations;

namespace AcManager.Tools.Objects {
    public interface IPythonAppConfigValue : IWithId, INotifyPropertyChanged {
        string DisplayName { get; }
        
        string Value { get; set; }
        
        string DisplayValueString { get; }

        string ToolTip { get; }

        bool IsNonDefault { get; }

        bool IsResettable { get; }

        bool IsEnabled { get; }

        bool IsNew { get; }

        ICommand ResetCommand { get; }

        void Reset();

        void UpdateReferenced(IPythonAppConfigValueProvider provider);

        void Set(string key, string value, [NotNull] string name, [CanBeNull] string toolTip,
                [CanBeNull] Func<IPythonAppConfigValueProvider, bool> isEnabledTest, [CanBeNull] Func<IPythonAppConfigValueProvider, bool> isHiddenTest,
                bool isNew, [CanBeNull] string originalValue);

        void SetFilesRelativeDirectory(string directory);
    }
}