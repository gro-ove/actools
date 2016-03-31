using System.ComponentModel;

namespace AcTools.Processes {
    public enum AssistState {
        [Description("0")]
        Off,

        [Description("2")]
        On,

        [Description("1")]
        Factory
    }
}
