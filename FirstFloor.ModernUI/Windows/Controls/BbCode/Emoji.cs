using System;
using System.Collections.Generic;

/*

Generation script for Node.JS, from a directory with Twemoji or pack with similar names:

const THRESHOLD = 0x203c;
const ZERO_WIDTH_SPACE = 0x200d;
const SEQUENCE_END = 0xfe0f;

var fs = require('fs');
var dir = fs.readdirSync(process.argv[2] || fs.readdirSync('.').filter(x => fs.lstatSync(x).isDirectory() && x[0] != '.')[0] || '.');
var numbers = dir.filter(x => !x.startsWith('00')).map(x => x.replace('.png', '').split('-').map(x => +('0x' + x)))
  .filter(x => x[0] >= THRESHOLD)
  .sort((a, b) => {
    for (var i = 0; i < a.length; i++){
      if (a[i] != b[i]) return a[i] - b[i];
    }
    return 0;
  });

function toRange(g){
  var a = g.reduce((a, b) => { if (a.indexOf(+b) == -1) a.push(+b); return a; }, []).sort((a, b) => a - b);
  var s = [], p = -1, r = 0;
  for (var i = 0; i <= a.length; i++){
    if (a[i] == p + 1){
      if (!r){ r = p; s.pop(); }
    } else {
      if (r){ s.push(p == r + 1 ? `c == 0x${r.toString(16)} || c == 0x${p.toString(16)}` : `c >= 0x${r.toString(16)} && c <= 0x${p.toString(16)}`); r = 0 }
      if (a[i]) s.push(`c == 0x${a[i].toString(16)}`);
    }
    p = a[i];
  }
  return s.join(' || ');
}

var range = (from, to) => new Array(to - from + 1).fill().map((d, i) => i + from);

// Various ranges:
var skinTones = range(0x1f3fb, 0x1f3ff);
var regionalSymbols = range(0x1f1e6, 0x1f1ff);
var genderFlags = [ 0x2640, 0x2642 ];
var genderPersons = [ 0x1f468, 0x1f469 ];

function isSkinTone(c){ return skinTones.indexOf(c) !== -1 || c == SEQUENCE_END; }
function isRegionalSymbol(c){ return regionalSymbols.indexOf(c) !== -1; }
function isGenderFlag(c){ return genderFlags.indexOf(c) !== -1; }
function isGenderPerson(c){ return genderPersons.indexOf(c) !== -1; }

// Utils:
function take(len, cb){
  var l = len == null ? complex : complex.filter(x => x.length == len);
  return l.filter(x => !x.taken && cb(x, l) && (x.taken = true));
}

// Base:
var complex = numbers.filter(x => x.length > 1);
var twoItemsGroups = complex.filter(x => x.length == 2);
var threeItemsGroups = complex.filter(x => x.length == 3);

// Colored skin:
var coloredSkinGroups = take(2,
  (x, a) => skinTones.indexOf(x[1]) !== -1
  && skinTones.every(y => a.some(z => z[0] == x[0] && z[1] == y)));

// Colored skin & gender
// <CH.>[SKIN]+[GENDER]0xFE0F
var coloredSkinGenderGroups = take(5,
  (x, a) => isSkinTone(x[1]) && x[2] == ZERO_WIDTH_SPACE && isGenderFlag(x[3]) && x[4] == SEQUENCE_END
  && skinTones.every(y => a.some(z => z[0] == x[0] && z[1] == y))
  && genderFlags.every(y => a.some(z => z[0] == x[0] && z[3] == y)));

// <CH.>[SKIN]
take(2, (x, a) => isSkinTone(x[1]) && coloredSkinGenderGroups.some(y => y[0] == x[0]));

// <CH.>+[GENDER]0xFE0F
take(4, (x, a) => x[1] == ZERO_WIDTH_SPACE && isGenderFlag(x[2]) && x[3] == SEQUENCE_END
  && coloredSkinGenderGroups.some(y => y[0] == x[0]));

// Non-colored, but with gender
// <CH.>+[GENDER]0xFE0F
var genderGroups = take(4,
  (x, a) => x[1] == ZERO_WIDTH_SPACE && isGenderFlag(x[2]) && x[3] == SEQUENCE_END
  && genderFlags.every(y => a.some(z => z[0] == x[0] && z[2] == y)));

// Professions:
// <PERSON>[SKIN]+<CH.>
var coloredSkinGenderProfessionItemGroups = take(4,
  (x, a) => isGenderPerson(x[0]) && isSkinTone(x[1]) && x[2] == ZERO_WIDTH_SPACE
  && skinTones.every(y => a.some(z => z[3] == x[3] && z[1] == y))
  && genderPersons.every(y => a.some(z => z[3] == x[3] && z[0] == y)));

// <PERSON>+<CH.>
take(3,
  (x, a) => isGenderPerson(x[0]) && x[1] == ZERO_WIDTH_SPACE
  && coloredSkinGenderProfessionItemGroups.some(y => y[3] == x[2]));

// Professions with SEQUENCE_END:
// <PERSON>[SKIN]+<CH.>0xFE0F
var coloredSkinGenderProfessionItemSequenceEndGroups = take(5,
  (x, a) => isGenderPerson(x[0]) && isSkinTone(x[1]) && x[2] == ZERO_WIDTH_SPACE && x[4] == SEQUENCE_END
  && skinTones.every(y => a.some(z => z[3] == x[3] && z[1] == y))
  && genderPersons.every(y => a.some(z => z[3] == x[3] && z[0] == y)));

// <PERSON>+<CH.>0xFE0F
take(4,
  (x, a) => isGenderPerson(x[0]) && x[1] == ZERO_WIDTH_SPACE && x[3] == SEQUENCE_END
  && coloredSkinGenderProfessionItemSequenceEndGroups.some(y => y[3] == x[2]));

// Flags:
var extraFlagsGroups = take(null, x => x[0] == 0x1f3f3 || x[0] == 0x1f3f4);
var flagsGroups = take(null, x => x.length == 2 && x.every(isRegionalSymbol));

// Companies:
var companies = take(null, x => {
  if (x.length < 3 || x.length % 2 == 0) return false;
  for (var i = 1; i < x.length - 1; i += 2){
    if (x[i] != ZERO_WIDTH_SPACE) return false;
  }
  return true;
});

// Left:
var leftOut = take(null, () => true);

// Debug:
function indexOf(array, pieceToFind){
  main: for (var i = 0; i < array.length; i++){
    var piece = array[i];
    for (var j = 0; j < piece.length; j++){ if (piece[j] != pieceToFind[j]) continue main; }
    return i;
  }
  return -1;
}

var missing = [ 0x1f482, 0x1f3fb, 0x2640 ];
console.log('Debug index: ' + indexOf(coloredSkinGenderGroups, missing) + '\n');

// Output:
function out(name, data){
  console.log(name + ':\n\t' + data.replace(/\n/g, '\n\t') + '\n\n');
}

out('IsEmoji()', toRange(numbers.map(x => x[0])));
out('IsSkinGroup(); <CH.>[SKIN]', toRange(coloredSkinGroups.map(x => x[0])));
out('IsGenderGroup(); <CH.>, <CH.>+[GENDER]0xFE0F',
  toRange(genderGroups.map(x => x[0])));
out('IsSkinGenderGroup(); <CH.>, <CH.>[SKIN], <CH.>+[GENDER]0xFE0F, <CH.>[SKIN]+[GENDER]0xFE0F',
  toRange(coloredSkinGenderGroups.map(x => x[0])));
out('IsSkinGenderProfessionGroup(); <PERSON>+<CH.>, <PERSON>[SKIN]+<CH.>', toRange(coloredSkinGenderProfessionItemGroups.map(x => x[3])));
out('IsSkinGenderProfessionSeGroup(); <PERSON>+<CH.>, <PERSON>[SKIN]+<CH.>0xFE0F', toRange(coloredSkinGenderProfessionItemSequenceEndGroups.map(x => x[3])));

out('IsFlagPrefix()', toRange(flagsGroups.map(x => x[0])));
console.log('GetFlagGroup() (without joiner):');
for (var i = flagsGroups[0][0], c = flagsGroups[flagsGroups.length - 1][0]; i <= c; i++){
  var a = flagsGroups.filter(x => x[0] == i).map(x => x[1]);
  if (a[0]) console.log(`\tcase 0x${i.toString(16)}:\n\t\treturn ${toRange(a)} ? result : 0;`);
}
console.log('\n\n');

out('IsExtraFlagPrefix()', toRange(extraFlagsGroups.map(x => x[0])));
out('ExtraFlags[][] (without joiner)', extraFlagsGroups.map(x => `new[] { ${x.map(y => '0x' + y.toString(16)).join(', ')} }`).join(',\n'));

out('IsCompanyPrefix()', toRange(companies.map(x => x[0])));
out('Company[][] (with joiner)', companies.map(x => `new[] { ${x.filter(x => x != ZERO_WIDTH_SPACE).map(y => '0x' + y.toString(16)).join(', ')} }`).join(',\n'));

out('IsMiscPrefix()', toRange(leftOut.map(x => x[0])));
out('Misc[][] (without joiner)', leftOut.map(x => `new[] { ${x.map(y => '0x' + y.toString(16)).join(', ')} }`).join(',\n'));

*/

namespace FirstFloor.ModernUI.Windows.Controls.BbCode {
    internal static class Emoji {
        private const int Threshold = 0x203c;
        private const int ZeroWidthSpace = 0x200d;
        private const int SequenceEnd = 0xfe0f;

        /// <summary>
        /// Checks if UTF32 character is a beginning of an emoji sequence (apart from
        /// some common characters like “©” or “®”).
        /// </summary>
        private static bool IsEmoji(int c) {
            /* 22 times faster than using Contains() of a huge list. */
            return c == 0x203c || c == 0x2049 || c == 0x2122 || c == 0x2139 || c >= 0x2194 && c <= 0x2199 || c == 0x21a9 || c == 0x21aa || c == 0x231a ||
                    c == 0x231b || c == 0x2328 || c == 0x23cf || c >= 0x23e9 && c <= 0x23f3 || c >= 0x23f8 && c <= 0x23fa || c == 0x24c2 || c == 0x25aa ||
                    c == 0x25ab || c == 0x25b6 || c == 0x25c0 || c >= 0x25fb && c <= 0x25fe || c >= 0x2600 && c <= 0x2604 || c == 0x260e || c == 0x2611 ||
                    c == 0x2614 || c == 0x2615 || c == 0x2618 || c == 0x261d || c == 0x2620 || c == 0x2622 || c == 0x2623 || c == 0x2626 || c == 0x262a ||
                    c == 0x262e || c == 0x262f || c >= 0x2638 && c <= 0x263a || c == 0x2640 || c == 0x2642 || c >= 0x2648 && c <= 0x2653 || c == 0x2660 ||
                    c == 0x2663 || c == 0x2665 || c == 0x2666 || c == 0x2668 || c == 0x267b || c == 0x267f || c >= 0x2692 && c <= 0x2697 || c == 0x2699 ||
                    c == 0x269b || c == 0x269c || c == 0x26a0 || c == 0x26a1 || c == 0x26aa || c == 0x26ab || c == 0x26b0 || c == 0x26b1 || c == 0x26bd ||
                    c == 0x26be || c == 0x26c4 || c == 0x26c5 || c == 0x26c8 || c == 0x26ce || c == 0x26cf || c == 0x26d1 || c == 0x26d3 || c == 0x26d4 ||
                    c == 0x26e9 || c == 0x26ea || c >= 0x26f0 && c <= 0x26f5 || c >= 0x26f7 && c <= 0x26fa || c == 0x26fd || c == 0x2702 || c == 0x2705 ||
                    c >= 0x2708 && c <= 0x270d || c == 0x270f || c == 0x2712 || c == 0x2714 || c == 0x2716 || c == 0x271d || c == 0x2721 || c == 0x2728 ||
                    c == 0x2733 || c == 0x2734 || c == 0x2744 || c == 0x2747 || c == 0x274c || c == 0x274e || c >= 0x2753 && c <= 0x2755 || c == 0x2757 ||
                    c == 0x2763 || c == 0x2764 || c >= 0x2795 && c <= 0x2797 || c == 0x27a1 || c == 0x27b0 || c == 0x27bf || c == 0x2934 || c == 0x2935 ||
                    c >= 0x2b05 && c <= 0x2b07 || c == 0x2b1b || c == 0x2b1c || c == 0x2b50 || c == 0x2b55 || c == 0x3030 || c == 0x303d || c == 0x3297 ||
                    c == 0x3299 || c == 0xe50a || c == 0x1f004 || c == 0x1f0cf || c == 0x1f170 || c == 0x1f171 || c == 0x1f17e || c == 0x1f17f || c == 0x1f18e ||
                    c >= 0x1f191 && c <= 0x1f19a || c >= 0x1f1e6 && c <= 0x1f1ff || c == 0x1f201 || c == 0x1f202 || c == 0x1f21a || c == 0x1f22f ||
                    c >= 0x1f232 && c <= 0x1f23a || c == 0x1f250 || c == 0x1f251 || c >= 0x1f300 && c <= 0x1f321 || c >= 0x1f324 && c <= 0x1f393 || c == 0x1f396 ||
                    c == 0x1f397 || c >= 0x1f399 && c <= 0x1f39b || c >= 0x1f39e && c <= 0x1f3f0 || c >= 0x1f3f3 && c <= 0x1f3f5 || c >= 0x1f3f7 && c <= 0x1f4fd ||
                    c >= 0x1f4ff && c <= 0x1f53d || c >= 0x1f549 && c <= 0x1f54e || c >= 0x1f550 && c <= 0x1f567 || c == 0x1f56f || c == 0x1f570 ||
                    c >= 0x1f573 && c <= 0x1f57a || c == 0x1f587 || c >= 0x1f58a && c <= 0x1f58d || c == 0x1f590 || c == 0x1f595 || c == 0x1f596 || c == 0x1f5a4 ||
                    c == 0x1f5a5 || c == 0x1f5a8 || c == 0x1f5b1 || c == 0x1f5b2 || c == 0x1f5bc || c >= 0x1f5c2 && c <= 0x1f5c4 || c >= 0x1f5d1 && c <= 0x1f5d3 ||
                    c >= 0x1f5dc && c <= 0x1f5de || c == 0x1f5e1 || c == 0x1f5e3 || c == 0x1f5e8 || c == 0x1f5ef || c == 0x1f5f3 || c >= 0x1f5fa && c <= 0x1f64f ||
                    c >= 0x1f680 && c <= 0x1f6c5 || c >= 0x1f6cb && c <= 0x1f6d2 || c >= 0x1f6e0 && c <= 0x1f6e5 || c == 0x1f6e9 || c == 0x1f6eb || c == 0x1f6ec ||
                    c == 0x1f6f0 || c >= 0x1f6f3 && c <= 0x1f6f8 || c >= 0x1f910 && c <= 0x1f93a || c >= 0x1f93c && c <= 0x1f93e || c >= 0x1f940 && c <= 0x1f945 ||
                    c >= 0x1f947 && c <= 0x1f94c || c >= 0x1f950 && c <= 0x1f96b || c >= 0x1f980 && c <= 0x1f997 || c == 0x1f9c0 || c >= 0x1f9d0 && c <= 0x1f9e6;
        }

        /// <summary>
        /// Gets next character of the string, could be UTF32 or UTF16.
        /// </summary>
        private static int GetNextChar(string s, ref int offset) {
            if (offset == s.Length) return -1;

            if (char.IsHighSurrogate(s, offset)) {
                if (offset == s.Length - 1) return -1;

                var result = char.ConvertToUtf32(s, offset);
                offset += 2;
                return result;
            } else {
                int result = s[offset];
                offset++;
                return result;
            }
        }

        /// <summary>
        /// For emojis with different skin color, modifiers.
        /// </summary>
        private static bool IsSkinModifier(int c) {
            // Source: http://unicode.org/reports/tr51/#Emoji_Modifiers_Table
            return c >= 0x1f3fb && c <= 0x1f3ff;
        }

        /// <summary>
        /// For ♀ and ♂️.
        /// </summary>
        private static bool IsGenderModifier(int c) {
            return c == 0x2640 || c == 0x2642;
        }

        /// <summary>
        /// Chars like MAN or WOMAN.
        /// </summary>
        private static bool IsGenderPerson(int c) {
            return c == 0x1f468 || c == 0x1f469;
        }

        /// <summary>
        /// For emojis with different skin color.
        /// </summary>
        private static bool IsSkinGroup(int c) {
            // Format to ignore: <CH.> (handled automatically as a one-char emoji)
            // Format to look for: <CH.>[SKIN]
            return c == 0x261d || c == 0x26f7 || c == 0x26f9 || c >= 0x270a && c <= 0x270d || c == 0x1f385 || c >= 0x1f3c2 && c <= 0x1f3c4 || c == 0x1f3c7
                    || c >= 0x1f3ca && c <= 0x1f3cc || c == 0x1f442 || c == 0x1f443 || c >= 0x1f446 && c <= 0x1f450 || c >= 0x1f466 && c <= 0x1f469
                    || c == 0x1f46e || c >= 0x1f470 && c <= 0x1f478 || c == 0x1f47c || c >= 0x1f481 && c <= 0x1f483 || c >= 0x1f485 && c <= 0x1f487
                    || c == 0x1f4aa || c == 0x1f574 || c == 0x1f575 || c == 0x1f57a || c == 0x1f590 || c == 0x1f595 || c == 0x1f596
                    || c >= 0x1f645 && c <= 0x1f647 || c >= 0x1f64b && c <= 0x1f64f || c == 0x1f6a3 || c >= 0x1f6b4 && c <= 0x1f6b6 || c == 0x1f6c0
                    || c == 0x1f6cc || c >= 0x1f918 && c <= 0x1f91c || c == 0x1f91e || c == 0x1f91f || c == 0x1f926 || c >= 0x1f930 && c <= 0x1f939
                    || c == 0x1f93d || c == 0x1f93e || c >= 0x1f9d1 && c <= 0x1f9dd;
        }

        /// <summary>
        /// For emojis with different gender.
        /// </summary>
        private static bool IsGenderGroup(int c) {
            // Format to ignore: <CH.> (handled automatically as a one-char emoji)
            // Format to look for: <CH.>+[GENDER]0xFE0F
            return c == 0x1f46f || c == 0x1f93c || c == 0x1f9de || c == 0x1f9df;
        }

        /// <summary>
        /// For emojis with different skin color and gender.
        /// </summary>
        private static bool IsSkinGenderGroup(int c) {
            // Should be called before IsSkinGroup()!
            // Format to ignore: <CH.> (handled automatically as a one-char emoji), <CH.>[SKIN] (handled within IsSkinGroup())
            // Format to look for: <CH.>+[GENDER]0xFE0F, <CH.>[SKIN]+[GENDER]0xFE0F
            return c == 0x26f9 || c == 0x1f3c3 || c == 0x1f3c4 || c >= 0x1f3ca && c <= 0x1f3cc || c == 0x1f46e || c == 0x1f471 || c == 0x1f473 || c == 0x1f477 ||
                    c == 0x1f481 || c == 0x1f482 || c == 0x1f486 || c == 0x1f487 || c == 0x1f575 || c >= 0x1f645 && c <= 0x1f647 || c == 0x1f64b || c == 0x1f64d ||
                    c == 0x1f64e || c == 0x1f6a3 || c >= 0x1f6b4 && c <= 0x1f6b6 || c == 0x1f926 || c >= 0x1f937 && c <= 0x1f939 || c == 0x1f93d || c == 0x1f93e ||
                    c >= 0x1f9d6 && c <= 0x1f9dd;
        }

        /// <summary>
        /// For professional emojis with different skin color and gender. For forth symbol in sequence, where first two are gender and race,
        /// </summary>
        private static bool IsSkinGenderProfessionGroup(int c) {
            // Format to look for: <PERSON>+<CH.>, <PERSON>[SKIN]+<CH.>
            return c == 0x1f33e || c == 0x1f373 || c == 0x1f393 || c == 0x1f3a4 || c == 0x1f3a8 || c == 0x1f3eb || c == 0x1f3ed || c == 0x1f4bb || c == 0x1f4bc ||
                    c == 0x1f527 || c == 0x1f52c || c == 0x1f680 || c == 0x1f692;
        }

        /// <summary>
        /// For professional emojis with different skin color and gender. For forth symbol in sequence, where first two are gender and race,
        /// </summary>
        private static bool IsSkinGenderProfessionSeGroup(int c) {
            // Format to look for: <PERSON>+<CH.>0xFE0F, <PERSON>[SKIN]+<CH.>0xFE0F
            return c == 0x2695 || c == 0x2696 || c == 0x2708;
        }

        /// <summary>
        /// For flags, starting symbol.
        /// </summary>
        private static bool IsFlagPrefix(int c) {
            return c >= 0x1f1e6 && c <= 0x1f1ff;
        }

        /// <summary>
        /// Takes group of two emojis making a country flag. As usual, there is no sense in those numbers, so I believe this is
        /// one of the fastest ways to do it.
        /// </summary>
        private static int GetFlagGroup(int group, string s, int offset) {
            var nextOffset = offset;
            var c = GetNextChar(s, ref nextOffset);
            var result = nextOffset - offset;

            switch (group) {
                case 0x1f1e6:
                    return c >= 0x1f1e8 && c <= 0x1f1ec || c == 0x1f1ee || c == 0x1f1f1 || c == 0x1f1f2 || c == 0x1f1f4 || c >= 0x1f1f6 && c <= 0x1f1fa ||
                            c == 0x1f1fc || c == 0x1f1fd || c == 0x1f1ff ? result : 0;
                case 0x1f1e7:
                    return c == 0x1f1e6 || c == 0x1f1e7 || c >= 0x1f1e9 && c <= 0x1f1ef || c >= 0x1f1f1 && c <= 0x1f1f4 || c >= 0x1f1f6 && c <= 0x1f1f9 ||
                            c == 0x1f1fb || c == 0x1f1fc || c == 0x1f1fe || c == 0x1f1ff ? result : 0;
                case 0x1f1e8:
                    return c == 0x1f1e6 || c == 0x1f1e8 || c == 0x1f1e9 || c >= 0x1f1eb && c <= 0x1f1ee || c >= 0x1f1f0 && c <= 0x1f1f5 || c == 0x1f1f7 ||
                            c >= 0x1f1fa && c <= 0x1f1ff ? result : 0;
                case 0x1f1e9:
                    return c == 0x1f1ea || c == 0x1f1ec || c == 0x1f1ef || c == 0x1f1f0 || c == 0x1f1f2 || c == 0x1f1f4 || c == 0x1f1ff ? result : 0;
                case 0x1f1ea:
                    return c == 0x1f1e6 || c == 0x1f1e8 || c == 0x1f1ea || c == 0x1f1ec || c == 0x1f1ed || c >= 0x1f1f7 && c <= 0x1f1fa ? result : 0;
                case 0x1f1eb:
                    return c >= 0x1f1ee && c <= 0x1f1f0 || c == 0x1f1f2 || c == 0x1f1f4 || c == 0x1f1f7 ? result : 0;
                case 0x1f1ec:
                    return c == 0x1f1e6 || c == 0x1f1e7 || c >= 0x1f1e9 && c <= 0x1f1ee || c >= 0x1f1f1 && c <= 0x1f1f3 || c >= 0x1f1f5 && c <= 0x1f1fa ||
                            c == 0x1f1fc || c == 0x1f1fe ? result : 0;
                case 0x1f1ed:
                    return c == 0x1f1f0 || c == 0x1f1f2 || c == 0x1f1f3 || c == 0x1f1f7 || c == 0x1f1f9 || c == 0x1f1fa ? result : 0;
                case 0x1f1ee:
                    return c >= 0x1f1e8 && c <= 0x1f1ea || c >= 0x1f1f1 && c <= 0x1f1f4 || c >= 0x1f1f6 && c <= 0x1f1f9 ? result : 0;
                case 0x1f1ef:
                    return c == 0x1f1ea || c == 0x1f1f2 || c == 0x1f1f4 || c == 0x1f1f5 ? result : 0;
                case 0x1f1f0:
                    return c == 0x1f1ea || c >= 0x1f1ec && c <= 0x1f1ee || c == 0x1f1f2 || c == 0x1f1f3 || c == 0x1f1f5 || c == 0x1f1f7 || c == 0x1f1fc ||
                            c == 0x1f1fe || c == 0x1f1ff ? result : 0;
                case 0x1f1f1:
                    return c >= 0x1f1e6 && c <= 0x1f1e8 || c == 0x1f1ee || c == 0x1f1f0 || c >= 0x1f1f7 && c <= 0x1f1fb || c == 0x1f1fe ? result : 0;
                case 0x1f1f2:
                    return c == 0x1f1e6 || c >= 0x1f1e8 && c <= 0x1f1ed || c >= 0x1f1f0 && c <= 0x1f1ff ? result : 0;
                case 0x1f1f3:
                    return c == 0x1f1e6 || c == 0x1f1e8 || c >= 0x1f1ea && c <= 0x1f1ec || c == 0x1f1ee || c == 0x1f1f1 || c == 0x1f1f4 || c == 0x1f1f5 ||
                            c == 0x1f1f7 || c == 0x1f1fa || c == 0x1f1ff ? result : 0;
                case 0x1f1f4:
                    return c == 0x1f1f2 ? result : 0;
                case 0x1f1f5:
                    return c == 0x1f1e6 || c >= 0x1f1ea && c <= 0x1f1ed || c >= 0x1f1f0 && c <= 0x1f1f3 || c >= 0x1f1f7 && c <= 0x1f1f9 || c == 0x1f1fc ||
                            c == 0x1f1fe ? result : 0;
                case 0x1f1f6:
                    return c == 0x1f1e6 ? result : 0;
                case 0x1f1f7:
                    return c == 0x1f1ea || c == 0x1f1f4 || c == 0x1f1f8 || c == 0x1f1fa || c == 0x1f1fc ? result : 0;
                case 0x1f1f8:
                    return c >= 0x1f1e6 && c <= 0x1f1ea || c >= 0x1f1ec && c <= 0x1f1f4 || c >= 0x1f1f7 && c <= 0x1f1f9 || c == 0x1f1fb ||
                            c >= 0x1f1fd && c <= 0x1f1ff ? result : 0;
                case 0x1f1f9:
                    return c == 0x1f1e6 || c == 0x1f1e8 || c == 0x1f1e9 || c >= 0x1f1eb && c <= 0x1f1ed || c >= 0x1f1ef && c <= 0x1f1f4 || c == 0x1f1f7 ||
                            c == 0x1f1f9 || c == 0x1f1fb || c == 0x1f1fc || c == 0x1f1ff ? result : 0;
                case 0x1f1fa:
                    return c == 0x1f1e6 || c == 0x1f1ec || c == 0x1f1f2 || c == 0x1f1f3 || c == 0x1f1f8 || c == 0x1f1fe || c == 0x1f1ff ? result : 0;
                case 0x1f1fb:
                    return c == 0x1f1e6 || c == 0x1f1e8 || c == 0x1f1ea || c == 0x1f1ec || c == 0x1f1ee || c == 0x1f1f3 || c == 0x1f1fa ? result : 0;
                case 0x1f1fc:
                    return c == 0x1f1eb || c == 0x1f1f8 ? result : 0;
                case 0x1f1fd:
                    return c == 0x1f1f0 ? result : 0;
                case 0x1f1fe:
                    return c == 0x1f1ea || c == 0x1f1f9 ? result : 0;
                case 0x1f1ff:
                    return c == 0x1f1e6 || c == 0x1f1f2 || c == 0x1f1fc ? result : 0;
                default:
                    return 0;
            }
        }

        /// <summary>
        /// Gets next joined character if any.
        /// </summary>
        /// <param name="s">String to search in.</param>
        /// <param name="baseOffset">Offset of a base character within a string.</param>
        /// <param name="value">Found character if any.</param>
        /// <param name="offset">Offset of a found character if any.</param>
        /// <returns>True if joined character is found.</returns>
        private static bool GetNextJoinedChar(string s, int baseOffset, ref int value, ref int offset) {
            if (value != 0 || baseOffset == 0) return value > 0;

            offset = baseOffset;
            value = GetNextChar(s, ref offset);

            if (value != ZeroWidthSpace) {
                value = -1;
                return false;
            }

            value = GetNextChar(s, ref offset);
            return true;
        }

        /// <summary>
        /// Takes group of emojis combined using zero-width joiner. I’ve tried to keep it fast.
        /// </summary>
        private static int GetJoinedGroup(int[][] array, int firstChar, string s, int offset) {
            int secondChar = 0, secondOffset = 0;
            int thirdChar = 0, thirdOffset = 0;
            int fourthChar = 0, fourthOffset = 0;
            if (!GetNextJoinedChar(s, offset, ref secondChar, ref secondOffset)) return 0;

            var result = 0;
            for (var i = 0; i < array.Length; i++) {
                var combo = array[i];
                if (combo[0] != firstChar || combo[1] != secondChar) continue;
                switch (combo.Length) {
                    case 2:
                        result = Math.Max(result, secondOffset - offset);
                        break;
                    case 3:
                        if (!GetNextJoinedChar(s, secondOffset, ref thirdChar, ref thirdOffset) || combo[2] != thirdChar) continue;
                        result = Math.Max(result, thirdOffset - offset);
                        break;
                    case 4:
                        if (!GetNextJoinedChar(s, secondOffset, ref thirdChar, ref thirdOffset) || combo[2] != thirdChar ||
                                !GetNextJoinedChar(s, thirdOffset, ref fourthChar, ref fourthOffset) || combo[3] != fourthChar) continue;
                        result = Math.Max(result, fourthOffset - offset);
                        break;
                    default:
                        throw new NotSupportedException();
                }
            }

            return result;
        }

        /// <summary>
        /// Takes group of emojis combined (without zero-width joiner).
        /// </summary>
        private static int GetNonjoinedGroup(int[][] array, int firstChar, string s, int offset) {
            var nextOffset = offset;
            var secondChar = GetNextChar(s, ref nextOffset);
            var extraChars = new List<Tuple<int, int>>();

            var result = 0;
            for (var i = 0; i < array.Length; i++) {
                var combo = array[i];
                if (combo[0] != firstChar || combo[1] != secondChar) continue;

                for (int j = 0, extraLength = combo.Length - 2; j < extraLength; j++) {
                    if (extraChars.Count <= j) {
                        var nextChar = GetNextChar(s, ref offset);
                        extraChars.Add(Tuple.Create(nextChar, nextOffset));
                        nextOffset = offset;
                    }

                    var pair = extraChars[j];
                    if (pair.Item1 != combo[j + 2]) {
                        result = Math.Max(result, pair.Item2) - offset;
                        goto Next;
                    }
                }

                result = Math.Max(result, nextOffset) - offset;

                Next:
                { }
            }

            return result;
        }

        /// <summary>
        /// For sets combined without zero-width joiner.
        /// </summary>
        private static readonly int[][] ExtraFlags = {
            new[] { 0x1f3f3, 0xfe0f, 0x200d, 0x1f308 },
            new[] { 0x1f3f4, 0x200d, 0x2620, 0xfe0f },
            new[] { 0x1f3f4, 0xe0067, 0xe0062, 0xe0077, 0xe006c, 0xe0073, 0xe007f },
            new[] { 0x1f3f4, 0xe0067, 0xe0062, 0xe0065, 0xe006e, 0xe0067, 0xe007f },
            new[] { 0x1f3f4, 0xe0067, 0xe0062, 0xe0073, 0xe0063, 0xe0074, 0xe007f }
        };

        /// <summary>
        /// Takes miscellaneous group of three or more emojis combined without zero-width joiner.
        /// </summary>
        private static int GetExtraFlagsGroup(int c, string s, int offset) {
            return c == 0x1f3f3 || c == 0x1f3f4
                    ? GetNonjoinedGroup(ExtraFlags, c, s, offset) : 0;
        }

        /// <summary>
        /// For bunches of people combined using zero-width joiner (0x200d).
        /// </summary>
        private static readonly int[][] Company = {
            new[] { 0x1f441, 0x1f5e8 },
            new[] { 0x1f468, 0x1f466 },
            new[] { 0x1f468, 0x1f466, 0x1f466 },
            new[] { 0x1f468, 0x1f467, 0x1f466 },
            new[] { 0x1f468, 0x1f467, 0x1f467 },
            new[] { 0x1f468, 0x1f467 },
            new[] { 0x1f468, 0x1f468, 0x1f466, 0x1f466 },
            new[] { 0x1f468, 0x1f468, 0x1f466 },
            new[] { 0x1f468, 0x1f468, 0x1f467 },
            new[] { 0x1f468, 0x1f468, 0x1f467, 0x1f466 },
            new[] { 0x1f468, 0x1f468, 0x1f467, 0x1f467 },
            new[] { 0x1f468, 0x1f469, 0x1f466, 0x1f466 },
            new[] { 0x1f468, 0x1f469, 0x1f466 },
            new[] { 0x1f468, 0x1f469, 0x1f467, 0x1f466 },
            new[] { 0x1f468, 0x1f469, 0x1f467 },
            new[] { 0x1f468, 0x1f469, 0x1f467, 0x1f467 },
            new[] { 0x1f469, 0x1f466 },
            new[] { 0x1f469, 0x1f466, 0x1f466 },
            new[] { 0x1f469, 0x1f467 },
            new[] { 0x1f469, 0x1f467, 0x1f466 },
            new[] { 0x1f469, 0x1f467, 0x1f467 },
            new[] { 0x1f469, 0x1f469, 0x1f466, 0x1f466 },
            new[] { 0x1f469, 0x1f469, 0x1f466 },
            new[] { 0x1f469, 0x1f469, 0x1f467 },
            new[] { 0x1f469, 0x1f469, 0x1f467, 0x1f466 },
            new[] { 0x1f469, 0x1f469, 0x1f467, 0x1f467 }
        };

        /// <summary>
        /// Takes bunches of people combined using zero-width joiner.
        /// </summary>
        private static int GetCompanyGroup(int c, string s, int offset) {
            return c == 0x1f441 || c == 0x1f468 || c == 0x1f469
                    ? GetJoinedGroup(Company, c, s, offset) : 0;
        }

        /// <summary>
        /// For special sets combined using zero-width joiner (0x200d).
        /// </summary>
        private static readonly int[][] Misc = {
            new[] { 0x1f468, 0x200d, 0x2764, 0xfe0f, 0x200d, 0x1f468 },
            new[] { 0x1f468, 0x200d, 0x2764, 0xfe0f, 0x200d, 0x1f48b, 0x200d, 0x1f468 },
            new[] { 0x1f469, 0x200d, 0x2764, 0xfe0f, 0x200d, 0x1f468 },
            new[] { 0x1f469, 0x200d, 0x2764, 0xfe0f, 0x200d, 0x1f469 },
            new[] { 0x1f469, 0x200d, 0x2764, 0xfe0f, 0x200d, 0x1f48b, 0x200d, 0x1f468 },
            new[] { 0x1f469, 0x200d, 0x2764, 0xfe0f, 0x200d, 0x1f48b, 0x200d, 0x1f469 }
        };

        /// <summary>
        /// Takes miscellaneous group of three or more emojis combined using zero-width joiner.
        /// </summary>
        /// <returns>Length of group found.</returns>
        private static int GetMiscGroup(int c, string s, int offset) {
            return c == 0x1f468 || c == 0x1f469
                    ? GetNonjoinedGroup(Misc, c, s, offset) : 0;
        }

        private const int ZeroWidthSpaceLength = 1;
        private const int SequenceEndLength = 1;
        private const int SkinToneModifierLength = 2;
        private const int GenderModifierLength = 1;

        /// <summary>
        /// Checks if character at given offset is an emoji (apart from “©” and “®” because it’s too much).
        /// </summary>
        /// <param name="s">String to look into.</param>
        /// <param name="offset">Offset to look to.</param>
        /// <param name="length">Length of given emoji (most of them are UTF32, so it’s two, and some of them are combined).</param>
        /// <returns>If character is emoji character.</returns>
        public static bool IsEmoji(string s, int offset, out int length) {
            if (s[offset] < Threshold){
                length = 1;
                return false;
            }

            int code;

            if (char.IsHighSurrogate(s, offset)) {
                code = char.ConvertToUtf32(s, offset);
                length = 2;
            } else {
                code = s[offset];
                length = 1;
            }

            if (!IsEmoji(code)) {
                return false;
            }

            var miscGroup = GetMiscGroup(code, s, offset + length);
            if (miscGroup != 0) {
                length += miscGroup;
                return true;
            }

            var extraFlagsGroup = GetExtraFlagsGroup(code, s, offset + length);
            if (extraFlagsGroup != 0) {
                length += extraFlagsGroup;
                return true;
            }

            var companyGroup = GetCompanyGroup(code, s, offset + length);
            if (companyGroup != 0) {
                length += companyGroup;
                return true;
            }

            if (IsGenderPerson(code) && offset + length + ZeroWidthSpaceLength + 1 <= s.Length) {
                var localOffset = offset + length;
                var next = char.ConvertToUtf32(s, localOffset);
                if (next == ZeroWidthSpace) {
                    // <PERSON>+<CH.>
                    var profession = char.ConvertToUtf32(s, localOffset + ZeroWidthSpaceLength);
                    var professionLength = char.IsHighSurrogate(s, localOffset + ZeroWidthSpaceLength) ? 2 : 1;
                    if (IsSkinGenderProfessionGroup(profession)) {
                        length += ZeroWidthSpaceLength + professionLength;
                        return true;
                    }

                    var afterProfessionOffset = localOffset + ZeroWidthSpace + professionLength;
                    if (IsSkinGenderProfessionSeGroup(profession)
                            && afterProfessionOffset + SequenceEndLength <= s.Length
                            && char.ConvertToUtf32(s, afterProfessionOffset) == SequenceEnd) {
                        length += ZeroWidthSpaceLength + professionLength + SequenceEndLength;
                        return true;
                    }
                } else if (IsSkinModifier(next) && offset + length + SkinToneModifierLength + ZeroWidthSpaceLength + 1 <= s.Length) {
                    localOffset = offset + length + SkinToneModifierLength;
                    next = char.ConvertToUtf32(s, localOffset);
                    if (next == ZeroWidthSpace) {
                        // <PERSON>[SKIN]+<CH.>
                        var profession = char.ConvertToUtf32(s, localOffset + ZeroWidthSpaceLength);
                        var professionLength = char.IsHighSurrogate(s, localOffset + ZeroWidthSpaceLength) ? 2 : 1;
                        if (IsSkinGenderProfessionGroup(profession)) {
                            length += SkinToneModifierLength + ZeroWidthSpaceLength + professionLength;
                            return true;
                        }

                        var afterProfessionOffset = localOffset + ZeroWidthSpace + professionLength;
                        if (IsSkinGenderProfessionSeGroup(profession)
                                && afterProfessionOffset + SequenceEndLength <= s.Length
                                && char.ConvertToUtf32(s, afterProfessionOffset) == SequenceEnd) {
                            length += ZeroWidthSpaceLength + professionLength + SequenceEndLength;
                            return true;
                        }
                    }
                }
            }

            if (IsSkinGenderGroup(code) && offset + length + ZeroWidthSpaceLength + GenderModifierLength + SequenceEndLength <= s.Length) {
                var localOffset = offset + length;
                var next = char.ConvertToUtf32(s, localOffset);

                // <CH.>+[GENDER]0xFE0F
                if (next == ZeroWidthSpace
                        && IsGenderModifier(char.ConvertToUtf32(s, localOffset + ZeroWidthSpaceLength))
                        && char.ConvertToUtf32(s, localOffset + ZeroWidthSpaceLength + GenderModifierLength) == SequenceEnd) {
                    length += ZeroWidthSpaceLength + GenderModifierLength + SequenceEndLength;
                    return true;
                }

                // <CH.>[SKIN]+[GENDER]0xFE0F
                if (IsSkinModifier(next) && offset + length + SkinToneModifierLength + ZeroWidthSpaceLength + GenderModifierLength +
                        SequenceEndLength <= s.Length) {
                    localOffset = offset + length + SkinToneModifierLength;
                    next = char.ConvertToUtf32(s, localOffset);
                    if (next == ZeroWidthSpace
                            && IsGenderModifier(char.ConvertToUtf32(s, localOffset + ZeroWidthSpaceLength))
                            && char.ConvertToUtf32(s, localOffset + ZeroWidthSpaceLength + GenderModifierLength) == SequenceEnd) {
                        length += SkinToneModifierLength + ZeroWidthSpaceLength + GenderModifierLength + SequenceEndLength;
                        return true;
                    }
                }
            }

            if (IsGenderGroup(code) && offset + length + ZeroWidthSpaceLength + GenderModifierLength + SequenceEndLength <= s.Length) {
                // <CH.>+[GENDER]0xFE0F
                var localOffset = offset + length;
                var next = char.ConvertToUtf32(s, localOffset);
                if (next == ZeroWidthSpace
                        && IsGenderModifier(char.ConvertToUtf32(s, localOffset + ZeroWidthSpaceLength))
                        && char.ConvertToUtf32(s, localOffset + ZeroWidthSpaceLength + GenderModifierLength) == SequenceEnd) {
                    length += ZeroWidthSpaceLength + GenderModifierLength + SequenceEndLength;
                    return true;
                }
            }

            if (IsSkinGroup(code) && offset + length + SkinToneModifierLength <= s.Length) {
                var next = char.ConvertToUtf32(s, offset + length);
                if (IsSkinModifier(next)) {
                    length += SkinToneModifierLength;
                    return true;
                }
            }

            if (IsFlagPrefix(code)) {
                var two = GetFlagGroup(code, s, offset + length);
                if (two != 0) {
                    length += two;
                    return true;
                }
            }

            // Single-char emoji
            return true;
        }
    }
}