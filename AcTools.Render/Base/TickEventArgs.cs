using System;

namespace AcTools.Render.Base {
    public class TickEventArgs : EventArgs {
        public TickEventArgs(float deltaTime) {
            DeltaTime = deltaTime;
        }

        public float DeltaTime { get; }
    }
}