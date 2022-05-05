using System;
using System.Collections.Generic;
using AcTools.Kn5File;

namespace AcTools.ExtraKn5Utils.Kn5Utils {
    public class Kn5MeshBuilder {
        private readonly bool _considerNormal;
        private readonly bool _considerTexCoords;
        private readonly List<Kn5Node.Vertex> _vertices = new List<Kn5Node.Vertex>();
        private readonly List<ushort> _indices = new List<ushort>();
        private Dictionary<long, ushort> _knownVertices = new Dictionary<long, ushort>();

        public int Count => _vertices.Count;

        public Kn5MeshBuilder(bool considerNormal = true, bool considerTexCoords = true) {
            _considerNormal = considerNormal;
            _considerTexCoords = considerTexCoords;
        }

        public void AddVertex(Kn5Node.Vertex v) {
            if (_vertices.Count >= 65536) throw new Exception("Limit exceeded");

            var hash = VertexHashCode(v);
            if (_knownVertices.TryGetValue(hash, out var knownIndex) && _vertices[knownIndex].Position.Equals(v.Position)) {
                _indices.Add(knownIndex);
            } else {
                var newIndex = (ushort)_vertices.Count;
                _indices.Add(newIndex);
                _knownVertices[hash] = newIndex;
                _vertices.Add(v);
            }
        }

        public bool IsCloseToLimit => _vertices.Count > 65500;

        public void SetTo(Kn5Node node) {
            node.Vertices = _vertices.ToArray();
            node.Indices = _indices.ToArray();
        }

        public void Clear() {
            _vertices.Clear();
            _indices.Clear();
            _knownVertices.Clear();
        }

        private long VertexHashCode(Kn5Node.Vertex v) {
            unchecked {
                long r = v.Position.GetHashCode();
                if (_considerNormal) {
                    r = (r * 397) ^ v.Normal.GetHashCode();
                }
                if (_considerTexCoords) {
                    r = (r * 397) ^ v.Tex.GetHashCode();
                }
                return r;
            }
        }
    }
}