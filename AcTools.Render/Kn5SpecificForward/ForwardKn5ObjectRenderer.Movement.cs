using AcTools.Render.Base.Cameras;
using AcTools.Render.Base.Objects;
using SlimDX;

namespace AcTools.Render.Kn5SpecificForward {
    public partial class ForwardKn5ObjectRenderer : IMousePositionProvider {
        private bool _mousePositionSet;
        private Vector2 _mousePosition;

        public Vector2 MousePosition {
            get { return _mousePosition; }
            set {
                if (Equals(value, _mousePosition)) return;
                _mousePosition = value;

                if (!_mousePositionSet) {
                    _mousePositionSet = true;
                    DeviceContextHolder.Set<IMousePositionProvider>(this);
                }

                OnPropertyChanged();
            }
        }

        private bool _showMovementArrows;

        public bool ShowMovementArrows {
            get { return _showMovementArrows; }
            set {
                if (Equals(value, _showMovementArrows)) return;
                _showMovementArrows = value;
                OnPropertyChanged();
            }
        }

        public bool MoveObject(Vector2 delta) {
            if (!MoveObjectOverride(new Vector2(MousePosition.X / ActualWidth, MousePosition.Y / ActualHeight),
                    new Vector2(delta.X / ActualWidth, delta.Y / ActualHeight), Camera)) return false;

            AutoAdjustTarget = false;
            _carBoundingBox = null;
            IsDirty = true;
            SetShadowsDirty();
            SetReflectionCubemapDirty();
            return true;
        }

        public void StopMovement() {
            StopMovementOverride();
        }

        protected virtual bool MoveObjectOverride(Vector2 relativeFrom, Vector2 relativeDelta, BaseCamera camera) {
            return CarNode?.Movable.MoveObject(relativeFrom, relativeDelta, camera) == true;
        }

        protected virtual void StopMovementOverride() {
            CarNode?.Movable.StopMovement();
        }

        Vector2 IMousePositionProvider.GetRelative() {
            return new Vector2(MousePosition.X / ActualWidth, MousePosition.Y / ActualHeight);
        }
    }
}
