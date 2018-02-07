using System.ComponentModel;

namespace AcTools.NeuralTyres.Data {
    public enum FannTrainingAlgorithm {
        [Description("Incremental: basic algorithm")]
        Incremental = 0,

#if DEBUG
        [Description("Batch: slower, but more accurate")]
        Batch = 1,
#endif

        [Description("RPROP: doesn’t use learing rate")]
        RProp = 2,

        [Description("QUICKPROP: more advanced algorithm")]
        QuickProp = 3,

#if DEBUG
        [Description("SARPROP: whatever that is")]
        SarProp = 4,
#endif
    }
}