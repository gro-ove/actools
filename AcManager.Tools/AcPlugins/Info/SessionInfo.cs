using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using AcTools.Processes;

namespace AcManager.Tools.AcPlugins.Info {
    [DataContract]
    public class SessionInfo {
        [DataMember]
        public bool MissedSessionStart { get; set; } = true;

        [DataMember]
        public string ServerName { get; set; }

        [DataMember]
        public string TrackName { get; set; }

        [DataMember]
        public string TrackConfig { get; set; }

        [DataMember]
        public string SessionName { get; set; }

        [DataMember]
        public Game.SessionType SessionType { get; set; }

        [DataMember]
        public ushort SessionDuration { get; set; }

        [DataMember]
        public ushort LapCount { get; set; }

        [DataMember]
        public ushort WaitTime { get; set; }

        [DataMember]
        public long Timestamp { get; set; } = DateTime.UtcNow.Ticks;

        [DataMember]
        public byte AmbientTemp { get; set; }

        [DataMember]
        public byte RoadTemp { get; set; }

        [DataMember]
        public string Weather { get; set; }

        [DataMember]
        public int MaxClients { get; set; }

        [DataMember]
        public int RealtimeUpdateInterval { get; set; }

        [DataMember]
        public List<DriverInfo> Drivers { get; set; } = new List<DriverInfo>();

        [DataMember]
        public List<LapInfo> Laps { get; set; } = new List<LapInfo>();

        [DataMember]
        public List<IncidentInfo> Incidents { get; set; } = new List<IncidentInfo>();

        /// <summary>
        /// Computes the distance to the closest opponent.
        /// </summary>
        /// <param name="driver">The driver.</param>
        /// <param name="opponent">The closest opponent.</param>
        /// <returns>The distance in meters.</returns>
        public float GetDistanceToClosestOpponent(DriverInfo driver, out DriverInfo opponent) {
            opponent = this.Drivers.Where(d => d != driver
                    && Math.Abs(d.LastPositionUpdate - driver.LastPositionUpdate) < 2 * RealtimeUpdateInterval)
                           .OrderBy(d => (d.LastPosition - driver.LastPosition).Length()).FirstOrDefault();
            if (opponent != null) {
                return (opponent.LastPosition - driver.LastPosition).Length();
            } else {
                return float.MaxValue;
            }
        }
    }
}