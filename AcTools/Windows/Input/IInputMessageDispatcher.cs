using System;
using AcTools.Windows.Input.Native;

namespace AcTools.Windows.Input {
    /// <summary>
    /// The contract for a service that dispatches <see cref="InputEntry"/> messages to the appropriate destination.
    /// </summary>
    internal interface IInputMessageDispatcher {
        /// <summary>
        /// Dispatches the specified list of <see cref="InputEntry"/> messages in their specified order.
        /// </summary>
        /// <param name="inputs">The list of <see cref="InputEntry"/> messages to be dispatched.</param>
        /// <exception cref="ArgumentException">If the <paramref name="inputs"/> array is empty.</exception>
        /// <exception cref="ArgumentNullException">If the <paramref name="inputs"/> array is null.</exception>
        /// <exception cref="Exception">If the any of the commands in the <paramref name="inputs"/> array could not be sent successfully.</exception>
        void DispatchInput(InputEntry[] inputs);
    }
}
