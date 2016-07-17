using System;

namespace AcManager.Tools.Helpers.AdditionalContentInstallation {
    public class PasswordException : Exception {
        public PasswordException(string message) : base(message) {}
    }
}
