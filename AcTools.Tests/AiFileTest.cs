using System.IO;
using AcTools.AiFile;
using AcTools.Utils;
using NUnit.Framework;

namespace AcTools.Tests {
    [TestFixture]
    public class AiFileTest {
        private static readonly string AcRoot = AcRootFinder.TryToFind();

        // default error is 8‰
        private void TestTrack(string path, float length, float minWidth, float maxWidth, float lengthError = 0.008f, float widthError = 0.1f) {
            var f = Path.Combine(AcRoot, path);

            var ai = AiLane.FromFile(f);
            Assert.AreEqual(length, ai.CalculateLength(), length * lengthError, "Length is wrong");

            var w = ai.CalculateWidth();
            if (w.Item1 == 0f) {
                // obsolete format?
                return;
            }

            Assert.AreEqual(minWidth, w.Item1, minWidth * widthError, "Min width is wrong");
            Assert.AreEqual(maxWidth, w.Item2, maxWidth * widthError, "Max width is wrong");
        }

        [Test]
        public void Main() {
            TestTrack(@"content\tracks\newbury_2006\ai\fast_lane.ai", 2213, 13, 16); // actual max width is 14, but track is messed up a bit
            TestTrack(@"content\tracks\hillclimb_moya_v2\ai\fast_lane.ai", 6475, 9.4f, 10f);
            TestTrack(@"content\tracks\redbullring\ai\fast_lane.ai", 4318, 12f, 13f);
        }
    }
}