using System.Drawing;

namespace AcTools.Render.Kn5SpecificForwardDark.Materials {
    public class DarkMaterialsParams {
        public TesselationMode TesselationMode;
        public WireframeMode WireframeMode;
        public bool IsWireframeColored = true;
        public Color WireframeColor = Color.White;
        public float WireframeBrightness = 2.5f;
        public bool IsMirrored;
        public bool MeshDebugWithEmissive = true;
    }
}