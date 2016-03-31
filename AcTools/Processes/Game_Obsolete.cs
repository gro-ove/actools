using AcTools.Utils;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using AcTools.DataFile;

namespace AcTools.Processes {
    public partial class Game {
        [Obsolete]
        public static void PrepareIni(string carName, string skinName, string trackName, string trackConfig) {
            var ini = Path.Combine(FileUtils.GetDocumentsCfgDirectory(), "race.ini");

            IniFile.Write(ini, "RACE", "MODEL", carName);
            IniFile.Write(ini, "RACE", "SKIN", skinName);
            IniFile.Write(ini, "CAR_0", "SKIN", skinName);
            IniFile.Write(ini, "RACE", "TRACK", trackName);
            IniFile.Write(ini, "RACE", "CONFIG_TRACK", trackConfig ?? "");
        }
        
        [Obsolete]
        private static void EnableGhostCar() {
            var ini = Path.Combine(FileUtils.GetDocumentsCfgDirectory(), "race.ini");

            IniFile.Write(ini, "GHOST_CAR", "RECORDING", "1");
            IniFile.Write(ini, "GHOST_CAR", "PLAYING", "1");
            IniFile.Write(ini, "GHOST_CAR", "SECONDS_ADVANTAGE", "0");
            IniFile.Write(ini, "GHOST_CAR", "LOAD", "1");
            IniFile.Write(ini, "GHOST_CAR", "FILE", "");
            IniFile.Write(ini, "GHOST_CAR", "ENABLED", "0");
        }
        
        [Obsolete]
        private static void DisableGhostCar() {
            var ini = Path.Combine(FileUtils.GetDocumentsCfgDirectory(), "race.ini");

            IniFile.Write(ini, "GHOST_CAR", "RECORDING", "0");
            IniFile.Write(ini, "GHOST_CAR", "PLAYING", "0");
            IniFile.Write(ini, "GHOST_CAR", "SECONDS_ADVANTAGE", "0");
            IniFile.Write(ini, "GHOST_CAR", "LOAD", "1");
            IniFile.Write(ini, "GHOST_CAR", "FILE", "");
            IniFile.Write(ini, "GHOST_CAR", "ENABLED", "0");
        }
        
        [Obsolete]
        public static void PrepareIniHotlapMode() {
            var ini = Path.Combine(FileUtils.GetDocumentsCfgDirectory(), "race.ini");
            EnableGhostCar();

            IniFile.Write(ini, "RACE", "CARS", "1");
            IniFile.Write(ini, "RACE", "AI_LEVEL", "90");
            IniFile.Write(ini, "RACE", "DRIFT_MODE", "0");
            IniFile.Write(ini, "RACE", "RACE_LAPS", "5");
            IniFile.Write(ini, "RACE", "FIXED_SETUP", "0");
            IniFile.Write(ini, "RACE", "PENALTIES", "1");

            IniFile.Write(ini, "GROOVE", "VIRTUAL_LAPS", "10");
            IniFile.Write(ini, "GROOVE", "MAX_LAPS", "1");
            IniFile.Write(ini, "GROOVE", "STARTING_LAPS", "1");

            IniFile.Write(ini, "SESSION_0", "NAME", "Hotlap");
            IniFile.Write(ini, "SESSION_0", "TYPE", "4");
            IniFile.Write(ini, "SESSION_0", "DURATION_MINUTES", "0");
            IniFile.Write(ini, "SESSION_0", "SPAWN_SET", "HOTLAP_START");
        }
        
        [Obsolete]
        public static void PrepareIniDriftMode() {
            var ini = Path.Combine(FileUtils.GetDocumentsCfgDirectory(), "race.ini");
            DisableGhostCar();

            IniFile.Write(ini, "RACE", "CARS", "1");
            IniFile.Write(ini, "RACE", "AI_LEVEL", "90");
            IniFile.Write(ini, "RACE", "DRIFT_MODE", "0");
            IniFile.Write(ini, "RACE", "RACE_LAPS", "5");
            IniFile.Write(ini, "RACE", "FIXED_SETUP", "0");
            IniFile.Write(ini, "RACE", "PENALTIES", "1");

            IniFile.Write(ini, "GROOVE", "VIRTUAL_LAPS", "10");
            IniFile.Write(ini, "GROOVE", "MAX_LAPS", "1");
            IniFile.Write(ini, "GROOVE", "STARTING_LAPS", "1");

            IniFile.Write(ini, "SESSION_0", "NAME", "Drift Session");
            IniFile.Write(ini, "SESSION_0", "TYPE", "6");
            IniFile.Write(ini, "SESSION_0", "SPAWN_SET", "PIT");
        }
        
        [Obsolete]
        public static void PrepareIniPracticeMode() {
            var ini = Path.Combine(FileUtils.GetDocumentsCfgDirectory(), "race.ini");
            DisableGhostCar();

            IniFile.Write(ini, "RACE", "CARS", "1");
            IniFile.Write(ini, "RACE", "AI_LEVEL", "90");
            IniFile.Write(ini, "RACE", "DRIFT_MODE", "0");
            IniFile.Write(ini, "RACE", "RACE_LAPS", "5");
            IniFile.Write(ini, "RACE", "FIXED_SETUP", "0");
            IniFile.Write(ini, "RACE", "PENALTIES", "1");

            IniFile.Write(ini, "GROOVE", "VIRTUAL_LAPS", "10");
            IniFile.Write(ini, "GROOVE", "MAX_LAPS", "30");
            IniFile.Write(ini, "GROOVE", "STARTING_LAPS", "0");

            IniFile.Write(ini, "SESSION_0", "NAME", "Practice");
            IniFile.Write(ini, "SESSION_0", "TYPE", "1");
            IniFile.Write(ini, "SESSION_0", "DURATION_MINUTES", "0");
            IniFile.Write(ini, "SESSION_0", "SPAWN_SET", "PIT");
        }
        
        [Obsolete]
        public static void PrepareIniRaceMode(RaceProperties properties) {
            var ini = Path.Combine(FileUtils.GetDocumentsCfgDirectory(), "race.ini");

            var iniFile = new IniFile(ini);
            for (var i = 1; i < 100; i++) {
                var key = "CAR_" + i;
                if (iniFile.ContainsKey(key)) {
                    iniFile.Remove(key);
                } else {
                    break;
                }
            }
            iniFile.Save();

            DisableGhostCar();

            IniFile.Write(ini, "RACE", "CARS", properties.BotCars.Count() + 1);
            IniFile.Write(ini, "RACE", "AI_LEVEL", properties.AiLevel);
            IniFile.Write(ini, "RACE", "DRIFT_MODE", "0");
            IniFile.Write(ini, "RACE", "RACE_LAPS", properties.RaceLaps);
            IniFile.Write(ini, "RACE", "FIXED_SETUP", properties.FixedSetup);
            IniFile.Write(ini, "RACE", "PENALTIES", properties.Penalties);

            IniFile.Write(ini, "GROOVE", "VIRTUAL_LAPS", "10");
            IniFile.Write(ini, "GROOVE", "MAX_LAPS", "30");
            IniFile.Write(ini, "GROOVE", "STARTING_LAPS", "0");

            IniFile.Write(ini, "SESSION_0", "NAME", "Quick Race");
            IniFile.Write(ini, "SESSION_0", "TYPE", "3");
            IniFile.Write(ini, "SESSION_0", "LAPS", properties.RaceLaps);
            IniFile.Write(ini, "SESSION_0", "STARTING_POSITION", properties.StartingPosition);
            IniFile.Write(ini, "SESSION_0", "DURATION_MINUTES", "0");
            IniFile.Write(ini, "SESSION_0", "SPAWN_SET", "START");

            var j = 0;
            foreach (var botCar in properties.BotCars) {
                var section = "CAR_" + ++j;
                IniFile.Write(ini, section, "MODEL", botCar.CarId);
                IniFile.Write(ini, section, "MODEL_CONFIG", "");
                IniFile.Write(ini, section, "SETUP", botCar.Setup);
                IniFile.Write(ini, section, "AI_LEVEL", botCar.AiLevel);
                IniFile.Write(ini, section, "SKIN", botCar.SkinId);
                IniFile.Write(ini, section, "DRIVER_NAME", botCar.DriverName);
                IniFile.Write(ini, section, "NATIONALITY", botCar.Nationality);
            }
        }
        
        [Obsolete]
        public static void StartPractice(string acRoot, string carName, string skinName, string trackName, string trackConfig) {
            PrepareIni(carName, skinName, trackName, trackConfig);
            PrepareIniPracticeMode();
            Start(acRoot);
        }
        
        [Obsolete]
        public static void StartHotlap(string acRoot, string carName, string skinName, string trackName, string trackConfig) {
            PrepareIni(carName, skinName, trackName, trackConfig);
            PrepareIniHotlapMode();
            Start(acRoot);
        }
        
        [Obsolete]
        public static void StartDrift(string acRoot, string carName, string skinName, string trackName, string trackConfig) {
            PrepareIni(carName, skinName, trackName, trackConfig);
            PrepareIniDriftMode();
            Start(acRoot);
        }
        
        [Obsolete]
        public static void StartRace(string acRoot, string carName, string skinName, string trackName, string trackConfig, RaceProperties properties) {
            PrepareIni(carName, skinName, trackName, trackConfig);
            PrepareIniRaceMode(properties);
            Start(acRoot);
        }
        
        [Obsolete]
        public static void StartSimpleRace(string acRoot, string carName, string skinName, string trackName, string trackConfig) {
            StartRace(acRoot, carName, skinName, trackName, trackConfig, new RaceProperties {
                BotCars = new[] {
                    new AiCar { CarId = carName }, 
                    new AiCar { CarId = carName },
                    new AiCar { CarId = carName }
                },
                StartingPosition = 4
            });
        }

        [Obsolete]
        public static Process Start(string acRoot) {
            new TrickyStarter(acRoot).Run();
            return null;
        }
    }
}
