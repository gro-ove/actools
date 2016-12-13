using System;

namespace FirstFloor.ModernUI.Windows.Controls.BbCode {
    internal class Emoji {
        /// <summary>
        /// Checks if UTF32 character is an emoji character. Knows all existing emojisapart from
        /// some common characters like “©” or “®”.
        /// </summary>
        private static bool IsEmoji(int c) {
            /* Generated with:

            function proc(a){
                a.sort();
                var s = [], p = -1, r = 0;
                for (var i = 0; i <= a.length; i++){ 
                    if (a[i] == p + 1){
                        if (!r){ r = p; s.pop(); } 
                    } else {
                        if (r){ s.push(p == r + 1 ? `c==${r}||c==${p}` : `c>=${r}&&c<=${p}`); r=0 }
                        if (a[i]) s.push(\'c==\' +a[i]); 
                    }
                    p = a[i];
                }
                return s.join(\'||\');
            }
            
            22 times faster than using Contains() of a huge list. */

            return c == 126980 || c == 127183 || c == 127344 || c == 127345 || c == 127358 || c == 127359 || c == 127374 || c >= 127377 && c <= 127386 ||
                    c >= 127462 && c <= 127487 || c == 127489 || c == 127490 || c == 127514 || c == 127535 || c >= 127538 && c <= 127546 || c == 127568 ||
                    c == 127569 || c >= 127744 && c <= 127777 || c >= 127780 && c <= 127891 || c == 127894 || c == 127895 || c >= 127897 && c <= 127899 ||
                    c >= 127902 && c <= 127984 || c >= 127987 && c <= 127989 || c >= 127991 && c <= 128253 || c >= 128255 && c <= 128317 ||
                    c >= 128329 && c <= 128334 || c >= 128336 && c <= 128359 || c == 128367 || c == 128368 || c >= 128371 && c <= 128378 || c == 128391 ||
                    c >= 128394 && c <= 128397 || c == 128400 || c == 128405 || c == 128406 || c == 128420 || c == 128421 || c == 128424 || c == 128433 ||
                    c == 128434 || c == 128444 || c >= 128450 && c <= 128452 || c >= 128465 && c <= 128467 || c >= 128476 && c <= 128478 || c == 128481 ||
                    c == 128483 || c == 128488 || c == 128495 || c == 128499 || c >= 128506 && c <= 128591 || c >= 128640 && c <= 128709 ||
                    c >= 128715 && c <= 128722 || c >= 128736 && c <= 128741 || c == 128745 || c == 128747 || c == 128748 || c == 128752 ||
                    c >= 128755 && c <= 128758 || c >= 129296 && c <= 129310 || c >= 129312 && c <= 129319 || c == 129328 || c >= 129331 && c <= 129344 ||
                    c >= 129346 && c <= 129353 || c >= 129360 && c <= 129374 || c == 129376 || c == 129377 || c >= 129408 && c <= 129425 || c == 129472 ||
                    c == 8252 || c == 8265 || c == 8419 || c == 8482 || c == 8505 || c >= 8596 && c <= 8601 || c == 8617 || c == 8618 || c == 8986 || c == 8987 ||
                    c == 9000 || c == 9167 || c >= 9193 && c <= 9203 || c >= 9208 && c <= 9210 || c == 9410 || c == 9642 || c == 9643 || c == 9654 || c == 9664 ||
                    c >= 9723 && c <= 9726 || c >= 9728 && c <= 9732 || c == 9742 || c == 9745 || c == 9748 || c == 9749 || c == 9752 || c == 9757 || c == 9760 ||
                    c == 9762 || c == 9763 || c == 9766 || c == 9770 || c == 9774 || c == 9775 || c >= 9784 && c <= 9786 || c >= 9800 && c <= 9811 || c == 9824 ||
                    c == 9827 || c == 9829 || c == 9830 || c == 9832 || c == 9851 || c == 9855 || c >= 9874 && c <= 9876 || c == 9878 || c == 9879 || c == 9881 ||
                    c == 9883 || c == 9884 || c == 9888 || c == 9889 || c == 9898 || c == 9899 || c == 9904 || c == 9905 || c == 9917 || c == 9918 || c == 9924 ||
                    c == 9925 || c == 9928 || c == 9934 || c == 9935 || c == 9937 || c == 9939 || c == 9940 || c == 9961 || c == 9962 || c >= 9968 && c <= 9973 ||
                    c >= 9975 && c <= 9978 || c == 9981 || c == 9986 || c == 9989 || c >= 9992 && c <= 9997 || c == 9999 || c == 10002 || c == 10004 ||
                    c == 10006 || c == 10013 || c == 10017 || c == 10024 || c == 10035 || c == 10036 || c == 10052 || c == 10055 || c == 10060 || c == 10062 ||
                    c >= 10067 && c <= 10069 || c == 10071 || c == 10083 || c == 10084 || c >= 10133 && c <= 10135 || c == 10145 || c == 10160 || c == 10175 ||
                    c == 10548 || c == 10549 || c >= 11013 && c <= 11015 || c == 11035 || c == 11036 || c == 11088 || c == 11093 || c == 12336 || c == 12349 ||
                    c == 12951 || c == 12953;
        }

        /// <summary>
        /// Gets next character of the string, could be UTF32 or UTF16.
        /// </summary>
        private static int NextChar(string s, ref int offset) {
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
        /// For emojis with different skin color.
        /// </summary>
        private static bool IsColoredSkinGroup(int c) {
            return c == 127877 || c == 127939 || c == 127940 || c == 127943 || c == 127946 || c == 127947 || c == 128066 || c == 128067 ||
                    c >= 128070 && c <= 128080 || c >= 128102 && c <= 128105 || c == 128110 || c >= 128112 && c <= 128120 || c == 128124 ||
                    c >= 128129 && c <= 128131 || c >= 128133 && c <= 128135 || c == 128170 || c == 128373 || c == 128378 || c == 128400 || c == 128405 ||
                    c == 128406 || c >= 128581 && c <= 128583 || c >= 128587 && c <= 128591 || c == 128675 || c >= 128692 && c <= 128694 || c == 128704 ||
                    c >= 129304 && c <= 129310 || c == 129318 || c == 129328 || c >= 129331 && c <= 129337 || c >= 129339 && c <= 129342 || c == 9757 ||
                    c == 9977 || c >= 9994 && c <= 9997;
        }

        /// <summary>
        /// For emojis with different skin color, modifiers.
        /// </summary>
        private static bool IsColoredSkinModifier(int c) {
            return c >= 127995 && c <= 127999;
        }

        /// <summary>
        /// For first UTF32 characters of special sets combined using zero-width joiner.
        /// </summary>
        private static bool IsSpecialGroup(int c) {
            return c == 0x1f468 || c == 0x1f469;
        }

        /// <summary>
        /// For special sets combined using zero-width joiner (0x200d).
        /// </summary>
        private static readonly int[][] Special = {
            new[] { 0x1f468, 0x1f468, 0x1f466 },
            new[] { 0x1f468, 0x1f468, 0x1f466, 0x1f466 },
            new[] { 0x1f468, 0x1f468, 0x1f467 },
            new[] { 0x1f468, 0x1f468, 0x1f467, 0x1f466 },
            new[] { 0x1f468, 0x1f468, 0x1f467, 0x1f467 },
            new[] { 0x1f468, 0x1f469, 0x1f466, 0x1f466 },
            new[] { 0x1f468, 0x1f469, 0x1f467 },
            new[] { 0x1f468, 0x1f469, 0x1f467, 0x1f466 },
            new[] { 0x1f468, 0x1f469, 0x1f467, 0x1f467 },
            new[] { 0x1f468, 0x2764, 0x1f48b, 0x1f468 },
            new[] { 0x1f468, 0x2764, 0x1f468 },
            new[] { 0x1f469, 0x1f469, 0x1f466 },
            new[] { 0x1f469, 0x1f469, 0x1f466, 0x1f466 },
            new[] { 0x1f469, 0x1f469, 0x1f467 },
            new[] { 0x1f469, 0x1f469, 0x1f467, 0x1f466 },
            new[] { 0x1f469, 0x1f469, 0x1f467, 0x1f467 },
            new[] { 0x1f469, 0x2764, 0x1f48b, 0x1f469 },
            new[] { 0x1f469, 0x2764, 0x1f469 }
        };

        /// <summary>
        /// For emojis combined from two UTF32 characters (but not skin color related).
        /// </summary>
        private static bool IsGroupPrefix(int c) {
            return c >= 127462 && c <= 127487 || c == 127987 || c == 128065;
        }

        /// <summary>
        /// Takes group of emojis combined using zero-width joiner. I’ve tried to keep
        /// it fast.
        /// </summary>
        private static int GroupSpecial(int group, string s, int offset) {
            var nextOffset = offset;
            var next = NextChar(s, ref nextOffset);
            if (next != 0x200d) return 0;
            next = NextChar(s, ref nextOffset);

            var thirdOffset = nextOffset;
            var third = NextChar(s, ref thirdOffset);
            if (third != 0x200d) return 0;
            third = NextChar(s, ref thirdOffset);

            var fourthOffset = thirdOffset;
            int? fourth = null;

            var result = 0;

            for (var i = 0; i < Special.Length; i++) {
                var combo = Special[i];
                if (combo[0] == group && combo[1] == next && combo[2] == third) {
                    if (combo.Length == 3) {
                        result = Math.Max(result, thirdOffset - offset);
                    } else {
                        if (!fourth.HasValue) {
                            fourth = NextChar(s, ref fourthOffset);
                            if (fourth != 0x200d) return result;
                            fourth = NextChar(s, ref fourthOffset);
                        }

                        if (combo[3] != fourth.Value) continue;
                        result = fourthOffset - offset;
                    }
                }
            }

            return result;
        }

        /// <summary>
        /// Takes group of two emojis. As usual, there is no sense in those numbers, so I believe this is
        /// one of the fastest ways to do it.
        /// </summary>
        private static int GroupTwo(int group, string s, int offset) {
            /* Generated with:
            
            lines = d.split('\n').map(x => x.split('-').map(x => +x)).filter(n => n.length == 2);
            for (var i = lines[0][0], c = lines[lines.length - 1][0]; i <= c; i++){
                var a = lines.filter(x => x[0] == i).map(x => x[1]);
                if (a[0]) console.log(`case ${i}:\n  return ${proc(a)} ? result : 0;`) 
            }

            String “d” should look like “127462-127464\n127462-127465\n…”.
            */

            var nextOffset = offset;
            var c = NextChar(s, ref nextOffset);
            var result = nextOffset - offset;

            switch (group) {
                case 127462:
                    return c >= 127464 && c <= 127468 || c == 127470 || c == 127473 || c == 127474 || c == 127476 || c >= 127478 && c <= 127482 || c == 127484 ||
                            c == 127485 || c == 127487 ? result : 0;
                case 127463:
                    return c == 127462 || c == 127463 || c >= 127465 && c <= 127471 || c >= 127473 && c <= 127476 || c >= 127478 && c <= 127481 || c == 127483 ||
                            c == 127484 || c == 127486 || c == 127487 ? result : 0;
                case 127464:
                    return c == 127462 || c == 127464 || c == 127465 || c >= 127467 && c <= 127470 || c >= 127472 && c <= 127477 || c == 127479 ||
                            c >= 127482 && c <= 127487 ? result : 0;
                case 127465:
                    return c == 127466 || c == 127468 || c == 127471 || c == 127472 || c == 127474 || c == 127476 || c == 127487 ? result : 0;
                case 127466:
                    return c == 127462 || c == 127464 || c == 127466 || c == 127468 || c == 127469 || c >= 127479 && c <= 127482 ? result : 0;
                case 127467:
                    return c >= 127470 && c <= 127472 || c == 127474 || c == 127476 || c == 127479 ? result : 0;
                case 127468:
                    return c == 127462 || c == 127463 || c >= 127465 && c <= 127470 || c >= 127473 && c <= 127475 || c >= 127477 && c <= 127482 || c == 127484 ||
                            c == 127486 ? result : 0;
                case 127469:
                    return c == 127472 || c == 127474 || c == 127475 || c == 127479 || c == 127481 || c == 127482 ? result : 0;
                case 127470:
                    return c >= 127464 && c <= 127466 || c >= 127473 && c <= 127476 || c >= 127478 && c <= 127481 ? result : 0;
                case 127471:
                    return c == 127466 || c == 127474 || c == 127476 || c == 127477 ? result : 0;
                case 127472:
                    return c == 127466 || c >= 127468 && c <= 127470 || c == 127474 || c == 127475 || c == 127477 || c == 127479 || c == 127484 || c == 127486 ||
                            c == 127487 ? result : 0;
                case 127473:
                    return c >= 127462 && c <= 127464 || c == 127470 || c == 127472 || c >= 127479 && c <= 127483 || c == 127486 ? result : 0;
                case 127474:
                    return c == 127462 || c >= 127464 && c <= 127469 || c >= 127472 && c <= 127487 ? result : 0;
                case 127475:
                    return c == 127462 || c == 127464 || c >= 127466 && c <= 127468 || c == 127470 || c == 127473 || c == 127476 || c == 127477 || c == 127479 ||
                            c == 127482 || c == 127487 ? result : 0;
                case 127476:
                    return c == 127474 ? result : 0;
                case 127477:
                    return c == 127462 || c >= 127466 && c <= 127469 || c >= 127472 && c <= 127475 || c >= 127479 && c <= 127481 || c == 127484 || c == 127486
                            ? result : 0;
                case 127478:
                    return c == 127462 ? result : 0;
                case 127479:
                    return c == 127466 || c == 127476 || c == 127480 || c == 127482 || c == 127484 ? result : 0;
                case 127480:
                    return c >= 127462 && c <= 127466 || c >= 127468 && c <= 127476 || c >= 127479 && c <= 127481 || c == 127483 || c >= 127485 && c <= 127487
                            ? result : 0;
                case 127481:
                    return c == 127462 || c == 127464 || c == 127465 || c >= 127467 && c <= 127469 || c >= 127471 && c <= 127476 || c == 127479 || c == 127481 ||
                            c == 127483 || c == 127484 || c == 127487 ? result : 0;
                case 127482:
                    return c == 127462 || c == 127468 || c == 127474 || c == 127480 || c == 127486 || c == 127487 ? result : 0;
                case 127483:
                    return c == 127462 || c == 127464 || c == 127466 || c == 127468 || c == 127470 || c == 127475 || c == 127482 ? result : 0;
                case 127484:
                    return c == 127467 || c == 127480 ? result : 0;
                case 127485:
                    return c == 127472 ? result : 0;
                case 127486:
                    return c == 127466 || c == 127481 ? result : 0;
                case 127487:
                    return c == 127462 || c == 127474 || c == 127484 ? result : 0;
                case 127987:
                    return c == 127752 ? result : 0;
                case 128065:
                    return c == 128488 ? result : 0;
                default:
                    return 0;
            }
        }

        /// <summary>
        /// Checks if character at given offset is an emoji (apart from “©” and “®” because it’s too much).
        /// </summary>
        /// <param name="s">String to look into.</param>
        /// <param name="offset">Offset to look to.</param>
        /// <param name="length">Length of given emoji (most of them are UTF32, so it’s two, and some of them are combined).</param>
        /// <returns>If character is emoji character.</returns>
        public static bool IsEmoji(string s, int offset, out int length) {
            if (s[offset] < 8252){
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

            if (IsColoredSkinGroup(code) && offset + length + 1 < s.Length) {
                var next = char.ConvertToUtf32(s, offset + length);
                if (IsColoredSkinModifier(next)) {
                    length += 2;
                    return true;
                }
            }

            if (IsSpecialGroup(code)) {
                var special = GroupSpecial(code, s, offset + length);
                if (special != 0) {
                    length += special;
                    return true;
                }
            }

            if (IsGroupPrefix(code)) {
                var two = GroupTwo(code, s, offset + length);
                length += two;
            }

            return true;
        }
    }
}