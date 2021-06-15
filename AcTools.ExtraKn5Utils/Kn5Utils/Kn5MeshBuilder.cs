using System;
using System.Collections.Generic;
using AcTools.Kn5File;

namespace AcTools.ExtraKn5Utils.Kn5Utils {
    public class Kn5MeshBuilder {
        private readonly List<Kn5Node.Vertex> _vertices = new List<Kn5Node.Vertex>();
        private readonly List<ushort> _indices = new List<ushort>();
        private Dictionary<long, ushort> _knownVertices = new Dictionary<long, ushort>();

        public int Count => _vertices.Count;

        public void AddVertex(Kn5Node.Vertex v) {
            if (_vertices.Count >= 65536) throw new Exception("Limit exceeded");

            var hash = VertexHashCode(v);
            if (_knownVertices.TryGetValue(hash, out var knownIndex)) {
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

        private static long VertexHashCode(Kn5Node.Vertex v) {
            long r = v.Position[0].GetHashCode();
            r = (r * 397) ^ v.Position[1].GetHashCode();
            r = (r * 397) ^ v.Position[2].GetHashCode();
            r = (r * 397) ^ v.Normal[0].GetHashCode();
            r = (r * 397) ^ v.Normal[1].GetHashCode();
            r = (r * 397) ^ v.Normal[2].GetHashCode();
            r = (r * 397) ^ v.TexC[0].GetHashCode();
            r = (r * 397) ^ v.TexC[1].GetHashCode();
            return r;
        }
    }
}