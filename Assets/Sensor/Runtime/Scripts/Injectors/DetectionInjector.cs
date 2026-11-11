using System;
using System.Collections.Generic;

namespace SensorSystem
{
    /// <summary>
    /// Adds detections straight into a sensor's result, bypassing the condition tree.
    /// To add one, inherit from this class. Public fields become its settings in the inspector,
    /// and it appears in the add menu automatically. Mark runtime state [NonSerialized].
    /// </summary>
    [Serializable]
    public abstract class DetectionInjector
    {
        [NonSerialized] private string _displayName;

        public virtual string DisplayName => _displayName ??= ConditionTree.GetDisplayName(GetType());

        /// <summary>Optional one-line description of the settings, shown on collapsed inspector cards.</summary>
        public virtual string Summary => null;

        /// <summary>A problem with the settings to show on the inspector card, or null when there is none.</summary>
        public virtual string Warning => null;

        /// <summary>Called once per refresh, before Collect.</summary>
        public virtual void Prepare(in SensorContext context) { }

        /// <summary>Adds the detections to report for this refresh.</summary>
        public abstract void Collect(ICollection<IDetectable> detections);

        /// <summary>Called when the sensor turns off.</summary>
        public virtual void ResetState() { }
    }
}
