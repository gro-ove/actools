using System;
using System.Collections.Generic;
using System.Linq;
using AcTools.ExtraKn5Utils.Kn5Utils;
using AcTools.Kn5File;
using AcTools.Numerics;
using AcTools.Utils.Helpers;
using JetBrains.Annotations;
using StringBasedFilter;

namespace AcTools.ExtraKn5Utils.LodGenerator {
    public class CarLodGeneratorMergeRules {
        private readonly Kn5NodeFilterContext _filterContext;

        [CanBeNull]
        private IFilter<Kn5Node>[] _mergeExceptions;

        [CanBeNull]
        private IFilter<Kn5Node>[] _mergeParents;

        [CanBeNull]
        private IFilter<Kn5Node>[] _mergeAsBlack;

        [CanBeNull]
        private IFilter<Kn5Node>[] _elementToRemove;

        [CanBeNull]
        private IFilter<Kn5Node>[] _emptyNodesToKeep;

        [CanBeNull]
        private IFilter<Kn5Node>[] _convertUv2;

        [CanBeNull]
        private Tuple<IFilter<Kn5Node>, double>[] _elementPriorities;

        [CanBeNull]
        private Tuple<IFilter<Kn5Node>, double>[] _offsetsAlongNormal;

        public CarLodGeneratorMergeRules(Kn5NodeFilterContext filterContext, CarLodGeneratorStageParams stage) {
            _filterContext = filterContext;
            _mergeExceptions = stage.MergeExceptions?.Select(filterContext.CreateFilter).ToArray();
            _mergeParents = stage.MergeParents?.Select(filterContext.CreateFilter).ToArray();
            _mergeAsBlack = stage.MergeAsBlack?.Select(filterContext.CreateFilter).ToArray();
            _elementToRemove = stage.ElementsToRemove?.Select(filterContext.CreateFilter).ToArray();
            _emptyNodesToKeep = stage.EmptyNodesToKeep?.Select(filterContext.CreateFilter).ToArray();
            _convertUv2 = stage.ConvertUv2?.Select(filterContext.CreateFilter).ToArray();
            _elementPriorities = stage.ElementsPriorities?.Select(x => Tuple.Create(filterContext.CreateFilter(x.Filter), x.Priority)).ToArray();
            _offsetsAlongNormal = stage.OffsetsAlongNormal?.Select(x => Tuple.Create(filterContext.CreateFilter(x.Filter), x.Priority)).ToArray();
        }

        public bool HasParentWithSameName([NotNull] Kn5Node node) {
            for (var parent = _filterContext.GetParent(node); parent != null; parent = _filterContext.GetParent(parent)) {
                if (parent.Name == node.Name) return true;
            }
            return false;
        }

        public double CalculateReductionPriority(Kn5Node node) {
            return _elementPriorities?.FirstOrDefault(x => x.Item1.Test(node))?.Item2 ?? 1d;
        }

        public double GetOffsetAlongNormal(Kn5Node node) {
            return _offsetsAlongNormal?.FirstOrDefault(x => x.Item1.Test(node))?.Item2 ?? 0d;
        }

        public bool CanSkipNode(Kn5Node node) {
            return node.NodeClass == Kn5NodeClass.SkinnedMesh
                    || node.NodeClass == Kn5NodeClass.Mesh && !node.IsRenderable
                    || _elementToRemove?.Any(x => x.Test(node)) == true;
        }

        public bool AnyUv2Converts() {
            return _convertUv2?.Length > 0;
        }

        public bool UseUv2(Kn5Node node) {
            return _convertUv2?.Any(x => x.Test(node)) == true;
        }

        public bool CanMerge(Kn5Node node) {
            return _mergeExceptions?.Any(x => x.Test(node)) != true;
        }

        public bool CanRemoveEmptyNode(Kn5Node node) {
            return _emptyNodesToKeep?.Any(x => x.Test(node)) != true;
        }

        public bool IsNodeMergeRoot(Kn5Node node) {
            return _mergeParents?.Any(x => x.Test(node)) == true;
        }

        public int MergeGroup(Kn5Node node, double priority) {
            if (_mergeAsBlack?.Any(x => x.Test(node)) == true) {
                node.MaterialId = uint.MaxValue;
                return priority.GetHashCode();
            }
            return (((int)node.MaterialId * 397) 
                    | (node.IsTransparent ? 1 << 31 : 0) 
                    | (node.CastShadows ? 1 << 30 : 0) 
                    | (node.Uv2 != null ? 1 << 29 : 0)) ^ priority.GetHashCode();
        }

        public int GroupOrder(IKn5 kn5, IEnumerable<Tuple<Kn5Node, double, Mat4x4>> node, Dictionary<Kn5Node, int> nodeIndices) {
            var isTransparent = false;
            var isBlending = false;
            var maxIndex = 0;
            foreach (var n in node) {
                isTransparent |= n.Item1.IsTransparent;
                isBlending = kn5.GetMaterial(n.Item1.MaterialId)?.BlendMode == Kn5MaterialBlendMode.AlphaBlend;
                maxIndex = Math.Max(maxIndex, nodeIndices[n.Item1]);
            }
            return (isTransparent ? 1 << 31 : 0) | (isBlending ? 1 << 30 : 0) | maxIndex;
        }

        public void FinalizeKn5(IKn5 kn5) {
            var materialAdded = false;
            foreach (var node in kn5.Nodes) {
                if (node.NodeClass != Kn5NodeClass.Base && node.MaterialId == uint.MaxValue) {
                    if (!materialAdded) {
                        materialAdded = true;
                        if (!kn5.Materials.ContainsKey("__LodGenBlack")) {
                            kn5.Materials["__LodGenBlack"] = Kn5MaterialUtils.Create("__LodGenBlack");
                        }
                    }
                    node.MaterialId = (uint)kn5.Materials.FindIndex(m => m.Key == "__LodGenBlack");
                }
            }
        }
    }
}