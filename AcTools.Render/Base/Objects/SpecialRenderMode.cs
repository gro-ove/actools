using System;

namespace AcTools.Render.Base.Objects {
    [Flags]
    public enum SpecialRenderMode {
        Simple,
        SimpleTransparent,

        Outline,

        Deferred,
        DeferredTransparentMask,    // draw depth of transparent surfaces for filtering stuff under
        DeferredTransparentDepth,   // ?
        DeferredTransparentForw,    // for transparent surfaces behind something transparent
        DeferredTransparentDef,     // for most top transparent surfaces, maximum of niceness

        Reflection,
        Shadow
    }
}