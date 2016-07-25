using System;

namespace AcManager.Tools.ContentInstallation {
    public class PasswordException : Exception {
        public PasswordException(string message) : base(message) {}
    }
}
