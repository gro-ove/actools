// https://www.nuget.org/packages/UkooLabs.FbxSharpie/
// https://github.com/UkooLabs/FBXSharpie
// License: MIT

using System.Collections.Generic;

namespace AcTools.ExtraKn5Utils.FbxUtils {
    public class FbxIndexer {
        private readonly List<long> _geometryIds;
        private readonly List<long> _materialIds;
        private readonly List<FbxVertex> _vertices;

        public FbxIndexer() {
            _geometryIds = new List<long>();
            _materialIds = new List<long>();
            _vertices = new List<FbxVertex>();
        }

        public void AddVertex(FbxVertex vertex) {
            _geometryIds.Add(0);
            _materialIds.Add(0);
            _vertices.Add(vertex);
        }

        public void AddVertex(FbxVertex vertex, long geometryId, long materialId) {
            _geometryIds.Add(geometryId);
            _materialIds.Add(materialId);
            _vertices.Add(vertex);
        }

        public void Index(out FbxVertex[] vertices, out int[] indices) {
            Index(0, 0, out vertices, out indices);
        }

        public void Index(long geometryId, long materialId, out FbxVertex[] vertices, out int[] indices) {
            var tempVertices = new List<FbxVertex>();
            var tempIndices = new List<int>();
            for (var i = 0; i < _vertices.Count; i++) {
                if (geometryId != _geometryIds[i] || materialId != _materialIds[i]) {
                    continue;
                }
                var vertex = _vertices[i];
                if (tempVertices.Contains(vertex)) {
                    var index = tempVertices.IndexOf(vertex);
                    tempIndices.Add(index);
                } else {
                    tempIndices.Add(tempVertices.Count);
                    tempVertices.Add(vertex);
                }
            }
            vertices = tempVertices.ToArray();
            indices = tempIndices.ToArray();
        }
    }
}