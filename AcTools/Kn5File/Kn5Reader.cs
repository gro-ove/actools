using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace AcTools.Kn5File {
    internal sealed class Kn5Reader : BinaryReader {
        public Kn5Reader(string filename)
            : this(File.Open(filename, FileMode.Open, FileAccess.Read, FileShare.Read)) {
        }

        public Kn5Reader(Stream input, bool withoutHeader = false)
            : base(input) {
            if (!withoutHeader && new string(ReadChars(6)) != "sc6969") {
                throw new Exception("Not a valid KN5 file.");
            }
        }

        public override string ReadString() {
            var length = ReadInt32();
            return Encoding.ASCII.GetString(ReadBytes(length));
        }

        public Kn5MaterialBlendMode ReadBlendMode() {
            return (Kn5MaterialBlendMode)ReadByte();
        }

        public Kn5MaterialDepthMode ReadDepthMode() {
            return (Kn5MaterialDepthMode)ReadInt32();
        }

        public float[] ReadSingle2D() {
            return new[] {
                ReadSingle(), ReadSingle()
            };
        }

        public float[] ReadSingle3D() {
            return new[] {
                ReadSingle(), ReadSingle(), ReadSingle()
            };
        }

        public float[] ReadSingle4D() {
            return new[] {
                ReadSingle(), ReadSingle(),
                ReadSingle(), ReadSingle()
            };
        }

        public Kn5NodeClass ReadNodeClass() {
            return (Kn5NodeClass)ReadInt32();
        }
        
        /// <summary>
        /// Read 64 bytes as 16 floats.
        /// </summary>
        /// <returns></returns>
        public float[] ReadMatrix() {
            return new[] {
                ReadSingle(), ReadSingle(), ReadSingle(), ReadSingle(),
                ReadSingle(), ReadSingle(), ReadSingle(), ReadSingle(),
                ReadSingle(), ReadSingle(), ReadSingle(), ReadSingle(),
                ReadSingle(), ReadSingle(), ReadSingle(), ReadSingle(),
            };
        }

        public Kn5Header ReadHeader() {
            var header = new Kn5Header {
                Version = ReadInt32()
            };

            header.Extra = header.Version > 5 ? ReadInt32() : 0;
            return header;
        }

        public Kn5Texture ReadTexture() {
            return new Kn5Texture {
                Active = ReadInt32() == 1,
                Name = ReadString(),
                Length = (int)ReadUInt32()
            };
        }

        public Kn5Material ReadMaterial() {
            var material = new Kn5Material {
                Name = ReadString(),
                ShaderName = ReadString(),
                BlendMode = ReadBlendMode(), // byte
                AlphaTested = ReadBoolean(), // bool
                DepthMode = ReadDepthMode(), // int32
                ShaderProperties = new Kn5Material.ShaderProperty[ReadInt32()]
            };

            for (var i = 0; i < material.ShaderProperties.Length; i++) {
                material.ShaderProperties[i] = new Kn5Material.ShaderProperty {
                    Name = ReadString(),
                    ValueA = ReadSingle(),
                    ValueB = ReadSingle2D(),
                    ValueC = ReadSingle3D(),
                    ValueD = ReadSingle4D()
                };
            }

            material.TextureMappings = new Kn5Material.TextureMapping[ReadInt32()];
            for (var i = 0; i < material.TextureMappings.Length; i++) {
                material.TextureMappings[i] = new Kn5Material.TextureMapping {
                    Name = ReadString(),
                    Slot = ReadInt32(),
                    Texture = ReadString()
                };
            }

            return material;
        }

        public Kn5Node ReadNode() {
            var nodeClass = ReadNodeClass();
            var nodeName = ReadString();
            var nodeChildren = ReadInt32();
            var nodeActive = ReadBoolean();

            var node = new Kn5Node {
                NodeClass = nodeClass,
                Name = nodeName,
                Children = new List<Kn5Node>(nodeChildren),
                Active = nodeActive
            };

            switch (node.NodeClass) {
                case Kn5NodeClass.Base:
                    node.Transform = ReadMatrix();
                    break;

                case Kn5NodeClass.Mesh:
                    node.CastShadows = ReadBoolean();
                    node.IsVisible = ReadBoolean();
                    node.IsTransparent = ReadBoolean();
            
                    node.Vertices = new Kn5Node.Vertice[ReadUInt32()];
                    for (var i = 0; i < node.Vertices.Length; i++) {
                        // 44 bytes per vertice
                        node.Vertices[i] = new Kn5Node.Vertice {
                            Co = ReadSingle3D(),
                            Normal = ReadSingle3D(),
                            Uv = ReadSingle2D(),
                            Tangent = ReadSingle3D()
                        };
                    }
            
                    node.Indices = new ushort[ReadUInt32()];
                    for (var i = 0; i < node.Indices.Length; i++) {
                        node.Indices[i] = ReadUInt16();
                    }
            
                    node.MaterialId = ReadUInt32();
                    node.Layer = ReadUInt32();
                    
                    node.LodIn = ReadSingle();
                    node.LodOut = ReadSingle();
                    
                    node.BoundingSphereCenter = ReadSingle3D();
                    node.BoundingSphereRadius = ReadSingle();

                    node.IsRenderable = ReadBoolean();
                    break;

                case Kn5NodeClass.SkinnedMesh:
                    node.CastShadows = ReadBoolean();
                    node.IsVisible = ReadBoolean();
                    node.IsTransparent = ReadBoolean();
            
                    node.Bones = new Kn5Node.Bone[ReadUInt32()];
                    for (var i = 0; i < node.Bones.Length; i++) {
                        node.Bones[i] = new Kn5Node.Bone {
                            Name = ReadString(),
                            Transform = ReadMatrix()
                        };
                    }
            
                    node.Vertices = new Kn5Node.Vertice[ReadUInt32()];
                    node.VerticeWeights = new Kn5Node.VerticeWeight[node.Vertices.Length];
                    for (var i = 0; i < node.Vertices.Length; i++) {
                        // 76 bytes per vertice
                        node.Vertices[i] = new Kn5Node.Vertice {
                            Co = ReadSingle3D(),
                            Normal = ReadSingle3D(),
                            Uv = ReadSingle2D(),
                            Tangent = ReadSingle3D()
                        };

                        node.VerticeWeights[i] = new Kn5Node.VerticeWeight {
                            Weights = ReadSingle4D(),

                            // Yes! Those are floats!
                            Indices = ReadSingle4D()
                        };
                    }
            
                    node.Indices = new ushort[ReadUInt32()];
                    for (var i = 0; i < node.Indices.Length; i++) {
                        node.Indices[i] = ReadUInt16();
                    }

                    node.MaterialId = ReadUInt32();
                    node.Layer = ReadUInt32();
                    
                    ReadBytes(8); // the only mistery left?
                    node.IsRenderable = true;
                    break;
            }

            return node;
        }

        public void Extract(int length, string filename) {
            var bytes = ReadBytes(length);
            if (filename == null) return;
            using (var output = File.Create(filename)) {
                output.Write(bytes, 0, length);
            }
        }
    }
}
