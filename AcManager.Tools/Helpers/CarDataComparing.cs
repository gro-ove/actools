using System.IO;
using System.Linq;
using AcManager.Tools.Managers;
using AcTools.DataAnalyzer;
using AcTools.Utils;
using JetBrains.Annotations;

namespace AcManager.Tools.Helpers {
    public static class CarDataComparing {
        public static RulesWrapper GetRules([CanBeNull] string[] additionalDonorIds) {
            var root = AcRootDirectory.Instance.RequireValue;
            var rules = BinaryResources.Rules;
            var overrideRules = FilesStorage.Instance.GetContentFile("AnalyzerRules.txt");
            if (overrideRules.Exists) {
                rules = File.ReadAllText(overrideRules.Filename);
            }
            return new RulesWrapper(root, rules,
                    FilesStorage.Instance.GetTemporaryFilename("Cars.data"),
                    additionalDonorIds == null ?
                            AcKunosContent.GetKunosCarIds(root).ToArray() :
                            AcKunosContent.GetKunosCarIds(root).Union(additionalDonorIds).ToArray());
        }
    }
}