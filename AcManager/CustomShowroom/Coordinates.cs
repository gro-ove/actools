using FirstFloor.ModernUI.Presentation;
using SlimDX;

namespace AcManager.CustomShowroom {
    public class Coordinates : NotifyPropertyChanged {
        private double _x;

        public double X {
            get => _x;
            set => Apply(value, ref _x);
        }

        private double _y;

        public double Y {
            get => _y;
            set => Apply(value, ref _y);
        }

        private double _z;

        public double Z {
            get => _z;
            set => Apply(value, ref _z);
        }

        public void Set(double x, double y, double z) {
            X = x;
            Y = y;
            Z = z;
        }

        public void Set(double[] v) {
            X = v[0];
            Y = v[1];
            Z = v[2];
        }

        public void Set(Vector3 v) {
            X = v.X;
            Y = v.Y;
            Z = v.Z;
        }

        public static Coordinates Create(Vector3 v) {
            var c = new Coordinates();
            c.Set(v);
            return c;
        }

        public Vector3 ToVector() {
            return new Vector3((float)X, (float)Y, (float)Z);
        }

        public double[] ToArray() {
            return new double[] { (float)X, (float)Y, (float)Z };
        }
    }
}