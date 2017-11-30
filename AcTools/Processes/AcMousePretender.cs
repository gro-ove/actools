using System;
using System.Drawing;
using System.Globalization;
using System.Windows.Forms;
using AcTools.Utils;
using AcTools.Windows.Input;

namespace AcTools.Processes {
    public static class AcMousePretender {
        private static string ToSwitch(this Point p) {
            return p.X.ToString(CultureInfo.InvariantCulture) + ":" + p.Y.ToString(CultureInfo.InvariantCulture);
        }

        private const string Screen1920X1080 = "1920:1080";
        private const string Screen1280X720 = "1280:720";

        private static void Click(Func<Point, Point> coordinatesProvider) {
            var originalPosition = Cursor.Position;
            var screen = Screen.FromPoint(originalPosition);

            var screenWidth = screen.Bounds.Width;
            var screenHeight = screen.Bounds.Height;
            var coordinates = coordinatesProvider(new Point(screenWidth, screenHeight));

            var inputSimulator = new InputSimulator();

            inputSimulator.Mouse.MoveMouseTo(65536d * coordinates.X / screenWidth, 65536d * coordinates.Y / screenHeight);
            inputSimulator.Mouse.LeftButtonClick();
            inputSimulator.Mouse.MoveMouseTo(65536d * originalPosition.X / screenWidth, 65536d * originalPosition.Y / screenHeight);
        }

        // Please, feel free to add coordinates for your own screen

        public static void ClickStartButton() {
            Click(screen => {
                switch (screen.ToSwitch()) {
                    case Screen1920X1080:
                        return new Point(50, 150);
                    case Screen1280X720:
                        return new Point(50, 170);
                    default:
                        return new Point(40, (150d / 1000d * screen.Y).RoundToInt());
                }
            });
        }

        public static void ClickContinueButton() {
            Click(screen => {
                switch (screen.ToSwitch()) {
                    case Screen1920X1080:
                        return new Point(960, 468);
                    case Screen1280X720:
                        return new Point(640, 290);
                    default:
                        return new Point(screen.X / 2, screen.Y / 2 - 72);
                }
            });
        }
    }
}