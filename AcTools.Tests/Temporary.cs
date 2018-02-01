using System;
using System.Diagnostics;
using System.Linq;
using AcTools.Utils;
using AcTools.Utils.Helpers;
using NUnit.Framework;

namespace AcTools.Tests {
    [TestFixture]
    public class Temporary {
        private static bool TryKuhn(bool[] used, int[][] g, int[] mt, int v) {
            if (used[v]) return false;
            used[v] = true;
            for (var i = 0; i < g[v].Length; i++) {
                var to = g[v][i];
                if (mt[to] == -1 || TryKuhn(used, g, mt, mt[to])) {
                    mt[to] = v;
                    return true;
                }
            }
            return false;
        }

        public static void Shuffle(int[] list) {
            for (var i = 0; i < list.Length; i++) {
                var n = MathUtils.Random(list.Length);
                var w = list[i];
                list[i] = list[n];
                list[n] = w;
            }
        }

        private static bool Dswap(int[] a, int i, int j, int d) {
            bool r = false;

            if (i == j) return true;

            if (a[i] >= j - d && a[i] <= j + d &&
                a[j] >= i - d && a[j] <= i + d) {
                r = true;
                int t = a[i];
                a[i] = a[j];
                a[j] = t;

                //if (a[i] == i) throw new Exception("Was here!");
                //if (a[j] == j) throw new Exception("Was here!");
            }

            return r;
        }

        [Test]
        public void A580375() {
            var size = 10;
            var maxDelta = 1;

            var iters = 100000;
            var counter = Enumerable.Range(0, size).Select(x => 0).ToList();

            var k = 1;

            for (var z = 0; z < iters; z++) {
                var a = new int[size];
                for (var i = 0; i < a.Length; i++) {
                    a[i] = i;
                }

                for (var i = size - 1; i >= 0; --i) {
                    for (var j = 0; j < k; j++) {
                        var h = Math.Max(i - maxDelta, 0);
                        var g = Math.Min(i + maxDelta, a.Length - 1);
                        if (Dswap(a, i, MathUtils.Random(h, g + 1), maxDelta)) {
                            // break;
                        }
                    }
                }

                for (var i = 0; i < size; i++) {
                    if (a[i] == 4) {
                        counter[i]++;
                    }
                }
            }

            Debug.WriteLine(counter.Select(x => $"{100d * x / iters:F1}%").JoinToString(", "));
        }

        [Test]
        public void A580301() {
            var size = 10;
            var maxDelta = 1;

            var iters = 100000;
            var counter = Enumerable.Range(0, size).Select(x => 0).ToList();

            var optimize = false;

            for (var z = 0; z < iters; z++) {
                var g = new int[size][];
                for (var i = 0; i < size; i++) {
                    var f = Math.Max(0, i - maxDelta);
                    var t = Math.Min(size - 1, i + maxDelta);
                    g[i] = new int[t - f + 1];
                    for (var j = f; j <= t; j++) {
                        g[i][j - f] = j;
                    }
                    Shuffle(g[i]);
                }

                var mt = new int[size];
                for (var i = 0; i < size; i++) {
                    mt[i] = -1;
                }

                // ReSharper disable once ConditionIsAlwaysTrueOrFalse
                if (optimize) {
                    var used1 = new bool[size];
                    for (var i = 0; i < size; i++) {
                        for (var j = 0; j < g[i].Length; j++) {
                            if (mt[g[i][j]] == -1) {
                                mt[g[i][j]] = i;
                                used1[i] = true;
                                break;
                            }
                        }
                    }

                    for (var i = 0; i < size; i++) {
                        if (used1[i]) continue;
                        TryKuhn(new bool[size], g, mt, i);
                    }
                } else {
                    for (var i = 0; i < size; i++) {
                        TryKuhn(new bool[size], g, mt, i);
                    }
                }

                var result = new int[size];
                for (var i = 0; i < size; i++)
                    if (mt[i] != -1) {
                        result[mt[i]] = i;
                    }

                for (var i = 0; i < size; i++) {
                    if (result[i] == 4) {
                        counter[i]++;
                    }
                }
            }

            Debug.WriteLine(counter.Select(x => $"{100d * x / iters:F1}%").JoinToString(", "));
        }
    }
}