using System;
using AcTools.ExtraKn5Utils.FbxUtils;
using AcTools.ExtraKn5Utils.FbxUtils.Extensions;
using AcTools.Numerics;

namespace AcTools.ExtraKn5Utils.Helpers {
    public class FbxDataAccessor {
        private float[] _data;
        private int[] _indices;
        private int _mappingMode;
        private int _hashCode = -1;

        public FbxDataAccessor(FbxNode node, string key) {
            _data = node.GetRelative(key)?.Value.GetAsFloatArray();
            if (node == null || _data == null) {
                throw new Exception($"Data is missing: {key}");
            }

            var mappingMode = node.GetRelative("MappingInformationType").Value.GetAsString();
            var referenceMode = node.GetRelative("ReferenceInformationType").Value.GetAsString();
            if (referenceMode == "IndexToDirect") {
                _indices = node.GetRelative(key + "Index")?.Value.GetAsIntArray();
                if (_indices == null) {
                    // node.Dump();
                }
            } else if (referenceMode != "Direct") {
                throw new Exception($"Unsupported referenceMode: {referenceMode}");
            }

            if (mappingMode == "ByPolygonVertex") {
                _mappingMode = 0;
            } else if (mappingMode == "ByPolygon") {
                _mappingMode = 1;
            } else if (mappingMode == "AllSame") {
                _mappingMode = 2;
            } else {
                throw new Exception($"Unsupported mappingMode: {mappingMode}");
            }
        }

        private int GetIndex(int index) {
            if (_mappingMode == 1) index /= 3;
            if (_mappingMode == 2) index = 0;
            if (_indices != null) index = _indices[index];
            return index;
        }

        public float GetFloat(int index) {
            return _data[GetIndex(index)];
        }

        public Vec2 GetVec2(int index) {
            index = GetIndex(index);
            return new Vec2(_data[index * 2], 1f - _data[index * 2 + 1]);
        }

        public Vec3 GetVec3(int index, bool flipYZ = false) {
            index = GetIndex(index);
            return new Vec3(_data[index * 3], _data[index * 3 + (flipYZ ? 2 : 1)], _data[index * 3 + (flipYZ ? 1 : 2)]);
        }

        public Vec4 GetVec4(int index) {
            index = GetIndex(index);
            return new Vec4(_data[index * 4], _data[index * 4 + 1], _data[index * 4 + 2], _data[index * 4 + 3]);
        }

        public static unsafe int AsInt(float v) {
            return *(int*)&v;
        }

        public int GetDataHashCode() {
            if (_hashCode == -1) {
                var value = _data.Length;
                for (var i = _data.Length - 1; i >= 0; i--) {
                    value = ((value << 5) + value) ^ AsInt(_data[i]);
                }
                _hashCode = value == -1 ? 0 : value;
            }
            return _hashCode;
        }
    }
}