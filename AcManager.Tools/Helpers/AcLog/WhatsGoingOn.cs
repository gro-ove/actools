using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using AcManager.Tools.Managers;
using AcManager.Tools.SemiGui;
using AcTools.Utils;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI.Helpers;
using JetBrains.Annotations;
#pragma warning disable 649

namespace AcManager.Tools.Helpers.AcLog {
    public class WhatsGoingOn {
        public WhatsGoingOnType Type { get; }

        public object[] Arguments { get; }

        /// <summary>
        /// Throws an exception if fixing failed.
        /// </summary>
        [CanBeNull]
        public Func<CancellationToken, Task> Fix { private get; set; }

        [CanBeNull]
        public string FixDisplayName { private get; set; }

        [CanBeNull]
        public string FixAffectingDataOriginalLog { private get; set; }

        public WhatsGoingOn(WhatsGoingOnType type, params object[] arguments) {
            Type = type;
            Arguments = arguments;
        }

        public static IEnumerable<string> GetCarsIds(string log) {
            return Regex.Matches(log, @"content/cars/(\w+)").Cast<Match>().Select(x => x.Groups[1].Value).Distinct().ToList();
        }

        public string GetDescription() {
            return string.Format(Type.GetDescription() ?? Type.ToString(), Arguments);
        }

        private LazierThis<NonfatalErrorSolution> _solution;

        public NonfatalErrorSolution Solution => _solution.Get(() => Fix == null ? null :
                new NonfatalErrorSolution(FixDisplayName, token => {
                    if (FixAffectingDataOriginalLog != null) {
                        var carIds = GetCarsIds(FixAffectingDataOriginalLog).ToList();
                        if (!DataUpdateWarning.Warn(carIds.Select(CarsManager.Instance.GetById))) {
                            return Task.Delay(0);
                        }
                    }

                    return Fix(token);
                }));
    }
}