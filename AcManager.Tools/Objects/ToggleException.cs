using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AcManager.Tools.Objects {
    public class ToggleException : Exception {
        internal ToggleException(string message) : base(message) { }
    }
}
