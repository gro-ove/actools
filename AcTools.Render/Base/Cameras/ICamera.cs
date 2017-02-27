using SlimDX;

namespace AcTools.Render.Base.Cameras {
    public interface ICamera {
        Vector3 Position { get; }

        Matrix ViewProj { get; }

        Matrix Proj { get; }

        Matrix ViewProjInvert { get; }

        bool Visible(BoundingBox box);

        FrustrumIntersectionType Intersect(BoundingBox box);
    }
}