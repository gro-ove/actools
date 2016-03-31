using System.Drawing;
using SlimDX.Direct3D11;
using SlimDX.DXGI;
using Resource = SlimDX.Direct3D11.Resource;

namespace AcTools.Kn5Render.Kn5Render {
    public partial class Render : System.IDisposable {
        public Image Shot(int width, int height, bool asJpeg = false){
            _width = width;
            _height = height;

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

            using (var stream = new System.IO.MemoryStream()) { 
                Texture2D.ToStream(_context, tmpTexture, asJpeg ? ImageFileFormat.Jpg : ImageFileFormat.Png, stream);
                tmpRenderTarget.Dispose();
                tmpTexture.Dispose();

                stream.Position = 0;
                return Image.FromStream(stream);
            }
        }

        public void ShotToFile(int width, int height, string outputFile){
            _width = width;
            _height = height;

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

            Resource.SaveTextureToFile(_context, tmpTexture, outputFile.EndsWith(".jpg") ? ImageFileFormat.Jpg : 
                outputFile.EndsWith(".bmp") ? ImageFileFormat.Bmp : ImageFileFormat.Png, outputFile);
            tmpRenderTarget.Dispose();
            tmpTexture.Dispose();
        }
    }
}
