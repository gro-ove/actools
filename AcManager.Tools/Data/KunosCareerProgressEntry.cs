using System;
using System.Collections.Generic;
using AcTools.Utils.Helpers;
using JetBrains.Annotations;

namespace AcManager.Tools.Data {
    internal class KunosCareerProgressEntry {
        /// <summary>
        /// Milliseconds.
        /// </summary>
        internal readonly long LastSelectedTimestamp;

        /// <summary>
        /// Starts from 0.
        /// </summary>
        internal readonly int SelectedEvent;
        internal readonly IReadOnlyList<int> EventsResults;
        internal readonly int? Points;

        /// <summary>
        /// Starts with 0, of course.
        /// </summary>
        internal readonly IReadOnlyList<int> AiPoints;

        /// <summary>
        /// New instance.
        /// </summary>
        /// <param name="selectedEvent">Starts from 0</param>
        /// <param name="eventsResults"></param>
        /// <param name="points"></param>
        /// <param name="aiPoints"></param>
        /// <param name="lastSelectedTimestamp">Milliseconds</param>
        internal KunosCareerProgressEntry(int selectedEvent, [CanBeNull] IEnumerable<int> eventsResults, int? points, [CanBeNull] IEnumerable<int> aiPoints,
                long? lastSelectedTimestamp = null) {
            SelectedEvent = selectedEvent;
            EventsResults = eventsResults?.ToIReadOnlyListIfItsNot() ?? new int[0];
            Points = points;
            AiPoints = aiPoints?.ToIReadOnlyListIfItsNot() ?? new int[0];
            LastSelectedTimestamp = lastSelectedTimestamp ?? DateTime.Now.ToMillisecondsTimestamp();
        }
    }
}