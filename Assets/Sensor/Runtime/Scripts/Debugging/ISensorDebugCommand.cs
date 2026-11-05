namespace SensorSystem
{
    /// <summary>What the QA overlay can do. Commands are written against this, never against the overlay itself.</summary>
    public interface ISensorDebugReceiver
    {
        bool IsOpen { get; }

        void SetOpen(bool open);

        /// <summary>Shows this sensor in the overlay.</summary>
        void Focus(Sensor sensor);

        /// <summary>Shows this detector in the overlay.</summary>
        void Focus(Detector detector);

        /// <summary>Puts the sensor's condition values back to what its settings asset says.</summary>
        void ResetValues(Sensor sensor);
    }

    /// <summary>
    /// A request for the QA overlay. Send one with <see cref="SensorDebug.Dispatch"/> from any code.
    /// To add your own, implement this interface. Nothing else needs to change.
    /// </summary>
    public interface ISensorDebugCommand
    {
        void Execute(ISensorDebugReceiver receiver);
    }
}
