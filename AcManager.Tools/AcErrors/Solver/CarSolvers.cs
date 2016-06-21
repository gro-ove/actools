using System.Collections.Generic;
using System.IO;
using System.Linq;
using AcManager.Tools.Objects;
using AcTools.Utils.Helpers;
using Newtonsoft.Json;

namespace AcManager.Tools.AcErrors.Solver {
    public class Car_ParentIsMissingSolver : AbstractSolver<CarObject> {
        public Car_ParentIsMissingSolver(CarObject target, AcError error) : base(target, error) {}

        protected override IEnumerable<Solution> GetSolutions() {
            return new [] {
                new Solution(
                    @"Make independent",
                    @"Remove id of missing parent from ui_car.json",
                    () => {
                        Target.ParentId = null;
                    })
            }.Concat(TryToFindRenamedFile(Target.Location, Target.JsonFilename)).NonNull();
        }
    }

    public class CarSkins_SkinsAreMissingSolver : AbstractSolver<CarObject> {
        public CarSkins_SkinsAreMissingSolver(CarObject target, AcError error) : base(target, error) {}

        protected override IEnumerable<Solution> GetSolutions() {
            return new [] {
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
}
