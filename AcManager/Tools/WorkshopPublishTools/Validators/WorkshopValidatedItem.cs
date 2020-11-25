using System;
using FirstFloor.ModernUI.Helpers;
using JetBrains.Annotations;

namespace AcManager.Tools.WorkshopPublishTools.Validators {
    public class WorkshopValidatedItem {
        public WorkshopValidatedItem(string message, WorkshopValidatedState state = WorkshopValidatedState.Passed) {
            Message = message;
            DisplayMessage = message.ToSentence();
            State = state;
            FixCallback = null;
        }

        public WorkshopValidatedItem(string message, Action fixCallback, Action rollbackCallback) {
            Message = message;
            DisplayMessage = message.ToSentence();
            State = WorkshopValidatedState.Fixable;
            FixCallback = fixCallback;
            RollbackCallback = rollbackCallback;
        }

        public string Message { get; }

        public string DisplayMessage { get; }

        public WorkshopValidatedState State { get; }

        [CanBeNull]
        public Action FixCallback { get; }

        [CanBeNull]
        public Action RollbackCallback { get; }
    }
}