using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AcManager.Controls.Helpers {
    public interface IFinishableControl {
        void Finish(bool result);
    }
}
