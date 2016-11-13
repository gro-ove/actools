using System;
using System.Windows.Media;

namespace FirstFloor.ModernUI.Windows {
    public static class CompositionTargetEx {
        private static TimeSpan _last = TimeSpan.Zero;
        private static event EventHandler<RenderingEventArgs> FrameUpdating;

        public static event EventHandler<RenderingEventArgs> Rendering {
            add {
                if (FrameUpdating == null) {
                    CompositionTarget.Rendering += CompositionTarget_Rendering;
                }
                FrameUpdating += value;
            }
            remove {
                FrameUpdating -= value;
                if (FrameUpdating == null) {
                    CompositionTarget.Rendering -= CompositionTarget_Rendering;
                }
            }
        }

        private static void CompositionTarget_Rendering(object sender, EventArgs e) {
            var args = (RenderingEventArgs)e;
            if (args.RenderingTime == _last) return;
            _last = args.RenderingTime;
            FrameUpdating?.Invoke(sender, args);
        }
    }
}