using UnityEngine;

namespace SensorSystem
{
    /// <summary>
    /// Lets scene objects open and close the QA overlay without code: point a UI Button, a UnityEvent
    /// or an Input System action at Toggle, Open or Close. It only dispatches commands.
    /// </summary>
    [AddComponentMenu("Sensor/Sensor Debug Commands")]
    public class SensorDebugCommandBehaviour : MonoBehaviour
    {
        public void Toggle() => SensorDebug.Dispatch(new ToggleOverlayCommand());

        public void Open() => SensorDebug.Dispatch(new OpenOverlayCommand());

        public void Close() => SensorDebug.Dispatch(new CloseOverlayCommand());
    }
}
