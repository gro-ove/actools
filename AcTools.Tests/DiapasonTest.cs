using System.Linq;
using AcTools.Utils.Helpers;
using NUnit.Framework;

namespace AcTools.Tests {
    internal static class Helper {
        public static Diapason<int> ToDiapason(this string s, int from, int to) {
            return Diapason.CreateInt32(s).SetLimits(from, to);
        }

        public static bool DiapasonContains(this string s, double value, bool roundSingle = true) {
            return Diapason.CreateDouble(s, roundSingle).Contains(value);
        }

        public static bool TimeDiapasonContains(this string s, int value, bool roundSingle = true) {
            return Diapason.CreateTime(s, roundSingle).Contains(value);
        }
    }

    public class DiapasonTest {
        [Test]
        public void Diapason() {
            Assert.AreEqual(101, "0-100".ToDiapason(0, 1000).Count());
            Assert.AreEqual(5050, "0-100".ToDiapason(0, 1000).Sum());

            Assert.AreEqual(202, "0-,-100".ToDiapason(0, 100).Count());
            Assert.AreEqual(10100, "0- ; -100".ToDiapason(0, 100).Sum());

            Assert.AreEqual(101, "-".ToDiapason(0, 100).Count());
            Assert.AreEqual(5050, "-".ToDiapason(0, 100).Sum());

            Assert.AreEqual(39, "-10,18,23-28,980-".ToDiapason(0, 1000).Count());
            Assert.AreEqual(21016, "-10, 18  ,  23 -  28  ;980-".ToDiapason(0, 1000).Sum());

            Assert.IsTrue("-10,18.3,23-28,980-".DiapasonContains(24));
            Assert.IsTrue("-10,18.3,23-28,980-".DiapasonContains(28));
            Assert.IsTrue("-10,18.3,23-28,980-".DiapasonContains(1024));
            Assert.IsTrue("-10,18.3,23-28,980-".DiapasonContains(18.35));
            Assert.IsTrue("-10,18,23-28,980-".DiapasonContains(18.35));

            Assert.IsFalse("-10,18.3,23-28,980-".DiapasonContains(15));
            Assert.IsFalse("-10,18.3,23-28,980-".DiapasonContains(29));
            Assert.IsFalse("-10,18.3,23-28,980-".DiapasonContains(18));
            Assert.IsFalse("-10,18.3,23-28,980-".DiapasonContains(979));
            Assert.IsFalse("-10,18.3,23-28,980-".DiapasonContains(18.35, false));
        }

        [Test]
        public void NegativeDiapason() {
            Assert.AreEqual(201, "-100-100".ToDiapason(-1000, 1000).Count());
            Assert.AreEqual(0, "-100-100".ToDiapason(-1000, 1000).Sum());
            Assert.AreEqual(1, "-100 - -100".ToDiapason(-1000, 1000).Count());
            Assert.AreEqual(1, "0 --0".ToDiapason(-1000, 1000).Count());
            Assert.AreEqual(11, "--10".ToDiapason(-20, 1000).Count());
            Assert.AreEqual(11, "-10-".ToDiapason(-20, 0).Count());

            Assert.IsTrue("-10--5".DiapasonContains(-7));
        }

        [Test]
        public void TimeDiapason() {
            Assert.IsTrue("10:30-11:48,12,13:00:16,18:47-".TimeDiapasonContains(FlexibleParser.ParseTime("11:43")));
            Assert.IsTrue("10:30-11:48,12,13:00:16,18:47-".TimeDiapasonContains(FlexibleParser.ParseTime("11:48")));
            Assert.IsFalse("10:30-11:48,12,13:00:16,18:47-".TimeDiapasonContains(FlexibleParser.ParseTime("11:49")));
            Assert.IsFalse("10:30-11:48,12,13:00:16,18:47-".TimeDiapasonContains(FlexibleParser.ParseTime("9:43")));
            Assert.IsTrue("10:30-11:48,12,13:00:16,18:47-".TimeDiapasonContains(FlexibleParser.ParseTime("12:43")));
            Assert.IsFalse("10:30-11:48,12,13:00:16,18:47-".TimeDiapasonContains(FlexibleParser.ParseTime("12:43"), false));
            Assert.IsFalse("10:30-11:48,12,13:00:16,18:47-".TimeDiapasonContains(FlexibleParser.ParseTime("13:00")));
            Assert.IsTrue("10:30-11:48,12,13:00:16,18:47-".TimeDiapasonContains(FlexibleParser.ParseTime("13:00:16")));
            Assert.IsTrue("10:30-11:48,12,13:00:16,18:47-".TimeDiapasonContains(FlexibleParser.ParseTime("20:24")));
        }
    }
}