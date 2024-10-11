using System;
using System.Windows.Forms;
using AcTools.Render.Base;
using AcTools.Render.Base.Utils;
using AcTools.Render.Kn5Specific;
using AcTools.Windows;
using SlimDX;

namespace AcTools.Render.Wrapper {
    public class Kn5WrapperCameraControlHelper {
        public virtual void CameraMousePan(IKn5ObjectRenderer renderer, double dx, double dy, double height, double width) {
            if (renderer.LockCamera) return;

            var size = 4.0 / Math.Min(height, width);
            dx *= size;
            dy *= size;

            var c = renderer.CameraOrbit;
            if (c != null) {
                c.Target -= (float)dy * Vector3.Cross(c.Look, c.Right) + (float)dx * c.Right;
                // renderer.AutoRotate = false;
                renderer.AutoAdjustTarget = false;
                ((BaseRenderer)renderer).IsDirty = true;
            } else {
                var f = renderer.FpsCamera;
                if (f != null) {
                    f.Position -= (float)dy * Vector3.Cross(f.Look, f.Right) + (float)dx * f.Right;
                    ((BaseRenderer)renderer).IsDirty = true;
                }
            }
        }

        public virtual void CameraMouseRotate(IKn5ObjectRenderer renderer, double dx, double dy, double height, double width) {
            if (renderer.LockCamera) return;

            var size = (renderer.UseFpsCamera ? 140d : 180d) / Math.Min(height, width);
            dx *= size;
            dy *= size;

            renderer.Camera.Pitch(((float)(renderer.UseFpsCamera ? -dy : dy)).ToRadians());
            renderer.Camera.Yaw(((float)(renderer.UseFpsCamera ? -dx : dx)).ToRadians());
            // renderer.AutoRotate = false;
            ((BaseRenderer)renderer).IsDirty = true;
        }

        public virtual void CameraMouseZoom(IKn5ObjectRenderer renderer, double dx, double dy, double height, double width) {
            if (renderer.LockCamera) return;

            var size = 9.0 / Math.Min(height, width);
            dy *= size;

            renderer.Camera.Zoom((float)dy);
            // renderer.AutoRotate = false;
            ((BaseRenderer)renderer).IsDirty = true;
        }

        private bool IsPressed(Keys key) {
            return User32.IsAsyncKeyPressed(key);
        }

        public virtual void OnTick(IKn5ObjectRenderer renderer, float deltaTime) {
            if (IsPressed(Keys.LMenu) || IsPressed(Keys.RMenu)) return;
            if (renderer.LockCamera) return;

            var speed = 0.1f;
            if (IsPressed(Keys.LShiftKey) || IsPressed(Keys.RShiftKey)) speed *= 0.2f;
            if (IsPressed(Keys.LControlKey) || IsPressed(Keys.RControlKey)) speed = 5.0f;

            if (IsPressed(Keys.Up)) {
                renderer.Camera.Walk(speed);
                renderer.AutoRotate = false;
                renderer.AutoAdjustTarget = false;
                ((BaseRenderer)renderer).IsDirty = true;
            }

            if (IsPressed(Keys.Down)) {
                renderer.Camera.Walk(-speed);
                renderer.AutoRotate = false;
                renderer.AutoAdjustTarget = false;
                ((BaseRenderer)renderer).IsDirty = true;
            }

            if (IsPressed(Keys.Left)) {
                renderer.Camera.Strafe(-speed);
                renderer.AutoRotate = false;
                renderer.AutoAdjustTarget = false;
                ((BaseRenderer)renderer).IsDirty = true;
            }

            if (IsPressed(Keys.Right)) {
                renderer.Camera.Strafe(speed);
                renderer.AutoRotate = false;
                renderer.AutoAdjustTarget = false;
                ((BaseRenderer)renderer).IsDirty = true;
            }
        }
    }
}