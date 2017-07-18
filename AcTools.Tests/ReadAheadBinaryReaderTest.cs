using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using NUnit.Framework;

namespace AcTools.Tests {
    [TestFixture]
    public class ReadAheadBinaryReaderTest {
        private static string GetTestDir([CallerFilePath] string callerFilePath = null) => Path.Combine(Path.GetDirectoryName(callerFilePath) ?? "", "test");
        private static string TestDir => GetTestDir();

        [Test]
        public async Task CopyToTest() {
            var f = @"D:\Games\Assetto Corsa\content\cars\ks_porsche_911_carrera_rsr\porsche_911_carrera_rsr.kn5";
            var target = File.ReadAllBytes(f);

            using (var r = new ReadAheadBinaryReader(f))
            using (var m = new MemoryStream()){
                Assert.AreEqual('s', r.ReadByte());
                m.WriteByte((byte)'s');
                r.CopyTo(m);

                /*File.WriteAllBytes($"{TestDir}/rabr_got.kn5", m.ToArray());
                File.WriteAllBytes($"{TestDir}/rabr_expected.kn5", File.ReadAllBytes(f));*/

                Assert.IsTrue(target.SequenceEqual(m.ToArray()));
            }

            using (var r = new ReadAheadBinaryReader(f))
            using (var m = new MemoryStream()){
                Assert.AreEqual('s', r.ReadByte());
                m.WriteByte((byte)'s');
                await r.CopyToAsync(m);
                Assert.IsTrue(target.SequenceEqual(m.ToArray()));
            }

            using (var r = new ReadAheadBinaryReader(f))
            using (var m = new MemoryStream()){
                Assert.AreEqual('s', r.ReadByte());
                m.WriteByte((byte)'s');
                r.CopyTo(m, 19270);
                Assert.IsTrue(target.Take(19271).SequenceEqual(m.ToArray()));
            }

            using (var r = new ReadAheadBinaryReader(f))
            using (var m = new MemoryStream()){
                Assert.AreEqual('s', r.ReadByte());
                m.WriteByte((byte)'s');
                await r.CopyToAsync(m, 19270);
                Assert.IsTrue(target.Take(19271).SequenceEqual(m.ToArray()));
            }
        }
    }
}