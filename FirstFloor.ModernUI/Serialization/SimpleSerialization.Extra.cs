using System;
using System.Windows;
using System.Windows.Media;

namespace FirstFloor.ModernUI.Serialization {
    public static partial class SimpleSerialization {
        private static void RegisterWpfTypes() {
            Register(Point.Parse, v => v.ToString(Cu));
            Register(s => {
                var bytes = BitConverter.GetBytes(s.As<int>());
                return Color.FromArgb(bytes[0], bytes[1], bytes[2], bytes[3]);
            }, v => BitConverter.ToInt32(new[] { v.A, v.R, v.G, v.B }, 0).As<string>());
        }

        private static void RegisterDrawingTypes() {
            Register(s => {
                var p = s.Split(';');
                return new System.Drawing.Point(p[0].As<int>(), p[1].As<int>());
            }, p => p.X.As<string>() + ';' + p.Y.As<string>());
        }
    }
}