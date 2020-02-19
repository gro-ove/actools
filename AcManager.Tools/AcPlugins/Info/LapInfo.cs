using System.Runtime.Serialization;

namespace AcManager.Tools.AcPlugins.Info {
    [DataContract]
    public class LapInfo {
        [DataMember]
        public int ConnectionId { get; set; }

        [DataMember]
        public long Timestamp { get; set; }

        [DataMember]
        public uint LapTime { get; set; }

        [DataMember]
        public float LapLength { get; set; }

        [DataMember]
        public ushort LapNo { get; set; }

        [DataMember]
        public ushort Position { get; set; }

        [DataMember]
        public byte Cuts { get; set; }

        [DataMember]
        public float GripLevel { get; set; }
    }
}