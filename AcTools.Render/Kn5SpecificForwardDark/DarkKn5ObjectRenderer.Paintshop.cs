using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using AcTools.Render.Base.Objects;
using AcTools.Render.Kn5Specific.Objects;
using AcTools.Render.Kn5SpecificForward;
using AcTools.Utils;
using AcTools.Utils.Helpers;
using ImageMagick;
using SlimDX;

namespace AcTools.Render.Kn5SpecificForwardDark {
    public partial class DarkKn5ObjectRenderer : IPaintShopRenderer {
        public bool OverrideTexture(string textureName, byte[] textureBytes) {
            if (CarNode == null) return true;
            return CarNode.OverrideTexture(DeviceContextHolder, textureName, textureBytes);
        }

        public bool OverrideTexture(string textureName, Color color) {
            using (var image = new MagickImage(new MagickColor(color), 4, 4)) {
                return OverrideTexture(textureName, image.ToByteArray(MagickFormat.Bmp));
            }
        }

        public Task SaveTexture(string filename, Color color) {
            return SaveAndDispose(filename, new MagickImage(new MagickColor(color), 16, 16));
        }

        public bool OverrideTexture(string textureName, Color color, double alpha) {
            using (var image = new MagickImage(new MagickColor(color) { A = (ushort)(ushort.MaxValue * alpha) }, 4, 4)) {
                return OverrideTexture(textureName, image.ToByteArray(MagickFormat.Bmp));
            }
        }

        public Task SaveTexture(string filename, Color color, double alpha) {
            return SaveAndDispose(filename, new MagickImage(new MagickColor(color) { A = (ushort)(ushort.MaxValue * alpha) }, 16, 16));
        }

        public bool OverrideTextureFlakes(string textureName, Color color) {
            using (var image = new MagickImage(new MagickColor(color) { A = 250 }, 256, 256)) {
                image.AddNoise(NoiseType.Poisson, Channels.Alpha);
                return OverrideTexture(textureName, image.ToByteArray(MagickFormat.Png));
            }
        }

        public bool OverrideTextureMaps(string textureName, double reflection, double blur, double specular) {
            if (CarNode == null) return true;
            using (var image = new MagickImage(CarNode.OriginalFile.TexturesData[textureName])) {
                if (image.Width > 512 || image.Height > 512) {
                    image.Resize(512, 512);
                }

                image.BrightnessContrast(reflection, 1d, Channels.Red);
                image.BrightnessContrast(blur, 1d, Channels.Green);
                image.BrightnessContrast(specular, 1d, Channels.Blue);

                return OverrideTexture(textureName, image.ToByteArray(MagickFormat.Bmp));
            }
        }

        public Task SaveTextureFlakes(string filename, Color color) {
            var image = new MagickImage(new MagickColor(color) { A = 250 }, 256, 256);
            image.AddNoise(NoiseType.Poisson, Channels.Alpha);
            return SaveAndDispose(filename, image);
        }

        private Task SaveAndDispose(string filename, MagickImage image) {
            try {
                if (File.Exists(filename)) {
                    FileUtils.Recycle(filename);
                }

                image.SetDefine(MagickFormat.Dds, "compression", "none");
                image.Quality = 100;
                var bytes = image.ToByteArray(MagickFormat.Dds);
                return FileUtils.WriteAllBytesAsync(filename, bytes);
            } finally {
                image.Dispose();
            }
        }

        #region License plate
        private bool _licensePlateSelected;

        public bool LicensePlateSelected {
            get { return _licensePlateSelected; }
            set {
                if (Equals(value, _licensePlateSelected)) return;
                _licensePlateSelected = value;
                OnPropertyChanged();
            }
        }

        public void OnClick(Vector2 mousePosition) {
            var ray = Camera.GetPickingRay(mousePosition, new Vector2(Width, Height));

            var nodes = Scene.SelectManyRecursive(x => x as RenderableList)
                             .OfType<Kn5RenderableObject>()
                             .Where(node => {
                                 float d;
                                 return node.BoundingBox.HasValue && Ray.Intersects(ray, node.BoundingBox.Value, out d);
                             })
                             .Select(node => {
                                 var min = float.MaxValue;
                                 var found = false;

                                 var indices = node.Indices;
                                 var vertices = node.Vertices;
                                 var matrix = node.ParentMatrix;
                                 for (int i = 0, n = indices.Length / 3; i < n; i++) {
                                     var v0 = Vector3.TransformCoordinate(vertices[indices[i * 3]].Position, matrix);
                                     var v1 = Vector3.TransformCoordinate(vertices[indices[i * 3 + 1]].Position, matrix);
                                     var v2 = Vector3.TransformCoordinate(vertices[indices[i * 3 + 2]].Position, matrix);

                                     float distance;
                                     if (!Ray.Intersects(ray, v0, v1, v2, out distance) || distance >= min) continue;
                                     min = distance;
                                     found = true;
                                 }

                                 return found ? new {
                                     Node = node,
                                     Distance = min
                                 } : null;
                             })
                             .Where(x => x != null)
                             .OrderBy(x => x.Distance)
                             .Select(x => x.Node)
                             .ToList();

            if (nodes.Any()) {
                var first = nodes[0];
                try {
                    var material = CarNode?.OriginalFile.GetMaterial(first.OriginalNode.MaterialId);
                    if (material?.TextureMappings.Any(x => x.Texture == "Plate_D.dds") == true) {
                        LicensePlateSelected = true;
                    }
                } catch (Exception) {
                    LicensePlateSelected = false;
                }
            } else {
                LicensePlateSelected = false;
            }
        }

        public void Deselect() {
            LicensePlateSelected = false;
        }
        #endregion
    }
}