using System.IO;

namespace AcTools.Kn5File {
    public sealed class Kn5Writer : ExtendedBinaryWriter {
        public Kn5Writer(string filename) : base(filename) {
            Write("sc6969".ToCharArray());
        }

        public Kn5Writer(Stream output, bool leaveOpen) : base(output, leaveOpen) {
            Write("sc6969".ToCharArray());
        }

        public void Write(Kn5MaterialBlendMode blendMode) {
            base.Write((byte)blendMode);
        }

        public void Write(Kn5MaterialDepthMode depthMode) {
            base.Write((int)depthMode);
        }

        public void Write(Kn5NodeClass nodeClass) {
            base.Write((int)nodeClass);
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
            for (var i = 0; i < material.ShaderProperties.Length; i++) {
                var property = material.ShaderProperties[i];
                Write(property.Name);
                Write(property.ValueA);
                Write(property.ValueB);
                Write(property.ValueC);
                Write(property.ValueD);
            }

            Write(material.TextureMappings.Length);
            for (var i = 0; i < material.TextureMappings.Length; i++) {
                var mapping = material.TextureMappings[i];
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
                        var v = node.Vertices[i];
                        Write(v.Position);
                        Write(v.Normal);
                        Write(v.TexC);
                        Write(v.TangentU);
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
                    Write(node.CastShadows);
                    Write(node.IsVisible);
                    Write(node.IsTransparent);

                    Write(node.Bones.Length);
                    for (var i = 0; i < node.Bones.Length; i++) {
                        var b = node.Bones[i];
                        Write(b.Name);
                        Write(b.Transform);
                    }

                    Write(node.Vertices.Length);
                    for (var i = 0; i < node.Vertices.Length; i++) {
                        var v = node.Vertices[i];
                        Write(v.Position);
                        Write(v.Normal);
                        Write(v.TexC);
                        Write(v.TangentU);

                        var w = node.VerticeWeights[i];
                        Write(w.Weights);
                        Write(w.Indices);
                    }

                    Write(node.Indices.Length);
                    foreach (var t in node.Indices) {
                        Write(t);
                    }

                    Write(node.MaterialId);
                    Write(node.Layer);
                    Write(node.MisteryBytes ?? new byte[8]);
                    break;
            }
        }
    }
}
