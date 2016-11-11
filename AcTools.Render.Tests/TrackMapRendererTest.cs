using System.Diagnostics;
using System.IO;
using AcTools.Render.Kn5SpecificSpecial;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AcTools.Render.Tests {
    [TestClass]
    public class TrackMapRendererTest {
        [TestMethod]
        public void HitabashiPark() {
            var path = @"D:\Games\Assetto Corsa\content\tracks\hitabashipark\hitabashipark.kn5";
            var output = Path.Combine(Path.GetDirectoryName(path) ?? "", "map_test.png");
            if (!File.Exists(path)) {
                Debug.WriteLine("REQUIRED ASSET IS MISSING, TEST CANNOT BE DONE");
                return;
            }

            using (var renderer = new TrackMapRenderer(path)) {
                renderer.Shot(output);
            }

            Process.Start(output);
        }
    }
}
