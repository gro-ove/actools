using System;
using System.Threading;
using System.Threading.Tasks;
using FirstFloor.ModernUI.Helpers;

namespace AcManager.Tools.Helpers.AcLog {
    public class WhatsGoingOn {
        public WhatsGoingOnType Type { get; }

        public object[] Arguments { get; }

        /// <summary>
        /// Throws an exception if fixing failed.
        /// </summary>
        public Func<CancellationToken, Task> Fix { get; set; }

        public WhatsGoingOn(WhatsGoingOnType type, params object[] arguments) {
            Type = type;
            Arguments = arguments;
        }

        public string GetDescription() {
            return string.Format(Type.GetDescription() ?? Type.ToString(), Arguments);
        }

        private NonfatalErrorSolution _solution;

        public NonfatalErrorSolution Solution => _solution ?? (Fix == null ? null :
                _solution = new NonfatalErrorSolution(null, null, Fix, () => Fix != null));
    }
}