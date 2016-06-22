using System.Collections.Generic;
using System.IO;
using System.Linq;
using AcManager.Controls.Pages.Dialogs;
using AcManager.Tools.AcObjectsNew;
using AcManager.Tools.Helpers;
using AcManager.Tools.Objects;
using AcTools.Utils;
using JetBrains.Annotations;
using Newtonsoft.Json.Linq;

namespace AcManager.Tools.AcErrors.Solver {

    public class Data_JsonIsDamagedSolver : SolverBase<AcJsonObjectNew> {
        public Data_JsonIsDamagedSolver(AcJsonObjectNew target, AcError error) : base(target, error) { }

        public override void OnSuccess(Solution selectedSolution) { }

        internal static Solution CreateNewFile(AcJsonObjectNew target) {
            if (target is ShowroomObject) {
                return new Solution(
                    @"Create a new file",
                    @"New file will be created containing name based on ID",
                    () => {
                        var jObject = new JObject {
                            ["name"] = AcStringValues.NameFromId(target.Id)
                        };

                        FileUtils.EnsureFileDirectoryExists(target.JsonFilename);
                        File.WriteAllText(target.JsonFilename, jObject.ToString());
                    });
            }

            if (target is CarSkinObject) {
                return new Solution(
                    @"Create a new file",
                    @"New file will be created containing name based on ID",
                    () => {
                        var jObject = new JObject {
                            ["skinname"] = AcStringValues.NameFromId(target.Id),
                            ["drivername"] = "",
                            ["country"] = "",
                            ["team"] = "",
                            ["number"] = "0",
                            ["priority"] = 1
                        };

                        FileUtils.EnsureFileDirectoryExists(target.JsonFilename);
                        File.WriteAllText(target.JsonFilename, jObject.ToString());
                    });
            }

            return null;
        }

        protected override IEnumerable<Solution> GetSolutions() {
            return new[] {
                new Solution(
                    @"Try to restore JSON file",
                    @"App will make an attempt to read known properties from damaged JSON file (be carefull, data loss is possible)",
                    () => {
                        if (!TryToRestoreDamagedJsonFile(Target.JsonFilename, JObjectRestorationSchemeProvider.GetScheme(Target))) {
                            throw new SolvingException("Can’t restore damaged JSON");
                        }
                    }),
                CreateNewFile(Target)
            }.Concat(TryToFindRenamedFile(Target.Location, Target.JsonFilename)).Where(x => x != null);
        }
    }

    public class Data_JsonIsMissingSolver : SolverBase<AcJsonObjectNew> {
        public Data_JsonIsMissingSolver(AcJsonObjectNew target, AcError error) : base(target, error) { }

        public override void OnSuccess(Solution selectedSolution) {
        }

        protected override IEnumerable<Solution> GetSolutions() {
            return new[] {
                Data_JsonIsDamagedSolver.CreateNewFile(Target)
            }.Concat(TryToFindRenamedFile(Target.Location, Target.JsonFilename)).Where(x => x != null);
        }
    }

    public class Data_ObjectNameIsMissingSolver : SolverBase<AcCommonObject> {
        public Data_ObjectNameIsMissingSolver([NotNull] AcCommonObject target, [NotNull] AcError error) : base(target, error) { }

        protected override IEnumerable<Solution> GetSolutions() {
            return new[] {
                new Solution(
                        @"Set name",
                        @"Just set a new name",
                        () => {
                            var value = Prompt.Show("Enter a new name", "New Name", AcStringValues.NameFromId(Target.Id), maxLength: 200);
                            if (value == null) throw new SolvingException();
                            Target.NameEditable = value;
                        }),
                new Solution(
                        @"Set name based on ID",
                        $@"New name will be: “{AcStringValues.NameFromId(Target.Id)}”",
                        () => {
                            Target.NameEditable = AcStringValues.NameFromId(Target.Id);
                        })
            };
        }
    }
}