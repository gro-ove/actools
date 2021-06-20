using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using AcTools.DataFile;
using AcTools.ExtraKn5Utils.ExtraMath;
using AcTools.ExtraKn5Utils.KsAnimUtils;
using AcTools.Kn5File;
using AcTools.KsAnimFile;
using AcTools.Utils;
using AcTools.Utils.Helpers;
using JetBrains.Annotations;
using Newtonsoft.Json.Linq;
using StringBasedFilter;
using StringBasedFilter.Parsing;
using StringBasedFilter.TestEntries;

namespace AcTools.ExtraKn5Utils.Kn5Utils {
    public class Kn5NodeFilterContext : ITester<Kn5Node> {
        private readonly JObject _definitions;
        private readonly Dictionary<string, string> _userDefined;
        private readonly string _carDirectory;
        private readonly DataWrapper _carData;
        private readonly IKn5 _kn5;
        private readonly FilterParams _filterParams;

        public IFilter<Kn5Node> CreateFilter([NotNull] string filterStr) {
            return Filter.Create(this, filterStr, _filterParams);
        }

        public Aabb3 GetAabb3([NotNull] Kn5Node node) {
            return GetAabb3Dictionary().GetValueOrDefault(node);
        }

        [CanBeNull]
        public Kn5Node GetParent([NotNull] Kn5Node node) {
            return GetParentsDictionary().GetValueOrDefault(node);
        }

        public Kn5NodeFilterContext([NotNull] JObject definitions, [NotNull] Dictionary<string, string> userDefined,
                [NotNull] string carDirectory, [NotNull] DataWrapper carData, [NotNull] IKn5 kn5) {
            _definitions = definitions;
            _userDefined = userDefined;
            _carDirectory = carDirectory;
            _carData = carData;
            _kn5 = kn5;

            _filterParams = new FilterParams {
                CustomTestEntryFactory = str => {
                    if (str.StartsWith("$")) {
                        var definition = GetDefinition(str);
                        if (definition != null) {
                            return new DefinitionTestEntry(Filter.Create(this, definition, _filterParams));
                        }
                    }
                    if (str.StartsWith("@:")) {
                        return ResolveQuery(str);
                    }
                    return null;
                },
                CustomValueParser = (string str, ref int pos) => {
                    if (pos < str.Length - 1 && str[pos] == '@' && str[pos + 1] == ':') {
                        var start = pos;
                        for (var bracket = 0; pos < str.Length && (bracket > 0 || !IsValueTerminatingCharacter(str[pos])); pos++) {
                            bracket += str[pos] == '[' ? 1 : str[pos] == ']' ? -1 : 0;
                        }
                        return str.Substring(start, pos - start);
                    }
                    return null;
                },
                ValueSplitter = new ValueSplitter(s => {
                    var m = Regex.Match(s, @"^([a-zA-Z]+)([:<>≤≥])");
                    return !m.Success ? null : new FilterPropertyValue(m.Groups[1].Value,
                            FilterComparingOperations.Parse(m.Groups[2].Value), s.Substring(m.Length).TrimStart());
                }, ':', '<', '>', '≤', '≥'),
                CaseInvariant = false,
                TreatSpacesAsAndOperands = false,
                StringMatchMode = StringMatchMode.CompleteMatch
            };
        }

        private static bool IsValueTerminatingCharacter(char c) {
            return char.IsWhiteSpace(c) || c == '&' || c == '|' || c == ',' || c == '^' || c == ')';
        }

        [CanBeNull]
        private string GetDefinition(string key) {
            return _definitions["elements"][key]?.ToString();
        }

        [CanBeNull]
        private ITestEntry ResolveQuery([NotNull] string query) {
            var s0 = query.IndexOf('/', 2);
            var s1 = query.LastIndexOf('/');
            var piece0 = s0 == -1 ? query.Substring(2) : query.Substring(2, s0 - 2);
            var piece1 = s1 == s0 ? s0 == 0 ? string.Empty : query.Substring(s0 + 1) : query.Substring(s0 + 1, s1 - s0 - 1);
            var piece2 = s1 == s0 ? string.Empty : query.Substring(s1 + 1);

            if (piece0.EndsWith(".ini")) return new ListTestEntry(LoadIni().NonNull().ToList());
            if (piece0.EndsWith(".ksanim")) return new ListTestEntry(LoadAnimation().NonNull().ToList());
            if (piece0 == "userDefined") {
                var value = _userDefined.GetValueOrDefault(piece1);
                if (value == null) return new ConstTestEntry(false);
                return new DefinitionTestEntry(Filter.Create(this, value, _filterParams));
            }

            return null;

            IEnumerable<string> LoadIni() {
                var s2 = piece1.IndexOf('[');
                IFilter<IniFileSection> sectionContentFilter = null;
                if (s2 != -1 && piece1.EndsWith("]")) {
                    sectionContentFilter = SectionFilterFactory.Create(piece1.Substring(s2 + 1, piece1.Length - s2 - 2));
                    piece1 = piece1.Substring(0, s2);
                }
                var sectionNameFilter = Filter.Create(StringTester.Instance, piece1, true);
                var valueNameFilter = Filter.Create(StringTester.Instance, piece2, true);
                return _carData.GetIniFile(piece0)
                        .Where(section => sectionNameFilter.Test(section.Key) && sectionContentFilter?.Test(section.Value) != false)
                        .SelectMany(section => section.Value)
                        .Where(pair => valueNameFilter.Test(pair.Key))
                        .Select(pair => pair.Value);
            }

            IEnumerable<string> LoadAnimation() {
                var animation = Path.Combine(_carDirectory, "animations", piece0);
                if (File.Exists(animation)) {
                    foreach (var entry in KsAnim.FromFile(animation)
                            .Entries.Where(x => !x.Value.IsStatic())) {
                        yield return entry.Key;
                    }
                }
            }
        }

        private static readonly FilterFactory<IniFileSection> SectionFilterFactory = FilterFactory.Create<IniFileSection>(
                (obj, key, value) => key == null ? obj.Any(x => value.Test(x.Key)) : value.Test(obj.GetNonEmpty(key)),
                FilterParams.DefaultStrictNoChildKeys);

        private static bool TestNode([NotNull] ITestEntry entry, [CanBeNull] Kn5Node node) {
            switch (entry) {
                case DefinitionTestEntry definitionEntry:
                    return definitionEntry.Test(node);
                case ListTestEntry listEntry:
                    return listEntry.Test(node);
                default:
                    return entry.Test(node?.Name);
            }
        }

        private IEnumerable<Kn5Node> GetParents([NotNull] Kn5Node node) {
            while (true) {
                node = GetParentsDictionary().GetValueOrDefault(node);
                if (node == null) yield break;
                yield return node;
            }
        }

        string ITester<Kn5Node>.ParameterFromKey(string key) {
            return null;
        }

        [CanBeNull]
        private Dictionary<Kn5Node, Aabb3> _aabb3s;

        private Dictionary<Kn5Node, Aabb3> GetAabb3Dictionary() {
            if (_aabb3s != null) return _aabb3s;

            var aabb3Dictionary = _aabb3s = new Dictionary<Kn5Node, Aabb3>();
            SetAabb(_kn5.RootNode);
            return aabb3Dictionary;

            Aabb3 SetAabb(Kn5Node node) {
                if (node.NodeClass != Kn5NodeClass.Base) {
                    return aabb3Dictionary[node] = node.CalculateAabb3(_kn5.RootNode);
                }
                var aabb3 = Aabb3.CreateNew();
                foreach (var child in node.Children) {
                    aabb3.Extend(SetAabb(child));
                }
                return aabb3Dictionary[node] = aabb3;
            }
        }

        private Dictionary<Kn5Node, Kn5Node> _parents;

        private Dictionary<Kn5Node, Kn5Node> GetParentsDictionary() {
            return _parents ?? (_parents = _kn5.Nodes.Where(n => n.NodeClass == Kn5NodeClass.Base)
                    .SelectMany(x => x.Children.Select(y => new { x, y })).ToDictionary(x => x.y, x => x.x));
        }

        bool TestAabb(string key, Kn5Node obj, ITestEntry value) {
            var aabb3 = GetAabb3Dictionary();
            if (!aabb3.TryGetValue(obj, out var aabb)) return false;

            string cutKey;
            if (key.StartsWith("aabbRel")) {
                cutKey = key.Substring(7);
                if (aabb3.TryGetValue(_kn5.RootNode, out var aabbModel)) {
                    aabb.Min.Y = aabb.Min.Y.LerpInv(aabbModel.Min.Y, aabbModel.Max.Y);
                    aabb.Max.Y = aabb.Max.Y.LerpInv(aabbModel.Min.Y, aabbModel.Max.Y);
                    aabb.Min.Z = aabb.Min.Z.LerpInv(aabbModel.Min.Z, aabbModel.Max.Z);
                    aabb.Max.Z = aabb.Max.Z.LerpInv(aabbModel.Min.Z, aabbModel.Max.Z);
                    if (cutKey.EndsWith("X") && !cutKey.StartsWith("Size")) {
                        aabb.Min.X /= Math.Max(aabbModel.Max.X, aabbModel.Min.X);
                        aabb.Max.X /= Math.Max(aabbModel.Max.X, aabbModel.Min.X);
                    } else {
                        aabb.Min.X = aabb.Min.X.LerpInv(aabbModel.Min.X, aabbModel.Max.X);
                        aabb.Max.X = aabb.Max.X.LerpInv(aabbModel.Min.X, aabbModel.Max.X);
                    }
                }
            } else {
                cutKey = key.Substring(4);
            }

            switch (cutKey) {
                case "Size":
                    return value.Test(aabb.Size.Length());
                case "SizeX":
                    return value.Test(aabb.Size.X);
                case "SizeY":
                    return value.Test(aabb.Size.Y);
                case "SizeZ":
                    return value.Test(aabb.Size.Z);
                case "CenterX":
                    return value.Test(aabb.Center.X);
                case "CenterY":
                    return value.Test(aabb.Center.Y);
                case "CenterZ":
                    return value.Test(aabb.Center.Z);
                case "MinX":
                    return value.Test(aabb.Min.X);
                case "MinY":
                    return value.Test(aabb.Min.Y);
                case "MinZ":
                    return value.Test(aabb.Min.Z);
                case "MaxX":
                    return value.Test(aabb.Max.X);
                case "MaxY":
                    return value.Test(aabb.Max.Y);
                case "MaxZ":
                    return value.Test(aabb.Max.Z);
                default:
                    AcToolsLogging.Write("Unknown property: " + key);
                    return false;
            }
        }

        bool ITester<Kn5Node>.Test(Kn5Node obj, string key, ITestEntry value) {
            switch (key) {
                case "directParent":
                    return TestNode(value, GetParentsDictionary().GetValueOrDefault(obj));
                case "parent":
                    return GetParents(obj).Any(x => TestNode(value, x));
                case "transparent":
                    return value.Test(obj.IsTransparent);
                case "visible":
                    return value.Test(obj.NodeClass == Kn5NodeClass.Base || obj.IsVisible);
                case "mesh":
                    return value.Test(obj.NodeClass == Kn5NodeClass.Mesh);
                case "skinnedMesh":
                    return value.Test(obj.NodeClass == Kn5NodeClass.SkinnedMesh);
                case "node":
                    return value.Test(obj.NodeClass == Kn5NodeClass.Base);
                case "active":
                    return value.Test(obj.Active);
                case "material":
                    return value.Test(Material()?.Name);
                case "shader":
                    return value.Test(Material()?.ShaderName);
                case "texture":
                    return Material()?.TextureMappings.Any(tm => value.Test(tm.Texture)) == true;
                case "textureSlot":
                    return Material()?.TextureMappings.Any(tm => value.Test(tm.Name)) == true;
                case "materialBlendMode":
                    return value.Test(MaterialBlendMode(Material()));
                case null:
                    return TestNode(value, obj);
                default:
                    if (key.StartsWith("aabb")) {
                        return TestAabb(key, obj, value);
                    }
                    return false;
            }

            Kn5Material Material() {
                return obj.NodeClass == Kn5NodeClass.Base ? null : _kn5.GetMaterial(obj.MaterialId);
            }

            string MaterialBlendMode(Kn5Material mat) {
                if (mat == null) return null;
                if (mat.AlphaTested || mat.BlendMode == Kn5MaterialBlendMode.AlphaToCoverage) return "alphaTest";
                if (mat.BlendMode == Kn5MaterialBlendMode.AlphaBlend) return "alphaBlend";
                return "opaque";
            }
        }

        private class DefinitionTestEntry : ITestEntry {
            private readonly IFilter<Kn5Node> _filter;

            public DefinitionTestEntry(IFilter<Kn5Node> filter) {
                _filter = filter;
            }

            public bool Test(Kn5Node value) => _filter.Test(value);
            void ITestEntry.Set(ITestEntryFactory factory) { }
            bool ITestEntry.Test(string value) => false;
            bool ITestEntry.Test(double value) => false;
            bool ITestEntry.Test(bool value) => false;
            bool ITestEntry.Test(TimeSpan value) => false;
            bool ITestEntry.Test(DateTime value) => false;
            public override string ToString() => _filter.ToString();
        }

        private class ListTestEntry : ITestEntry {
            private readonly List<string> _filter;

            public ListTestEntry(List<string> filter) {
                _filter = filter;
            }

            public bool Test(Kn5Node value) => _filter.Any(x => x == value?.Name);
            void ITestEntry.Set(ITestEntryFactory factory) { }
            bool ITestEntry.Test(string value) => false;
            bool ITestEntry.Test(double value) => false;
            bool ITestEntry.Test(bool value) => false;
            bool ITestEntry.Test(TimeSpan value) => false;
            bool ITestEntry.Test(DateTime value) => false;
            public override string ToString() => $"[{_filter.JoinToString(", ")}]";
        }
    }
}