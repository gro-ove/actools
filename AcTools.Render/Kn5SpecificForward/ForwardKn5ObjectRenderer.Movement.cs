using SlimDX;

namespace AcTools.Render.Kn5SpecificForward {
    public partial class ForwardKn5ObjectRenderer {
        private Vector2 _mousePosition;

        public Vector2 MousePosition {
            get { return _mousePosition; }
            set {
                if (Equals(value, _mousePosition)) return;
                _mousePosition = value;
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
            if (CarNode?.MoveObject(new Vector2(MousePosition.X / ActualWidth, MousePosition.Y / ActualHeight),
                    new Vector2(delta.X / ActualWidth, delta.Y / ActualHeight), Camera) != true) {
                return false;
            }

            AutoAdjustTarget = false;
            _carBoundingBox = null;
            IsDirty = true;
            SetShadowsDirty();
            SetReflectionCubemapDirty();
            return true;
        }

        public void StopMovement() {
            CarNode?.StopMovement();
        }
    }
}
