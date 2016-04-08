using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace AcTools.Utils.Helpers {
    public static class ProcessExtension {
        private static string GetQuotedArgument(string argument) {
            // The argument is processed in reverse character order.
            // Any quotes (except the outer quotes) are escaped with backslash.
            // Any sequences of backslashes preceding a quote (including outer quotes) are doubled in length.
            var resultBuilder = new StringBuilder();

            var outerQuotesRequired = HasWhitespace(argument);

            var precedingQuote = false;
            if (outerQuotesRequired) {
                resultBuilder.Append('"');
                precedingQuote = true;
            }

            for (var index = argument.Length - 1; index >= 0; index--) {
                var @char = argument[index];
                resultBuilder.Append(@char);

                if (@char == '"') {
                    precedingQuote = true;
                    resultBuilder.Append('\\');
                } else if (@char == '\\' && precedingQuote) {
                    resultBuilder.Append('\\');
                } else {
                    precedingQuote = false;
                }
            }

            if (outerQuotesRequired) {
                resultBuilder.Append('"');
            }

            return Reverse(resultBuilder.ToString());
        }

        private static bool HasWhitespace(string text) {
            return text.Any(char.IsWhiteSpace);
        }

        private static string Reverse(string text) {
            return new string(text.Reverse().ToArray());
        }

        public static Process Start(string filename, IEnumerable<string> args) {
            return Process.Start(filename, args.Select(GetQuotedArgument).JoinToString(" "));
        }

        public static Task WaitForExitAsync(this Process process, CancellationToken cancellationToken = default(CancellationToken)) {
            var tcs = new TaskCompletionSource<object>();
            process.EnableRaisingEvents = true;
            process.Exited += (sender, args) => tcs.TrySetResult(null);
            if (cancellationToken != default(CancellationToken)) {
                cancellationToken.Register(() => { tcs.TrySetCanceled(); });
            }

            return tcs.Task;
        }
    }
}
