using System.Windows.Forms;
using System.Windows.Input;

namespace AcManager.Tools.Miscellaneous {
    public interface ILocalKeyBindingInput {
        bool IsWaiting { get; set; }
        bool IsPressed { set; }
        Keys Value { get; set; }
        ICommand ClearCommand { get; }
    }
}