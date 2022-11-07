using System;
using System.Collections.Generic;
using AcTools.Kn5File;
using AcTools.Numerics;

namespace AcTools.ExtraKn5Utils.Kn5Utils {
    public class Kn5MeshBuilder {
        private readonly bool _considerNormal;
        private readonly bool _considerTexCoords;
        private readonly List<Kn5Node.Vertex> _vertices = new List<Kn5Node.Vertex>();
        private readonly List<ushort> _indices = new List<ushort>();
        private List<Vec2> _uv2;
        private Dictionary<long, ushort> _knownVertices = new Dictionary<long, ushort>();

        public int Count => _vertices.Count;

        public Kn5MeshBuilder(bool considerNormal = true, bool considerTexCoords = true) {
            _considerNormal = considerNormal;
            _considerTexCoords = considerTexCoords;
        }

        public void AddVertex(Kn5Node.Vertex v, Vec2? uv2 = null) {
            var hash = VertexHashCode(v);
            if (_knownVertices.TryGetValue(hash, out var knownIndex) && _vertices[knownIndex].Position.Equals(v.Position)) {
                _indices.Add(knownIndex);
            } else {
                if (_vertices.Count > 65535) throw new Exception("Limit exceeded");
                var newIndex = (ushort)_vertices.Count;
                _indices.Add(newIndex);
                _knownVertices[hash] = newIndex;
                _vertices.Add(v);
                if (uv2.HasValue) {
                    if (_uv2 == null) {
                        _uv2 = new List<Vec2>();
                        if (_vertices.Count != 1) throw new Exception("Vertex without UV2 has already be added");
                    }
                    _uv2.Add(uv2.Value);
                } else if (_uv2 != null) {
                    throw new Exception("Vertex expected to have UV2");
                }
            }
        }

        public bool IsCloseToLimit => _vertices.Count > 65500;

        public void SetTo(Kn5Node node) {
            node.Vertices = _vertices.ToArray();
            node.Indices = _indices.ToArray();
            node.Uv2 = _uv2?.ToArray();
        }

        public void Clear() {
            _vertices.Clear();
            _indices.Clear();
            _knownVertices.Clear();
        }

        private long VertexHashCode(Kn5Node.Vertex v, Vec2? uv2 = null) {
            unchecked {
                int r = v.Position.GetHashCode();
                if (_considerNormal) {
                    r = HashCodeHelper.CombineHashCodes(r, v.Normal.GetHashCode());
                }
                if (_considerTexCoords) {
                    r = HashCodeHelper.CombineHashCodes(r, v.Tex.GetHashCode());
                    if (uv2.HasValue) {
                        r = HashCodeHelper.CombineHashCodes(r, uv2.Value.GetHashCode());
                    }
                }
                return r;
            }
        }
    }
}