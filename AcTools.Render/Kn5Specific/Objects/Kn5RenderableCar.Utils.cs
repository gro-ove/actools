// #define BB_PERF_PROFILE

using System;
using System.Collections.Generic;
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
                bool clampEnabled = true) {
            return CreateAnimator(Path.Combine(rootDirectory, "animations", animName), duration, clampEnabled);
        }

        [CanBeNull]
        public static KsAnimAnimator CreateAnimator([NotNull] string rootDirectory, CarData.AnimationBase animation, bool clampEnabled = true) {
            return CreateAnimator(Path.Combine(rootDirectory, "animations", animation.KsAnimName), animation.Duration, clampEnabled);
        }

        [CanBeNull]
        public static KsAnimAnimator CreateAnimator([NotNull] string filename, float duration = 1f, bool clampEnabled = true) {
            if (!File.Exists(filename)) return null;

            try {
                return new KsAnimAnimator(filename, duration) {
                    ClampPosition = clampEnabled
                };
            } catch (Exception e) {
                AcToolsLogging.Write(e);
                return null;
            }
        }

        public static void SetCockpitLrActive([NotNull] RenderableList parent, bool value) {
            foreach (var child in parent.GetAllChildren().OfType<Kn5RenderableList>()) {
                switch (child.OriginalNode.Name) {
                    case "COCKPIT_LR":
                    case "STEER_LR":
                    case "SHIFT_LD":
                        child.IsEnabled = value;
                        break;
                    case "COCKPIT_HR":
                    case "STEER_HR":
                    case "SHIFT_HD":
                        child.IsEnabled = !value;
                        break;
                }
            }
        }

        public static void SetSeatbeltActive([NotNull] RenderableList parent, bool value) {
            var onNode = parent.GetDummyByName("CINTURE_ON");
            if (onNode != null) {
                onNode.IsEnabled = value;
            }

            var offNode = parent.GetDummyByName("CINTURE_OFF");
            if (offNode != null) {
                offNode.IsEnabled = !value;
            }
        }

        public static void SetBlurredObjects([NotNull] RenderableList parent, IEnumerable<CarData.BlurredObject> blurredObjects, bool value) {
            foreach (var blurredObject in blurredObjects) {
                var staticNode = parent.GetDummyByName(blurredObject.StaticName);
                if (staticNode != null) {
                    staticNode.IsEnabled = !value;
                }

                var blurredNode = parent.GetDummyByName(blurredObject.BlurredName);
                if (blurredNode != null) {
                    blurredNode.IsEnabled = value;
                }
            }
        }
    }
}