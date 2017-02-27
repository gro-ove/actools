using System;

namespace AcTools.Render.Base.Objects {
    [Flags]
    public enum SpecialRenderMode : ulong {
        InitializeOnly = 1 << 0,

        Simple = 1 << 1,
        SimpleTransparent = 1 << 2,

        Outline = 1 << 3,

        Deferred = 1 << 4,
        DeferredTransparentMask = 1 << 5,    // draw depth of transparent surfaces for filtering stuff under
        DeferredTransparentDepth = 1 << 6,   // ?
        DeferredTransparentForw = 1 << 7,    // for transparent surfaces behind something transparent
        DeferredTransparentDef = 1 << 8,     // for most top transparent surfaces, maximum of niceness
        GBuffer = 1 << 9,
        
        Reflection = 1 << 10,
        Shadow = 1 << 11
    }
}