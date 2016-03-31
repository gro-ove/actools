using System;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using AcTools.DataFile;
using AcTools.Kn5Render.Kn5Render;
using AcTools.Utils;

namespace AcTools.Kn5Render.Utils {
    public static class Kn5RenderWrapper {
        public static void GenerateLivery(string carDir, string skinName, string outputFile) {
            var kn5File = FileUtils.GetMainCarFilename(carDir);
            using (var render = new Render(kn5File, skinName, Render.VisualMode.LIVERY_VIEW)) {
                using (var image = (Bitmap)render.Shot(48, 48)) {
                    ImageUtils.CreateLivery(outputFile, ImageUtils.GetBaseColors(image));
                }
            }
        }

        public static void StartDarkRoomPreview(string carDir, string skinName) {
            var kn5File = FileUtils.GetMainCarFilename(carDir);

            using (var render = new Render(kn5File, skinName, Render.VisualMode.DARK_ROOM)) {
                render.Form(1280, 720);
            }
        }

        public static void StartBrightRoomPreview(string carDir, string skinName) {
            var kn5File = FileUtils.GetMainCarFilename(carDir);

            using (var render = new Render(kn5File, skinName, Render.VisualMode.BRIGHT_ROOM)) {
                render.Form(1280, 720);
            }
        }

        public static void UpdateTrackMap(string kn5File, Regex surfaceFilter) {
            var trackDir = Path.GetDirectoryName(kn5File);

            foreach (var filename in new[] { "map.png", "data/map.ini" }.Select(f => Path.Combine(trackDir ?? "", f)).Where(File.Exists)) {
                FileUtils.Recycle(filename);
            }

            using (var render = new Render(kn5File, 0, Render.VisualMode.TRACK_MAP)) {
                Render.TrackMapInformation information;
                render.ShotTrackMap(Path.Combine(trackDir ?? "", "map.png"), surfaceFilter, out information);
                information.SaveTo(Path.Combine(trackDir ?? "", "data", "map.ini"));
            }
        }

        public static void UpdateTrackMap(string kn5File, string surfaceName) {
            UpdateTrackMap(kn5File, new Regex(surfaceName, RegexOptions.Compiled));
        }

        public static void UpdateAmbientShadows(string carDir) {
            var kn5File = FileUtils.GetMainCarFilename(carDir);

            foreach (var filename in new[] { "body_shadow.png", "tyre_0_shadow.png", "tyre_1_shadow.png", "tyre_2_shadow.png", "tyre_3_shadow.png" }
                    .Select(f => Path.Combine(carDir, f)).Where(File.Exists)) {
                FileUtils.Recycle(filename);
            }

            using (var render = new Render(kn5File, 0, Render.VisualMode.BODY_SHADOW)) {
                var ini = new IniFile(carDir, "ambient_shadows.ini");
                if (ini.Mode == AbstractDataFile.StorageMode.UnpackedFile) {
                    render.AmbientBodyShadowSize.X = (float) (0.1f*Math.Round(5.0f*render.CarSize.X) + 0.1f);
                    render.AmbientBodyShadowSize.Z = (float) (0.1f*Math.Round(5.0f*render.CarSize.Z) + 0.1f);
                }

                render.ShotBodyShadow(Path.Combine(carDir, "body_shadow.png"));
                render.ShotWheelsShadow(Path.Combine(carDir, "tyre_0_shadow.png"), Path.Combine(carDir, "tyre_1_shadow.png"),
                    Path.Combine(carDir, "tyre_2_shadow.png"), Path.Combine(carDir, "tyre_3_shadow.png"));

                if (ini.Mode == AbstractDataFile.StorageMode.UnpackedFile) {
                    ini["SETTINGS"]["WIDTH"] = render.AmbientBodyShadowSize.X.ToString(CultureInfo.InvariantCulture);
                    ini["SETTINGS"]["LENGTH"] = render.AmbientBodyShadowSize.Z.ToString(CultureInfo.InvariantCulture);
                    ini.Save();
                }
            }
        }

        public static string GetBodyAmbientShadowSize(string carDir) {
            var ini = new IniFile(carDir, "ambient_shadows.ini");
            return ini["SETTINGS"].Get("WIDTH") + "," + ini["SETTINGS"].Get("LENGTH");
        }

        public static void SetBodyAmbientShadowSize(string carDir, double width, double height) {
            var ini = new IniFile(carDir, "ambient_shadows.ini");
            ini["SETTINGS"]["WIDTH"] = width.ToString(CultureInfo.InvariantCulture);
            ini["SETTINGS"]["LENGTH"] = height.ToString(CultureInfo.InvariantCulture);
            ini.Save();
        }

        public static string Shot(string carDir, string modeName) {
            var kn5File = FileUtils.GetMainCarFilename(carDir);
            var skins = Directory.GetDirectories(FileUtils.GetCarSkinsDirectory(carDir)).ToList();
            skins.Sort();

            var output = Path.Combine(Path.GetTempPath(), "AcToolsShot_" + Path.GetFileName(carDir) + "_" + DateTime.Now.Ticks);
            Directory.CreateDirectory(output);

            var mode = Render.VisualMode.DARK_ROOM;
            if (modeName == "gt5") mode = Render.VisualMode.SIMPLE_PREVIEW_GT5;
            if (modeName == "gt6") mode = Render.VisualMode.SIMPLE_PREVIEW_GT6;
            if (modeName == "kunos") mode = Render.VisualMode.DARK_ROOM;
            if (modeName == "seatleon") mode = Render.VisualMode.SIMPLE_PREVIEW_SEAT_LEON_EUROCUP;
            using (var render = new Render(kn5File, 0, mode)) {
                foreach (var skin in skins) {
                    render.ShotToFile(1024, 575, Path.Combine(output, Path.GetFileName(skin) + ".bmp"));
                    render.LoadSkin(render.SelectedSkin + 1);
                }
            }

            return output;
        }
    }
}
