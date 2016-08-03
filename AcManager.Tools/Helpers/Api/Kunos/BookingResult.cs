using System;

namespace AcManager.Tools.Helpers.Api.Kunos {
    public class BookingResult {
        public BookingResult(TimeSpan left) {
            Left = left;
        }

        public BookingResult(string errorMessage) {
            ErrorMessage = errorMessage;
        }

        public TimeSpan Left { get; }

        public string ErrorMessage { get; }

        public bool IsSuccessful => ErrorMessage == null;
    }
}