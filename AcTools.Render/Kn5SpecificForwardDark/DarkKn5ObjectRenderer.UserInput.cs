using System.Linq;
using AcTools.Render.Base.Cameras;
using AcTools.Render.Base.Objects;
using AcTools.Render.Kn5Specific.Objects;
using AcTools.Render.Kn5SpecificForwardDark.Lights;
using AcTools.Utils.Helpers;
using SlimDX;

namespace AcTools.Render.Kn5SpecificForwardDark {
    public partial class DarkKn5ObjectRenderer {
        protected override bool MoveObjectOverride(Vector2 relativeFrom, Vector2 relativeDelta, CameraBase camera, bool tryToClone) {
            return base.MoveObjectOverride(relativeFrom, relativeDelta, camera, tryToClone) ||
                    _complexMode && _lights.Any(light => {
                        if (light.IsMovable && light.Movable.MoveObject(relativeFrom, relativeDelta, camera, tryToClone, out var cloned)) {
                            if (cloned is DarkLightBase clonedLight) {
                                InsertLightAt(clonedLight, _lights.IndexOf(light));
                            }
                            return true;
                        }

                        return false;
                    });
        }

        protected override void StopMovementOverride() {
            base.StopMovementOverride();

            if (_complexMode) {
                for (var i = _lights.Length - 1; i >= 0; i--) {
                    var light = _lights[i];
                    if (light.Enabled && (light as DarkDirectionalLight)?.IsMainLightSource != true && _movingLights.All(x => x.Light != light)) {
                        light.Movable.StopMovement();
                    }
                }
            }
        }

        public void AutoFocus(Vector2 mousePosition) {
            var ray = Camera.GetPickingRay(mousePosition, new Vector2(ActualWidth, ActualHeight));
            var distance = Scene.SelectManyRecursive(x => x as RenderableList)
                                .OfType<IKn5RenderableObject>()
                                .Where(x => x.IsInitialized)
                                .Select(node => {
                                    var f = node.CheckIntersection(ray);
                                    return f.HasValue ? new {
                                        Node = node,
                                        Distance = f.Value
                                    } : null;
                                })
                                .Where(x => x != null)
                                .MinEntryOrDefault(x => x.Distance)?.Distance;
            if (distance.HasValue) {
                DofFocusPlane = distance.Value
                        * Vector3.Dot(Vector3.Normalize(Camera.Look), Vector3.Normalize(ray.Direction * distance.Value));
            }
        }

        protected override void OnClickSelect(IKn5RenderableObject selected) {
            var light = _lights.FirstOrDefault(x => x.Tag.IsCarTag && x.AttachedToSelect);
            if (light != null) {
                light.AttachedTo = selected.Name;
                light.AttachedToSelect = false;
            } else {
                base.OnClickSelect(selected);
            }
        }
    }
}