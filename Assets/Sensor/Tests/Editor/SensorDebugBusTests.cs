using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace SensorSystem.Tests
{
    public class SensorDebugBusTests
    {
        private class Receiver : ISensorDebugReceiver
        {
            public bool IsOpen { get; private set; }
            public readonly List<string> Calls = new List<string>();

            public void SetOpen(bool open)
            {
                IsOpen = open;
                Calls.Add(open ? "open" : "close");
            }

            public void Focus(Sensor sensor) => Calls.Add("focus sensor");
            public void Focus(Detector detector) => Calls.Add("focus detector");
            public void ResetValues(Sensor sensor) => Calls.Add("reset");
        }

        /// <summary>A command defined outside the overlay's assembly, to show that new commands need no other change.</summary>
        private class CountCommand : ISensorDebugCommand
        {
            public int Runs;

            public void Execute(ISensorDebugReceiver receiver)
            {
                Runs++;
            }
        }

        private readonly List<ISensorDebugReceiver> _registered = new List<ISensorDebugReceiver>();

        private Receiver NewReceiver()
        {
            var receiver = new Receiver();
            SensorDebug.Register(receiver);
            _registered.Add(receiver);
            return receiver;
        }

        [TearDown]
        public void TearDown()
        {
            foreach (ISensorDebugReceiver receiver in _registered)
            {
                SensorDebug.Unregister(receiver);
            }
            _registered.Clear();
        }

        [Test]
        public void Dispatch_WithNoReceiver_ReturnsFalseAndDoesNothing()
        {
            Assert.IsFalse(SensorDebug.HasReceiver);
            Assert.IsFalse(SensorDebug.Dispatch(new ToggleOverlayCommand()));
        }

        [Test]
        public void Dispatch_Null_IsIgnored()
        {
            NewReceiver();
            Assert.IsFalse(SensorDebug.Dispatch(null));
        }

        [Test]
        public void Toggle_FlipsTheOverlay()
        {
            Receiver receiver = NewReceiver();

            Assert.IsTrue(SensorDebug.Dispatch(new ToggleOverlayCommand()));
            Assert.IsTrue(receiver.IsOpen);

            SensorDebug.Dispatch(new ToggleOverlayCommand());
            Assert.IsFalse(receiver.IsOpen);
        }

        [Test]
        public void OpenAndClose_SetTheStateWhateverItWas()
        {
            Receiver receiver = NewReceiver();

            SensorDebug.Dispatch(new OpenOverlayCommand());
            SensorDebug.Dispatch(new OpenOverlayCommand());
            Assert.IsTrue(receiver.IsOpen);

            SensorDebug.Dispatch(new CloseOverlayCommand());
            SensorDebug.Dispatch(new CloseOverlayCommand());
            Assert.IsFalse(receiver.IsOpen);
        }

        [Test]
        public void FocusCommands_OpenTheOverlayThenFocus()
        {
            Receiver receiver = NewReceiver();
            var owner = new GameObject("sensor and detector");
            try
            {
                Sensor sensor = owner.AddComponent<Sensor>();
                Detector detector = owner.AddComponent<Detector>();

                SensorDebug.Dispatch(new FocusSensorCommand(sensor));
                SensorDebug.Dispatch(new FocusDetectorCommand(detector));

                CollectionAssert.AreEqual(new[] { "open", "focus sensor", "open", "focus detector" }, receiver.Calls);
            }
            finally
            {
                Object.DestroyImmediate(owner);
            }
        }

        [Test]
        public void FocusAndResetWithNothing_DoNotReachTheReceiver()
        {
            Receiver receiver = NewReceiver();

            SensorDebug.Dispatch(new FocusSensorCommand(null));
            SensorDebug.Dispatch(new FocusDetectorCommand(null));
            SensorDebug.Dispatch(new ResetSensorValuesCommand(null));

            Assert.IsEmpty(receiver.Calls);
        }

        [Test]
        public void ResetCommand_ReachesTheReceiver()
        {
            Receiver receiver = NewReceiver();
            var owner = new GameObject("sensor");
            try
            {
                SensorDebug.Dispatch(new ResetSensorValuesCommand(owner.AddComponent<Sensor>()));
                CollectionAssert.AreEqual(new[] { "reset" }, receiver.Calls);
            }
            finally
            {
                Object.DestroyImmediate(owner);
            }
        }

        [Test]
        public void EveryReceiverGetsTheCommand_AndUnregisteredOnesDoNot()
        {
            Receiver first = NewReceiver();
            Receiver second = NewReceiver();

            SensorDebug.Dispatch(new OpenOverlayCommand());
            Assert.IsTrue(first.IsOpen);
            Assert.IsTrue(second.IsOpen);

            SensorDebug.Unregister(second);
            SensorDebug.Dispatch(new CloseOverlayCommand());
            Assert.IsFalse(first.IsOpen);
            Assert.IsTrue(second.IsOpen, "An unregistered receiver must not hear commands.");
        }

        [Test]
        public void RegisteringTwice_DoesNotRunACommandTwice()
        {
            Receiver receiver = NewReceiver();
            SensorDebug.Register(receiver);

            SensorDebug.Dispatch(new OpenOverlayCommand());

            Assert.AreEqual(1, receiver.Calls.Count);
        }

        [Test]
        public void CustomCommands_WorkWithoutAnyOtherChange()
        {
            NewReceiver();
            var command = new CountCommand();

            Assert.IsTrue(SensorDebug.Dispatch(command));
            Assert.AreEqual(1, command.Runs);
        }

        [Test]
        public void ACommandMayUnregisterItsReceiverWhileRunning()
        {
            Receiver receiver = NewReceiver();
            NewReceiver();
            var command = new UnregisteringCommand(receiver);

            Assert.DoesNotThrow(() => SensorDebug.Dispatch(command));
        }

        private class UnregisteringCommand : ISensorDebugCommand
        {
            private readonly ISensorDebugReceiver _toRemove;

            public UnregisteringCommand(ISensorDebugReceiver toRemove)
            {
                _toRemove = toRemove;
            }

            public void Execute(ISensorDebugReceiver receiver)
            {
                SensorDebug.Unregister(_toRemove);
            }
        }
    }
}
