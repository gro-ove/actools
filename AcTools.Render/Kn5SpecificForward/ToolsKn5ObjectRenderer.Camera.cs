using System;
using System.Linq;
using AcTools.Render.Base.Cameras;
using AcTools.Render.Base.Objects;
using AcTools.Render.Base.Utils;
using AcTools.Render.Kn5Specific.Objects;
using AcTools.Utils;
using SlimDX;

namespace AcTools.Render.Kn5SpecificForward {
    public partial class ToolsKn5ObjectRenderer {
        public event EventHandler CameraMoved;
        private Matrix _cameraView;
        private bool _cameraIgnoreNext;
        private Vector3 _cameraTo;
        private Vector3 _showroomOffset;

        public void SetCamera(Vector3 lookFrom, Vector3 lookAt, float fovRadY, float tiltRad) {
            UseFpsCamera = true;
            AutoRotate = false;
            Camera = new FpsCamera(fovRadY);

            _cameraTo = lookAt;
            Camera.LookAt(lookFrom, lookAt, tiltRad);
            Camera.SetLens(AspectRatio);
            //Camera.UpdateViewMatrix();

            PrepareCamera(Camera);
            IsDirty = true;
            _cameraIgnoreNext = true;
        }

        public void SetCameraOrbit(Vector3 lookFrom, Vector3 lookAt, float fovRadY, float tiltRad) {
            UseFpsCamera = false;
            AutoRotate = false;
            Camera = new CameraOrbit(fovRadY);

            _cameraTo = lookAt;
            Camera.LookAt(lookFrom, lookAt, tiltRad);
            Camera.SetLens(AspectRatio);
            //Camera.UpdateViewMatrix();

            PrepareCamera(Camera);
            IsDirty = true;
            _cameraIgnoreNext = true;
        }

        public void AlignCar() {
            if (Camera is FpsCamera camera) {
                var boundingBox = MainSlot.CarNode?.GetAllChildren().OfType<Kn5RenderableObject>().Where(x => x.Name?.StartsWith("CINTURE_ON") != true)
                                          .Aggregate(new BoundingBox(), (a, b) => {
                                              b.BoundingBox?.ExtendBoundingBox(ref a);
                                              return a;
                                          });
                if (boundingBox.HasValue) {
                    var offset = boundingBox.Value.GetCenter();
                    camera.LookAt(camera.Position + offset, _cameraTo += offset, camera.Tilt);
                    camera.SetLens(AspectRatio);

                    offset.Y = 0;
                    _showroomOffset = offset;
                    if (ShowroomNode != null) {
                        ShowroomNode.LocalMatrix = Matrix.Translation(_showroomOffset);
                    }

                    IsDirty = true;
                }
            }
        }

        private void GetCameraOffsetForCenterAlignmentUsingVertices_ProcessObject(Kn5RenderableObject obj, Matrix matrix, ref Vector3 min, ref Vector3 max) {
            if (min.Z != 0f && obj.BoundingBox.HasValue) {
                var corners = obj.BoundingBox.Value.GetCorners();
                foreach (var c in corners) {
                    var sp = Vector3.TransformCoordinate(c, matrix);
                    if (sp.X < min.X || sp.Y < min.Y || sp.Z < min.Z ||
                            sp.X > max.X || sp.Y > max.Y || sp.Z > max.Z) {
                        goto NextStep;
                    }
                }

                return;
            }

            NextStep:
            var wm = obj.ParentMatrix * matrix;
            var vertices = obj.Vertices;
            for (var i = 0; i < vertices.Length; i++) {
                var sp = Vector3.TransformCoordinate(vertices[i].Position, wm);
                if (min.Z == 0f) {
                    min = sp;
                    max = sp;
                } else {
                    if (sp.X < min.X) min.X = sp.X;
                    if (sp.Y < min.Y) min.Y = sp.Y;
                    if (sp.Z < min.Z) min.Z = sp.Z;
                    if (sp.X > max.X) max.X = sp.X;
                    if (sp.Y > max.Y) max.Y = sp.Y;
                    if (sp.Z > max.Z) max.Z = sp.Z;
                }
            }
        }

        private void GetCameraOffsetForCenterAlignmentUsingVertices_ProcessList(RenderableList list, Matrix matrix, ref Vector3 min, ref Vector3 max) {
            for (var i = 0; i < list.Count; i++) {
                var child = list[i];
                switch (child) {
                    case RenderableList li:
                        GetCameraOffsetForCenterAlignmentUsingVertices_ProcessList(li, matrix, ref min, ref max);
                        break;
                    case Kn5RenderableObject ro:
                        GetCameraOffsetForCenterAlignmentUsingVertices_ProcessObject(ro, matrix, ref min, ref max);
                        break;
                }
            }
        }

        private Vector3 GetCameraOffsetForCenterAlignmentUsingVertices(ICamera camera, bool x, float xOffset, bool xOffsetRelative, bool y, float yOffset,
                bool yOffsetRelative) {
            if (CarNode == null) return Vector3.Zero;

            var matrix = camera.ViewProj;
            var min = Vector3.Zero;
            var max = Vector3.Zero;
            GetCameraOffsetForCenterAlignmentUsingVertices_ProcessList(CarNode.RootObject, matrix, ref min, ref max);

            var center = (min + max) / 2f;
            var offsetScreen = center;
            var targetScreen = new Vector3(xOffset, yOffset, offsetScreen.Z);

            if (!x) {
                targetScreen.X = offsetScreen.X;
            } else if (xOffsetRelative) {
                if (xOffset > 0) {
                    var left = (0.5f - max.X / 2f).Saturate();
                    targetScreen.X = xOffset * left;
                } else {
                    var left = (min.X / 2f + 0.5f).Saturate();
                    targetScreen.X = xOffset * left;
                }
            }

            if (!y) {
                targetScreen.Y = offsetScreen.Y;
            } else if (yOffsetRelative) {
                if (yOffset > 0) {
                    var left = (0.5f - max.Y / 2f).Saturate();
                    targetScreen.Y = yOffset * left;
                } else {
                    var left = (min.Y / 2f + 0.5f).Saturate();
                    targetScreen.Y = yOffset * left;
                }
            }

            return Vector3.TransformCoordinate(offsetScreen, camera.ViewProjInvert) -
                    Vector3.TransformCoordinate(targetScreen, camera.ViewProjInvert);
        }

        public void AlignCamera(bool x, float xOffset, bool xOffsetRelative, bool y, float yOffset, bool yOffsetRelative) {
            if (!x && !y) return;

            var camera = Camera;
            if (camera == null) return;

            camera.SetLens(AspectRatio);
            camera.UpdateViewMatrix();

            var offset = GetCameraOffsetForCenterAlignmentUsingVertices(camera, x, xOffset, xOffsetRelative, y, yOffset, yOffsetRelative);
            camera.LookAt(camera.Position + offset, _cameraTo += offset, camera.Tilt);

            offset.Y = 0;
            _showroomOffset += offset;
            if (ShowroomNode != null) {
                ShowroomNode.LocalMatrix = Matrix.Translation(_showroomOffset);
            }

            IsDirty = true;
        }

        private void TestCameraMoved() {
            if (AccumulatedFrame > 1) return;

            var cameraMoved = CameraMoved;
            if (cameraMoved != null) {
                Camera.UpdateViewMatrix();

                if (_cameraView != Camera.ViewProj) {
                    _cameraView = Camera.ViewProj;

                    if (_cameraIgnoreNext) {
                        _cameraIgnoreNext = false;
                    } else {
                        cameraMoved.Invoke(this, EventArgs.Empty);
                        Camera.UpdateViewMatrix();
                        _cameraView = Camera.ViewProj;
                    }
                }
            }
        }
    }
}