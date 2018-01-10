using System;
using System.Threading;

namespace AcTools.Windows.Input {
    public class MouseSimulator {
        private const int MouseWheelClickSize = 120;

        private void SendSimulatedInput(User32.Input[] inputList) {
            InputBuilder.DispatchInput(inputList);
        }

        public MouseSimulator MoveMouseBy(int pixelDeltaX, int pixelDeltaY) {
            var inputList = new InputBuilder().AddRelativeMouseMovement(pixelDeltaX, pixelDeltaY).ToArray();
            SendSimulatedInput(inputList);
            return this;
        }

        public MouseSimulator MoveMouseTo(double absoluteX, double absoluteY) {
            var inputList = new InputBuilder().AddAbsoluteMouseMovement((int)Math.Truncate(absoluteX), (int)Math.Truncate(absoluteY)).ToArray();
            SendSimulatedInput(inputList);
            return this;
        }

        public MouseSimulator MoveMouseToPositionOnVirtualDesktop(double absoluteX, double absoluteY) {
            var inputList = new InputBuilder().AddAbsoluteMouseMovementOnVirtualDesktop((int)Math.Truncate(absoluteX), (int)Math.Truncate(absoluteY)).ToArray();
            SendSimulatedInput(inputList);
            return this;
        }

        public MouseSimulator LeftButtonDown() {
            var inputList = new InputBuilder().AddMouseButtonDown(MouseButton.LeftButton).ToArray();
            SendSimulatedInput(inputList);
            return this;
        }

        public MouseSimulator LeftButtonUp() {
            var inputList = new InputBuilder().AddMouseButtonUp(MouseButton.LeftButton).ToArray();
            SendSimulatedInput(inputList);
            return this;
        }

        public MouseSimulator LeftButtonClick() {
            var inputList = new InputBuilder().AddMouseButtonClick(MouseButton.LeftButton).ToArray();
            SendSimulatedInput(inputList);
            return this;
        }

        public MouseSimulator LeftButtonDoubleClick() {
            var inputList = new InputBuilder().AddMouseButtonDoubleClick(MouseButton.LeftButton).ToArray();
            SendSimulatedInput(inputList);
            return this;
        }

        public MouseSimulator RightButtonDown() {
            var inputList = new InputBuilder().AddMouseButtonDown(MouseButton.RightButton).ToArray();
            SendSimulatedInput(inputList);
            return this;
        }

        public MouseSimulator RightButtonUp() {
            var inputList = new InputBuilder().AddMouseButtonUp(MouseButton.RightButton).ToArray();
            SendSimulatedInput(inputList);
            return this;
        }

        public MouseSimulator RightButtonClick() {
            var inputList = new InputBuilder().AddMouseButtonClick(MouseButton.RightButton).ToArray();
            SendSimulatedInput(inputList);
            return this;
        }

        public MouseSimulator RightButtonDoubleClick() {
            var inputList = new InputBuilder().AddMouseButtonDoubleClick(MouseButton.RightButton).ToArray();
            SendSimulatedInput(inputList);
            return this;
        }

        public MouseSimulator XButtonDown(XButton buttonId) {
            var inputList = new InputBuilder().AddMouseXButtonDown(buttonId).ToArray();
            SendSimulatedInput(inputList);
            return this;
        }

        public MouseSimulator XButtonUp(XButton buttonId) {
            var inputList = new InputBuilder().AddMouseXButtonUp(buttonId).ToArray();
            SendSimulatedInput(inputList);
            return this;
        }

        public MouseSimulator XButtonClick(XButton buttonId) {
            var inputList = new InputBuilder().AddMouseXButtonClick(buttonId).ToArray();
            SendSimulatedInput(inputList);
            return this;
        }

        public MouseSimulator XButtonDoubleClick(XButton buttonId) {
            var inputList = new InputBuilder().AddMouseXButtonDoubleClick(buttonId).ToArray();
            SendSimulatedInput(inputList);
            return this;
        }

        public MouseSimulator VerticalScroll(int scrollAmountInClicks) {
            var inputList = new InputBuilder().AddMouseVerticalWheelScroll(scrollAmountInClicks * MouseWheelClickSize).ToArray();
            SendSimulatedInput(inputList);
            return this;
        }

        public MouseSimulator HorizontalScroll(int scrollAmountInClicks) {
            var inputList = new InputBuilder().AddMouseHorizontalWheelScroll(scrollAmountInClicks * MouseWheelClickSize).ToArray();
            SendSimulatedInput(inputList);
            return this;
        }

        public MouseSimulator Sleep(int millsecondsTimeout) {
            Thread.Sleep(millsecondsTimeout);
            return this;
        }

        public MouseSimulator Sleep(TimeSpan timeout) {
            Thread.Sleep(timeout);
            return this;
        }
    }
}