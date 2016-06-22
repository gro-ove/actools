using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using AcManager.Annotations;
using AcManager.Controls.Pages.Dialogs;
using AcManager.Pages.Dialogs;
using AcManager.Tools.AcObjectsNew;
using AcManager.Tools.Helpers;
using AcManager.Tools.Managers;
using AcManager.Tools.Objects;
using AcTools.Utils.Helpers;
using Newtonsoft.Json;

namespace AcManager.Tools.AcErrors.Solver {
    public class Car_ParentIsMissingSolver : SolverBase<CarObject> {
        public Car_ParentIsMissingSolver(CarObject target, AcError error) : base(target, error) { }

        protected override IEnumerable<Solution> GetSolutions() {
            return new[] {
                new Solution(
                    @"Make independent",
                    @"Remove id of missing parent from ui_car.json",
                    () => {
                        Target.ParentId = null;
                    }),
                new Solution(
                    @"Change parent",
                    @"Select a new parent from cars list",
                    () => {
                        new ChangeCarParentDialog(Target).ShowDialog();
                        if (Target.Parent == null) {
                            throw new SolvingException();
                        }
                    })
            }.Concat(TryToFindRenamedFile(Target.Location, Target.JsonFilename)).NonNull();
        }
    }

    public class CarSkins_SkinsAreMissingSolver : SolverBase<CarObject> {
        public CarSkins_SkinsAreMissingSolver(CarObject target, AcError error) : base(target, error) { }

        protected override IEnumerable<Solution> GetSolutions() {
            return new[] {
                new Solution(
                    @"Create empty skin",
                    @"Create a new empty skin called “default”",
                    () => {
                        var target = Path.Combine(Target.SkinsDirectory, "default");
                        Directory.CreateDirectory(target);
                        File.WriteAllText(Path.Combine(target, "ui_skin.json"), JsonConvert.SerializeObject(new {
                            skinname = "Default",
                            drivername = "",
                            country = "",
                            team = "",
                            number = 0
                        }));
                    })
            }.Union(
                Target.SkinsManager.WrappersList.Where(x => !x.Value.Enabled).Select(x => new Solution(
                    $@"Enable {x.Value.DisplayName}",
                    "Enable disabled skin",
                    () => {
                        ((CarSkinObject)x.Loaded()).ToggleCommand.Execute(null);
                    }
                ))
            ).Concat(TryToFindRenamedFile(Target.Location, Target.SkinsDirectory, true)).NonNull();
        }
    }

    public class Data_CarBrandIsMissingSolver : SolverBase<CarObject> {
        public Data_CarBrandIsMissingSolver([NotNull] CarObject target, [NotNull] AcError error) : base(target, error) { }

        protected override IEnumerable<Solution> GetSolutions() {
            var guess = AcStringValues.BrandFromName(Target.DisplayName);
            return new[] {
                new Solution(
                        @"Set brand name",
                        @"Just set a new brand name",
                        () => {
                            var value = Prompt.Show("Enter a new brand name", "New Brand Name", guess, maxLength: 200, suggestions: SuggestionLists.CarBrandsList);
                            if (value == null) throw new SolvingException();
                            Target.Brand = value;
                        }),
                guess == null ? null : new Solution(
                        @"Set brand name based on car’s name",
                        $@"New brand name will be: “{guess}”",
                        () => {
                            Target.Brand = guess;
                        })
            }.NonNull();
        }
    }
}
