// #define BB_PERF_PROFILE

using System;
using System.IO;
using System.Linq;
using AcTools.Render.Base.Objects;
using AcTools.Render.Data;
using AcTools.Render.Kn5Specific.Animations;
using JetBrains.Annotations;

namespace AcTools.Render.Kn5Specific.Objects {
    public partial class Kn5RenderableCar {
        [CanBeNull]
        public static KsAnimAnimator CreateAnimator([NotNull] string rootDirectory, [NotNull] string animName, float duration = 1f,
                bool clampEnabled = true, bool skipFixed = true) {
            return CreateAnimator(Path.Combine(rootDirectory, "animations", animName), duration, clampEnabled, skipFixed);
        }

        [CanBeNull]
        public static KsAnimAnimator CreateAnimator([NotNull] string rootDirectory, CarData.AnimationBase animation, bool clampEnabled = true,
                bool skipFixed = true) {
            return CreateAnimator(Path.Combine(rootDirectory, "animations", animation.KsAnimName), animation.Duration, clampEnabled, skipFixed);
        }

        [CanBeNull]
        public static KsAnimAnimator CreateAnimator([NotNull] string filename, float duration = 1f, bool clampEnabled = true, bool skipFixed = true) {
            try {
                return new KsAnimAnimator(filename, duration, skipFixed) {
                    ClampPosition = clampEnabled
                };
            } catch (Exception e) {
                AcToolsLogging.Write(e);
                return null;
            }
        }

        public static bool SetCockpitLrActive([NotNull] RenderableList parent, bool value) {
            var changed = false;
            foreach (var child in parent.GetAllChildren().OfType<Kn5RenderableList>()) {
                switch (child.OriginalNode.Name) {
                    case "COCKPIT_LR":
                    case "STEER_LR":
                    case "SHIFT_LD":
                        if (child.IsEnabled != value) {
                            child.IsEnabled = value;
                            changed = true;
                        }
                        break;
                    case "COCKPIT_HR":
                    case "STEER_HR":
                    case "SHIFT_HD":
                        if (child.IsEnabled != !value) {
                            child.IsEnabled = !value;
                            changed = true;
                        }
                        break;
                }
            }

            return changed;
        }

        public static bool SetSeatbeltActive([NotNull] RenderableList parent, bool value) {
            var changed = false;

            var onNode = parent.GetDummyByName("CINTURE_ON");
            if (onNode != null) {
                if (onNode.IsEnabled != value) {
                    onNode.IsEnabled = value;
                    changed = true;
                }
            }

            var offNode = parent.GetDummyByName("CINTURE_OFF");
            if (offNode != null) {
                if (offNode.IsEnabled != !value) {
                    offNode.IsEnabled = !value;
                    changed = true;
                }
            }

            return changed;
        }

        public static bool SetBlurredObjects([NotNull] IRenderableObject parent, CarData.BlurredObject[] blurredObjects, float speed) {
            var c = parent as Kn5RenderableCar;
            var getDummyByName = c != null ? (Func<string, RenderableList>)c.GetDummyByName :
                ((RenderableList)parent).GetDummyByName;

            var changed = false;
            foreach (var o in CarData.BlurredObject.GetNamesToToggle(blurredObjects, speed)) {
                var node = getDummyByName(o.Item1);
                if (node.IsEnabled != o.Item2) {
                    node.IsEnabled = o.Item2;
                    changed = true;
                }
            }

            return changed;
        }

        public void UpdateCurrentLodKn5Values() {
            var kn5 = GetCurrentLodKn5();
            if (kn5 == null) return;

            foreach (var list in _currentLodObject.Renderable.GetAllChildren().OfType<Kn5RenderableList>()) {
                var node = list.OriginalNode;
                node.Transform = list.LocalMatrix.ToArray();
                node.Active = list.IsEnabled;
            }
        }
    }
}