using System.Threading;
using AcManager.Tools.AcTelemetryListener;
using NUnit.Framework;

namespace AcManager.Tools.Tests {
    [TestFixture]
    public class TelemetryListenerTest {
        [Test]
        public void FirstTest() {
            using (var listener = new TelemetryListener(new TelemetryListenerSettings())) {
                Thread.Sleep(1000);
            }
        }
    }
}