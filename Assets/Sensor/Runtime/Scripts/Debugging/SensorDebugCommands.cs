namespace SensorSystem
{
    /// <summary>Opens the overlay if it is closed and closes it if it is open.</summary>
    public sealed class ToggleOverlayCommand : ISensorDebugCommand
    {
        public void Execute(ISensorDebugReceiver receiver) => receiver.SetOpen(!receiver.IsOpen);
    }

    public sealed class OpenOverlayCommand : ISensorDebugCommand
    {
        public void Execute(ISensorDebugReceiver receiver) => receiver.SetOpen(true);
    }

    public sealed class CloseOverlayCommand : ISensorDebugCommand
    {
        public void Execute(ISensorDebugReceiver receiver) => receiver.SetOpen(false);
    }

    /// <summary>Opens the overlay on this sensor.</summary>
    public sealed class FocusSensorCommand : ISensorDebugCommand
    {
        public Sensor Sensor { get; }

        public FocusSensorCommand(Sensor sensor)
        {
            Sensor = sensor;
        }

        public void Execute(ISensorDebugReceiver receiver)
        {
            if (Sensor == null) return;

            receiver.SetOpen(true);
            receiver.Focus(Sensor);
        }
    }

    /// <summary>Opens the overlay on this detector.</summary>
    public sealed class FocusDetectorCommand : ISensorDebugCommand
    {
        public Detector Detector { get; }

        public FocusDetectorCommand(Detector detector)
        {
            Detector = detector;
        }

        public void Execute(ISensorDebugReceiver receiver)
        {
            if (Detector == null) return;

            receiver.SetOpen(true);
            receiver.Focus(Detector);
        }
    }

    /// <summary>Discards the values changed at runtime on a sensor.</summary>
    public sealed class ResetSensorValuesCommand : ISensorDebugCommand
    {
        public Sensor Sensor { get; }

        public ResetSensorValuesCommand(Sensor sensor)
        {
            Sensor = sensor;
        }

        public void Execute(ISensorDebugReceiver receiver)
        {
            if (Sensor != null)
            {
                receiver.ResetValues(Sensor);
            }
        }
    }
}
