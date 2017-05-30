using System.Linq;
using AcTools.DataFile;
using NUnit.Framework;

namespace AcTools.Tests {
    [TestFixture]
    public class IniParsingTest {
        private static string Data = @"[SECTION_0]
KEY0=VALUE0 ; COMMENT
A=BCD
VLEQ=some=thing
   KEY_WITH SPACES = something[TOTALLY MESSED UP SECTION]value= kkk
   ; comment

[SECTION_1]
KEY1=VALUE1 // another type of comment
VAL_WITH_EQ=VAL=UE1 // another type of comment
KEY_WITHOUT_VALUE= ; missing value
KEY_WITHOUT_VALUE= // missing value
KEY_WITHOUT_VALUE_AND_COMMENT_BUNCH_OF_SPACES=
IMMEDIATE_VALUE_AFTERWARDS_SHOULD_BE_IGNORED
KEY_WITHOUT_VALUE_AND_COMMENT=
=VALUE_WITHOUT_KEY
NORMAL  =VA;LUE

COMPLI  =
CATED

NO_EMPTY_LINE=AFTERWARDS";

        [Test]
        public void IniParsing() {
            var parsed = IniFile.Parse(Data);

            Assert.IsTrue(parsed.ContainsKey("TOTALLY MESSED UP SECTION"));
            Assert.IsTrue(parsed.ContainsKey("SECTION_1"));
            Assert.AreEqual(3, parsed.Count());

            Assert.AreEqual("VA", parsed["SECTION_1"].GetNonEmpty("NORMAL"));
            Assert.AreEqual("VAL=UE1", parsed["SECTION_1"].GetNonEmpty("VAL_WITH_EQ"));
            Assert.AreEqual("some=thing", parsed["SECTION_0"].GetNonEmpty("VLEQ"));
            Assert.AreEqual(null, parsed["SECTION_1"].GetNonEmpty("KEY_WITHOUT_VALUE_AND_COMMENT_BUNCH_OF_SPACES"));
            Assert.AreEqual("", parsed["SECTION_1"].GetPossiblyEmpty("KEY_WITHOUT_VALUE_AND_COMMENT_BUNCH_OF_SPACES"));
            Assert.AreEqual(null, parsed["SECTION_1"].GetNonEmpty("COMPLI"));
            Assert.AreEqual("", parsed["SECTION_1"].GetPossiblyEmpty("COMPLI"));
            Assert.AreEqual("AFTERWARDS", parsed["SECTION_1"].GetPossiblyEmpty("NO_EMPTY_LINE"));
            Assert.AreEqual("something", parsed["SECTION_0"].GetPossiblyEmpty("KEY_WITH SPACES"));
            Assert.IsFalse(parsed["SECTION_1"].ContainsKey("CATED"));

            Assert.AreEqual("kkk", parsed["TOTALLY MESSED UP SECTION"].GetNonEmpty("value"));
        }

        [Test]
        public void IniSemicolonsParsing() {
            var parsed = IniFile.Parse(Data, IniFileMode.ValuesWithSemicolons);

            Assert.IsFalse(parsed.ContainsKey("TOTALLY MESSED UP SECTION"));
            Assert.IsTrue(parsed.ContainsKey("SECTION_1"));
            Assert.AreEqual(2, parsed.Count());

            Assert.AreEqual("something[TOTALLY MESSED UP SECTION]value= kkk", parsed["SECTION_0"].GetNonEmpty("KEY_WITH SPACES"));
            Assert.AreEqual("VA;LUE", parsed["SECTION_1"].GetNonEmpty("NORMAL"));
            Assert.AreEqual("VAL=UE1", parsed["SECTION_1"].GetNonEmpty("VAL_WITH_EQ"));
            Assert.AreEqual("some=thing", parsed["SECTION_0"].GetNonEmpty("VLEQ"));
            Assert.AreEqual(null, parsed["SECTION_1"].GetNonEmpty("KEY_WITHOUT_VALUE_AND_COMMENT_BUNCH_OF_SPACES"));
            Assert.AreEqual("", parsed["SECTION_1"].GetPossiblyEmpty("KEY_WITHOUT_VALUE_AND_COMMENT_BUNCH_OF_SPACES"));
            Assert.AreEqual(null, parsed["SECTION_1"].GetNonEmpty("COMPLI"));
            Assert.AreEqual("", parsed["SECTION_1"].GetPossiblyEmpty("COMPLI"));
            Assert.AreEqual("AFTERWARDS", parsed["SECTION_1"].GetPossiblyEmpty("NO_EMPTY_LINE"));
            Assert.IsFalse(parsed["SECTION_1"].ContainsKey("CATED"));
        }
    }
}