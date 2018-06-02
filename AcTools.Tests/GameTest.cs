using AcTools.Processes;
using NUnit.Framework;

namespace AcTools.Tests {
    [TestFixture]
    public class GameTest {
        [Test]
        public void RoadTemperatureTest() {
            Assert.AreEqual(35.27, Game.ConditionProperties.GetRoadTemperature(12 * 60 * 60, 25, 1), 0.01);
            Assert.AreEqual(30.13, Game.ConditionProperties.GetRoadTemperature(12 * 60 * 60, 25, 0.5), 0.01);
            Assert.AreEqual(25.51, Game.ConditionProperties.GetRoadTemperature(12 * 60 * 60, 25, 0.05), 0.01);
            Assert.AreEqual(35.27, Game.ConditionProperties.GetRoadTemperature(12 * 60 * 60, 25, 0), 0.01);

            Assert.AreEqual(27.51, Game.ConditionProperties.GetRoadTemperature(1 * 60 * 60, 25, 0.5), 0.01);
            Assert.AreEqual(26.89, Game.ConditionProperties.GetRoadTemperature(4 * 60 * 60, 25, 0.5), 0.01);
            Assert.AreEqual(26.06, Game.ConditionProperties.GetRoadTemperature(8 * 60 * 60, 25, 0.5), 0.01);
            Assert.AreEqual(28.97, Game.ConditionProperties.GetRoadTemperature(18 * 60 * 60, 25, 0.5), 0.01);
            Assert.AreEqual(28.55, Game.ConditionProperties.GetRoadTemperature(20 * 60 * 60, 25, 0.5), 0.01);
            Assert.AreEqual(28.14, Game.ConditionProperties.GetRoadTemperature(22 * 60 * 60, 25, 0.5), 0.01);
        }
    }
}