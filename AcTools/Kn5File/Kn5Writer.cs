using System;
using System.IO;
using System.Text;

namespace AcTools.Kn5File {
    internal sealed class Kn5Writer : BinaryWriter {
        public Kn5Writer(string filename)
            : this(File.Open(filename, FileMode.CreateNew)) {
        }

        public Kn5Writer(Stream output)
            : base(output) {
            Write("sc6969".ToCharArray());
        }

        public override void Write(string value) {
            Write(value.Length);
            Write(Encoding.ASCII.GetBytes(value));
        }

        public void Write(Kn5MaterialBlendMode blendMode) {
            Write((byte)blendMode);
        }

        public void Write(Kn5MaterialDepthMode depthMode) {
            Write((int)depthMode);
        }

        public void Write(Kn5NodeClass nodeClass) {
            Write((int)nodeClass);
        }

        public void Write(float[] values) {
            foreach (var t in values) {
                Write(t);
            }
        }

        public void Write(Kn5Header header) {
            Write(header.Version);

            if (header.Version > 5) {
                Write(header.Extra);
            }
        }

        public void Write(Kn5Texture texture) {
            Write(texture.Active ? 1 : 0);
            Write(texture.Name);
            Write(texture.Length);
        }

        public void Write(Kn5Material material) {
            Write(material.Name);
            Write(material.ShaderName);
            Write(material.BlendMode);
            Write(material.AlphaTested);
            Write(material.DepthMode);

            Write(material.ShaderProperties.Length);
            foreach (var property in material.ShaderProperties) {
                Write(property.Name);
                Write(property.ValueA);
                Write(property.ValueB);
                Write(property.ValueC);
                Write(property.ValueD);
            }
            
            Write(material.TextureMappings.Length);
            foreach (var mapping in material.TextureMappings) {
                Write(mapping.Name);
                Write(mapping.Slot);
                Write(mapping.Texture);
            }
        }

        public void Write(Kn5Node node) {
            Write(node.NodeClass);
            Write(node.Name);
            Write(node.Children.Count);
            Write(node.Active);

            switch (node.NodeClass) {
                case Kn5NodeClass.Base:
                    Write(node.Transform);
                    break;

                case Kn5NodeClass.Mesh:
                    Write(node.CastShadows);
                    Write(node.IsVisible);
                    Write(node.IsTransparent);

                    Write(node.Vertices.Length);
                    for (var i = 0; i < node.Vertices.Length; i++) {
                        Write(node.Vertices[i].Co);
                        Write(node.Vertices[i].Normal);
                        Write(node.Vertices[i].Uv);
                        Write(node.Vertices[i].Tangent);
                    }

                    Write(node.Indices.Length);
                    foreach (var t in node.Indices) {
                        Write(t);
                    }

                    Write(node.MaterialId);
                    Write(node.Layer);
                    
                    Write(node.LodIn);
                    Write(node.LodOut);
                    
                    Write(node.BoundingSphereCenter);
                    Write(node.BoundingSphereRadius);

                    Write(node.IsRenderable);
                    break;

                case Kn5NodeClass.SkinnedMesh:
                    throw new NotImplementedException();
            }
        }

        public void Insert(string filename) {
            Write(File.ReadAllBytes(filename));
        }
    }
}
