using System;

namespace FirstFloor.ModernUI.Presentation {
    public class LinkCloseEventArgs : EventArgs {
        public LinkCloseEventArgs(LinkCloseMode mode) {
            Mode = mode;
        }
        
        public LinkCloseMode Mode { get; }
    }
}