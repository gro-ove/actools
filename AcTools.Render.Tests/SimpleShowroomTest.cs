using System.Diagnostics;
using System.IO;
using AcTools.Render.Kn5Specific.Objects;
using AcTools.Render.Kn5SpecificForward;
using AcTools.Render.Wrapper;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AcTools.Render.Tests {
    [TestClass]
    public class SimpleShowroomTest {
        [TestMethod]
        public void BmwE92() {
            var path = Path.Combine(AcRootFinder.Find(), @"content\cars\bmw_m3_e92\bmw_m3_e92.kn5");
            if (!File.Exists(path)) {
                Debug.WriteLine("REQUIRED ASSET IS MISSING, TEST CANNOT BE DONE");
                return;
            }

            using (var renderer = new ForwardKn5ObjectRenderer(new CarDescription(path))) {
                renderer.UseMsaa = false;
                renderer.UseFxaa = true;
                new LiteShowroomFormWrapper(renderer).Run();
            }
        }
    }
}