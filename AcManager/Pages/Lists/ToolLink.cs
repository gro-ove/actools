using System;
using System.Collections.Generic;
using System.Linq;
using AcTools.Utils;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI.Commands;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Presentation;

namespace AcManager.Pages.Lists {
    public class ToolLink : Link {
        public ToolLink() {
            _notAvailableReasonValue = Lazier.Create(() => {
                var list = NotAvailableReasonFunc?.Invoke().ToList();
                return list?.Count > 1 ? list.Select(x => "• " + x).JoinToString(";\n").ToSentence() : list?.Count > 0 ? list[0] : null;
            });
        }

        public string Description { get; set; }

        protected virtual Uri LaunchSource => Source;

        protected void Launch() {
            ToolsListPage.Launch(DisplayName, LaunchSource);
        }

        private DelegateCommand _launchCommand;

        public DelegateCommand LaunchCommand => _launchCommand ?? (_launchCommand = new DelegateCommand(Launch,
                () => !ToolsListPage.Holder.Active).ListenOnWeak(ToolsListPage.Holder, nameof(ToolsListPage.Holder.Active)));

        public Func<IEnumerable<string>> NotAvailableReasonFunc { get; set; }

        private readonly Lazier<string> _notAvailableReasonValue;

        public bool IsAvailable => _notAvailableReasonValue.Value == null;
        public string NotAvailableReason => _notAvailableReasonValue.Value;
    }
}