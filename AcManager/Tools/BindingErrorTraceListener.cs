// If you have nothing else to do, or UI behaves weird, just turn on
// this thing. Literally hours of fun.
// #define TRACE_ERRORS

using System.Diagnostics;

namespace AcManager.Tools {
    public class BindingErrorTraceListener : TraceListener {
        public static SourceLevels GetSourceLevels() {
#if TRACE_ERRORS
            return SourceLevels.Warning;
#else
            return SourceLevels.Critical;
#endif
        }

        public override void Write(string message) {
            FirstFloor.ModernUI.Helpers.Logging.Caution("[B] " + message);
        }

        public override void WriteLine(string message) {
            FirstFloor.ModernUI.Helpers.Logging.Caution("[B] " + message);
        }
    }
}