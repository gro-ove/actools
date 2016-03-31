using System;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using AcTools.DataFile;
using AcTools.Kn5Render.Kn5Render.Effects;
using AcTools.Utils;
using SlimDX;
using SlimDX.Direct3D11;
using SlimDX.DXGI;
using Resource = SlimDX.Direct3D11.Resource;

namespace AcTools.Kn5Render.Kn5Render {
    [Serializable]
    public class ShotException : Exception {
        public ShotException(string message) : base(message) { }
    }

    public partial class Render {
        public class TrackMapInformation {
            public float Width, Height,
                XOffset, ZOffset,
                Margin = 20.0f, ScaleFactor = 1.0f, DrawingSize = 10.0f;


            public void SaveTo(string filename) {
                var file = new IniFile();

                file["PARAMETERS"]["WIDTH"] = Width.ToString(CultureInfo.InvariantCulture);
                file["PARAMETERS"]["HEIGHT"] = Height.ToString(CultureInfo.InvariantCulture);

                file["PARAMETERS"]["X_OFFSET"] = XOffset.ToString(CultureInfo.InvariantCulture);
                file["PARAMETERS"]["Z_OFFSET"] = ZOffset.ToString(CultureInfo.InvariantCulture);

                file["PARAMETERS"]["MARGIN"] = Margin.ToString(CultureInfo.InvariantCulture);
                file["PARAMETERS"]["SCALE_FACTOR"] = ScaleFactor.ToString(CultureInfo.InvariantCulture);
                file["PARAMETERS"]["DRAWING_SIZE"] = DrawingSize.ToString(CultureInfo.InvariantCulture);

                file.Save(filename);
            }
        }

        private const float DefaultMargin = 2;

        public void ShotTrackMap(string outputFile, Regex surfaceFilter, out TrackMapInformation information) {
            information = new TrackMapInformation { Margin = DefaultMargin };

            var bb = new BoundingBox(new Vector3(float.MaxValue), new Vector3(float.MinValue));

            var oldObjs = _objs;
            _objs = _objs.Where(x => surfaceFilter.IsMatch(x.Name)).ToList();

            try {
                foreach (var obj in _objs) {
                    if (obj.Blen != null) {
                        obj.Blen.Dispose();
                        obj.Blen = null;
                    }

                    obj.Mat.MinAlpha = 1.0f;
                    obj.Mat.Diffuse = obj.Mat.Ambient = new Color4(1.0f, 0.0f, 0.0f, 0.0f);

                    var meshObj = obj as MeshObjectKn5;
                    if (meshObj == null) continue;

                    bb = bb.Expand(meshObj.MeshBox);
                }

                if (bb.Minimum.X > bb.Maximum.X) {
                    throw new ShotException("Surface not found");
                }

                Console.WriteLine(@"BB: {0} / {1}", bb.Minimum, bb.Maximum);

                _width = (int)(bb.GetSizeX() + DefaultMargin * 2);
                _height = (int)(bb.GetSizeZ() + DefaultMargin * 2);

                information.Width = bb.GetSizeX() + DefaultMargin * 2;
                information.Height = bb.GetSizeZ() + DefaultMargin * 2;
                information.XOffset = bb.Maximum.X + DefaultMargin;
                information.ZOffset = -bb.Minimum.Z - DefaultMargin;

                ShotTrackMap_SetCamera(bb);

                DxResize();
                DrawFrame();

                var tmpTexture = new Texture2D(CurrentDevice, new Texture2DDescription {
                    Width = _width,
                    Height = _height,
                    MipLevels = 1,
                    ArraySize = 1,
                    Format = Format.R8G8B8A8_UNorm,
                    SampleDescription = new SampleDescription(1, 0),
                    Usage = ResourceUsage.Default,
                    BindFlags = BindFlags.ShaderResource | BindFlags.RenderTarget,
                    CpuAccessFlags = CpuAccessFlags.None,
                    OptionFlags = ResourceOptionFlags.None
                });

                var tmpRenderTarget = new RenderTargetView(CurrentDevice, tmpTexture);
                DrawPreviousFrameTo(tmpRenderTarget);
                BlurSomething(tmpTexture, 1, 1.0f);
                ShotTrackMap_Process(tmpTexture);

                Resource.SaveTextureToFile(_context, tmpTexture, ImageFileFormat.Png, outputFile);
                tmpRenderTarget.Dispose();
                tmpTexture.Dispose();

            } finally {
                _objs = oldObjs;
            }
        }

        void ShotTrackMap_SetCamera(BoundingBox box) {
            _camera = new CameraOrtho() {
                Position = new Vector3(box.GetCenter().X, box.Maximum.Y + DefaultMargin, box.GetCenter().Z),
                FarZ = box.GetSizeY() + DefaultMargin * 2,
                Target = box.GetCenter(),
                Up = new Vector3(0.0f, 0.0f, -1.0f),
                Width = box.GetSizeX() + DefaultMargin * 2,
                Height = box.GetSizeZ() + DefaultMargin * 2
            };
        }

        void ShotTrackMap_Process(Texture2D texture) {
            var temporary = new Texture2D(CurrentDevice, texture.Description);
            var sra = new ShaderResourceView(CurrentDevice, texture);
            var srb = new ShaderResourceView(CurrentDevice, temporary);
            var rta = new RenderTargetView(CurrentDevice, texture);
            var rtb = new RenderTargetView(CurrentDevice, temporary);

            var effectTrackMap = new EffectShotTrackMap(CurrentDevice);

            DrawSomethingFromTo(sra, rtb, effectTrackMap.LayoutPT, effectTrackMap.FxInputImage, effectTrackMap.TechTrackMap);
            DrawSomethingFromTo(srb, rta, _effectScreen.TechCopy);

            effectTrackMap.Dispose();

            sra.Dispose();
            srb.Dispose();
            rta.Dispose();
            rtb.Dispose();
        }
    }
}
