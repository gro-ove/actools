using System.Collections.Generic;
using System.IO;
using System.Linq;
using AcManager.Tools.Managers;
using AcManager.Tools.Objects;

namespace AcManager.Tools.AcErrors.Solver {
    public class Font_BitmapIsMissingSolver : AbstractSolver<FontObject> {
        public Font_BitmapIsMissingSolver(FontObject target, AcError error) : base(target, error) { }

        protected override IEnumerable<Solution> GetSolutions() {
            return TryToFindRenamedFile(Path.GetDirectoryName(Target.Location), Target.FontBitmap);
        }
    }

    public class Font_UsedButDisabledSolver : AbstractSolver<FontObject> {
        public Font_UsedButDisabledSolver(FontObject target, AcError error) : base(target, error) {}

        protected override IEnumerable<Solution> GetSolutions() {
            return new[] {
                new Solution(
                        @"Enable",
                        @"Move from “fonts-off” to “fonts”",
                        () => {
                            Target.ToggleCommand.Execute(null);
                        })
            };
        }
    }
}
