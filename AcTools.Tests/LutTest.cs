using System.Linq;
using AcTools.Utils.Physics;
using NUnit.Framework;

namespace AcTools.Tests {
    [TestFixture]
    public class LutTest {
        [Test]
        public void LinearInterpolation() {
            var lut = new Lut {
                new LutPoint(1d, 1d),
                new LutPoint(2d, 1d),
                new LutPoint(4d, 5d),
                new LutPoint(5d, 3d),
            };

            // points
            Assert.AreEqual(1d, lut.InterpolateLinear(1d));
            Assert.AreEqual(1d, lut.InterpolateLinear(2d));
            Assert.AreEqual(3d, lut.InterpolateLinear(5d));

            // clamping
            Assert.AreEqual(1d, lut.InterpolateLinear(-1d));
            Assert.AreEqual(3d, lut.InterpolateLinear(5.7d));

            // interpolation itself
            Assert.AreEqual(1d, lut.InterpolateLinear(1.5d));
            Assert.AreEqual(2d, lut.InterpolateLinear(2.5d));
            Assert.AreEqual(3d, lut.InterpolateLinear(3d));
            Assert.AreEqual(4d, lut.InterpolateLinear(4.5d));
        }

        [Test]
        public void CubicInterpolation() {
            var lut = new Lut {
                new LutPoint(1d, 1d),
                new LutPoint(2d, 1d),
                new LutPoint(4d, 5d),
                new LutPoint(5d, 3d),
            };

            // points
            Assert.AreEqual(1d, lut.InterpolateCubic(1d));
            Assert.AreEqual(1d, lut.InterpolateCubic(2d));
            Assert.AreEqual(3d, lut.InterpolateCubic(5d));

            // clamping
            Assert.AreEqual(1d, lut.InterpolateCubic(-1d));
            Assert.AreEqual(3d, lut.InterpolateCubic(5.7d));

            // interpolation itself
            Assert.IsTrue(lut.InterpolateCubic(1.9d) < 1d);
        }

        [Test]
        public void LutValueParse() {
            var lut = Lut.FromValue("(|0=0.7|1000=0.8|4000=0.9|)");

            Assert.IsTrue(lut.SequenceEqual(Lut.FromValue("(0=0.7|1000=0.8|4000=0.9)")));
            Assert.AreEqual(3, lut.Count);
            Assert.AreEqual(2, Lut.FromValue("(0=0.7|=0.8|4000=0.9)").Count);

            Assert.AreEqual(0.75, lut.InterpolateLinear(500d), 0.00001);
            Assert.AreEqual(0.85, lut.InterpolateLinear(2500d), 0.00001);
            Assert.AreEqual(0.9, lut.InterpolateLinear(4500d), 0.00001);
        }

        [Test]
        public void ToStringTest() {
            var val = "(|0=0.7|1000=0.8|4000=0.9|)";
            var lut = Lut.FromValue(val);
            Assert.AreEqual(val, lut.ToString());
        }

        [Test]
        public void OptimizeTest() {
            var val = "(|0=0.7|2000=0.8|4000=0.9|5000=0.6|)";
            var lut = Lut.FromValue(val);
            Assert.AreEqual("(|0=0.7|4000=0.9|5000=0.6|)", lut.Optimize().ToString());
        }

        [Test]
        public void MinMax() {
            var lut = new Lut();
            lut.UpdateBoundingBox();
            Assert.AreEqual(double.NaN, lut.MinX);
            Assert.AreEqual(double.NaN, lut.MinY);
            Assert.AreEqual(double.NaN, lut.MaxX);
            Assert.AreEqual(double.NaN, lut.MaxY);

            lut.Add(new LutPoint(1d, 1d));
            lut.UpdateBoundingBox();
            Assert.AreEqual(1d, lut.MinX);
            Assert.AreEqual(1d, lut.MinY);
            Assert.AreEqual(1d, lut.MaxX);
            Assert.AreEqual(1d, lut.MaxY);

            lut.Add(new LutPoint(-1d, 2d));
            lut.UpdateBoundingBox();
            Assert.AreEqual(-1d, lut.MinX);
            Assert.AreEqual(1d, lut.MinY);
            Assert.AreEqual(1d, lut.MaxX);
            Assert.AreEqual(2d, lut.MaxY);
        }
    }
}