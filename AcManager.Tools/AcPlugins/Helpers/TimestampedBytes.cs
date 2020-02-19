using System;

namespace AcManager.Tools.AcPlugins.Helpers {
    public class TimestampedBytes {
        public byte[] RawData;
        public DateTime IncomingDate;

        public TimestampedBytes(byte[] rawData) {
            RawData = rawData;
            IncomingDate = DateTime.Now;
        }

        public TimestampedBytes(byte[] rawData, DateTime incomingDate) {
            RawData = rawData;
            IncomingDate = incomingDate;
        }
    }
}