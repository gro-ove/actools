using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Input;
using AcManager.Tools.AcErrors.Solutions;
using AcManager.Tools.AcObjectsNew;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI.Commands;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Presentation;
using JetBrains.Annotations;

namespace AcManager.Tools.AcErrors {
    public class AcError : NotifyPropertyChanged, IAcError {
        public AcCommonObject Target { get; }

        public AcErrorCategory Category { get; }

        public AcErrorType Type { get; }

        public string Message { get; }

        public Exception BaseException { get; private set; }

        public AcError(AcCommonObject target, AcErrorType type, params object[] args) {
            Target = target;
            Type = type;
            Category = CategoryFromType(type);

            try {
                Message = string.Format(MessageFromType(type), args.Select(x => (x as Exception)?.Message ?? x).ToArray());
            } catch (FormatException) {
                Message = Regex.Replace(MessageFromType(type), @"\{\d+\}", "?");
            }

            BaseException = args.OfType<Exception>().FirstOrDefault();

            if (Category != AcErrorCategory.CarSkin
                    && (type != AcErrorType.Data_JsonIsMissing || !Equals(args.FirstOrDefault(), @"ui_skin.json"))
                    && type != AcErrorType.Car_ParentIsMissing
                    && type != AcErrorType.Track_PreviewIsMissing
                    && type != AcErrorType.Track_OutlineIsMissing
                    && type != AcErrorType.Track_MapIsMissing) {
                Logging.Write(Message);
            }

            foreach (var exception in args.OfType<Exception>()) {
                Logging.Warning(exception);
            }
        }

        private static string MessageFromType(AcErrorType type) {
            return type.GetDescription() ?? @"?";
        }

        private static AcErrorCategory CategoryFromType(AcErrorType type) {
            AcErrorCategory result;
            if (Enum.TryParse(type.ToString().Split(new[] { '_' }, 2)[0], out result)) {
                return result;
            }

            Logging.Warning($"Can’t get category for AcErrorType: {type}");
            return AcErrorCategory.Unspecific;
        }

        #region Fixer
        private static IAcErrorFixer _acErrorFixer;

        public static void RegisterFixer(IAcErrorFixer fixer) {
            _acErrorFixer = fixer;
        }

        private ICommand _startErrorFixerCommand;

        public ICommand StartErrorFixerCommand => _startErrorFixerCommand ?? (_startErrorFixerCommand = new DelegateCommand(() => {
            _acErrorFixer?.Run(this);
        }, () => _acErrorFixer != null));
        #endregion

        #region Solutions
        private static ISolutionsFactory _factory;

        public static void RegisterSolutionsFactory(ISolutionsFactory factory) {
            _factory = factory;
        }

        [ItemNotNull]
        public Task<IEnumerable<ISolution>> GetSolutionsAsync() {
            return _factory == null ? Task.FromResult((IEnumerable<ISolution>)new ISolution[0]) : _factory.GetSolutionsAsync(this);
        }
        #endregion
    }
}
