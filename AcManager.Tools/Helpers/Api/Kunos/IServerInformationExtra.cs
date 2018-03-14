using JetBrains.Annotations;
using Newtonsoft.Json.Linq;

namespace AcManager.Tools.Helpers.Api.Kunos {
    public interface IServerInformationExtra {
        /// <summary>
        /// Name and ID.
        /// </summary>
        [CanBeNull]
        string[] Country { get; }

        [CanBeNull]
        string City { get; }

        /// <summary>
        /// In seconds.
        /// </summary>
        [CanBeNull]
        long[] Durations { get; }

        /// <summary>
        /// Usual and admin passwords.
        /// </summary>
        [CanBeNull]
        string[] PasswordChecksum { get; }

        [CanBeNull]
        string Description { get; }

        [CanBeNull]
        ServerCarsInformation Players { get; }

        [CanBeNull]
        ServerInformationExtendedAssists Assists { get; }

        [CanBeNull]
        string ContentPrivate { get; }

        [CanBeNull]
        JObject Content { get; }

        [CanBeNull]
        string TrackBase { get; }

        [CanBeNull]
        string WeatherId { get; }

        int? FrequencyHz { get; }
        double? AmbientTemperature { get; }
        double? RoadTemperature { get; }
        double? WindSpeed { get; }
        double? WindDirection { get; }
        double? Grip { get; }
        double? GripTransfer { get; }
        double? MaxContactsPerKm { get; }
    }
}