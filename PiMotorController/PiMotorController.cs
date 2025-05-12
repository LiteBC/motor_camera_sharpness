namespace PiMotorController
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Linq;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using System.Threading.Tasks.Dataflow;
    using System.Xml.Linq;
    using MotionCamera;
    using NLog;

    /// <summary>
    /// Provides extension methods for various utility operations.
    /// </summary>
    public static class ExtensionUtilities
    {
        /// <summary>
        /// Clamps a value to the specified minimum and maximum values.
        /// </summary>
        /// <typeparam name="T">The type of the value, which must implement <see cref="IComparable{T}"/>.</typeparam>
        /// <param name="value">The value to clamp.</param>
        /// <param name="min">The minimum value.</param>
        /// <param name="max">The maximum value.</param>
        /// <returns>The clamped value.</returns>
        /// <exception cref="ArgumentNullException">Thrown if any of the parameters are null.</exception>
        public static T Clamped<T>(this T value, T min, T max)
            where T : IComparable<T>
        {
            if (value == null)
            {
                throw new ArgumentNullException(nameof(value), "is null.");
            }
            else if (min == null)
            {
                throw new ArgumentNullException(nameof(min), "is null.");
            }
            else if (max == null)
            {
                throw new ArgumentNullException(nameof(max), "is null.");
            }

            T result = value;
            if (value.CompareTo(max) > 0)
            {
                return max;
            }
            else if (value.CompareTo(min) < 0)
            {
                return min;
            }

            return result;
        }
    }

    public class GCSCommandError : Exception
    {
        public GCSCommandError(string message)
            : base(message)
        {
        }
    }

    public enum ConnectionType
    {
        Dialog,
        Rs232,
        Tcpip,
        Usb
    }

    /// <summary>
    /// C-884.4DC Motion Controller for DC Motors, 4 Axes.
    /// </summary>
    public class PiMotorController : IMotorController
    {
        public Logger LogicLogger = NLog.LogManager.GetCurrentClassLogger();

        private const int PI_TRUE = 1;
        private const int PI_RESULT_FAILURE = 0;
        private const int PI_NUMBER_OF_AXIS = 1;
        private readonly AutoResetEvent eventAutoReset;

        private string comPort = "USB";
        private int controllerID = -1;
        private Thread thread;

        private CancellationTokenSource cancellationSource;
        private bool taskRunning;

        private BufferBlock<Action> commandQueue = new BufferBlock<Action>();

        private bool isTrackingMovement = false;
        private object isTrackingMovementLock = new object();

        /// <summary>
        /// Initializes a new instance of the <see cref="PiMotorController"/> class.
        /// </summary>
        public PiMotorController(string comPort)
        {
            this.comPort = comPort;

            this.eventAutoReset = new AutoResetEvent(true);

            this.cancellationSource = null;
        }

        /// <summary>
        /// Event fired when the the serial port in connected.
        /// </summary>
        public event EventHandler<EventArgs> Connected;

        /// <summary>
        /// Event fired when the the serial port in connected.
        /// </summary>
        public event EventHandler<EventArgs> Disconnected;

        public static bool IsAvailable()
        {
            StringBuilder connectedUsbController = new StringBuilder(1024);
            int noDevices = GCS2.EnumerateUSB(connectedUsbController, 1024, string.Empty);
            if (noDevices > 0)
            {
                return true;
            }

            var port = SerialPortUtilities.GetSerialPortInfo()
                .FirstOrDefault(p => p.Caption.ToLower().Contains("prolific"));
            if (port != null)
            {
                // LogicLogger.Info($"Found: {port.Caption}, connecting to the: {port.PortName}");
                int.TryParse(string.Join(string.Empty, port.PortName.Where(c => char.IsDigit(c))), out int serialPortNumber);
                var controllerID = GCS2.ConnectRS232(serialPortNumber, 38400);
                var connectedController = new StringBuilder(1024);
                noDevices = GCS2.EnumerateUSB(connectedController, 1024, string.Empty);
                if (noDevices > 0)
                {
                    return true;
                }
            }

            return false;
        }

        public bool Connect()
        {
            if (this.taskRunning)
            {
                LogicLogger.Warn("Motion task already running.");
                return false;
            }

            try
            {
                if (!this.OpenConnection())
                {
                    return false;
                }

                this.PrintControllerIdentification();

                string[] сonnectedAxes = this.GetNamesOfConnectedAxes();
                string axis = string.Join(" ", сonnectedAxes);

                var velocity = 1.0;
                this.SetVelocity(velocity);

                var state = Enumerable.Repeat(PI_TRUE, PI_NUMBER_OF_AXIS).ToArray();
                this.SetServoState(axis, state);
            }
            catch (GCSCommandError exception)
            {
                this.PrintErrorMessage(exception);
                return false;
            }
            catch (Exception exception)
            {
                LogicLogger.Error(exception, "Exception caught:");
                return false;
            }

            this.cancellationSource?.Dispose();

            this.cancellationSource = new CancellationTokenSource();
            this.thread = new Thread(() => this.ControlThread(this.cancellationSource.Token));
            this.thread.Name = "PiMotionController_ControlThread";
            this.thread.Start();

            this.Connected?.Invoke(this, EventArgs.Empty);
            return true;
        }

        public void Disconnect()
        {
            if (this.cancellationSource != null)
            {
                this.cancellationSource.Cancel();
            }

            this.taskRunning = false;
            this.eventAutoReset.Set();
            if (this.thread != null && this.thread.IsAlive)
            {
                this.thread.Join();
                this.thread = null;
            }

            this.DisconnectController();
            this.Disconnected?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// Move absolute in one axis.
        /// </summary>
        /// <param name="position">The target position in mm.</param>
        /// <param name="speed">The movement speed in mm/s.</param>
        /// <param name="waitForCompletion">Flag indicating whether to wait for the movement to complete.</param>
        /// <returns>True if the move is successful, otherwise false.</returns>
        /// <returns>if it success or not.</returns>
        /// <exception cref="InvalidOperationException">Tried to move while active operation in progress.</exception>
        public bool MoveAbsolute(double position, double speed, bool waitForCompletion)
        {
            lock (this.isTrackingMovementLock)
            {
                if (this.isTrackingMovement)
                {
                    throw new InvalidOperationException("Tried to move while active operation in progress");
                }
            }
            this.SetVelocity(speed);

            double targetPos = position.Clamped(this.MinPosition, this.MaxPosition);
            this.QueueMoveAbsolute(new[] { targetPos });

            if(waitForCompletion)
            {
                this.isTrackingMovement = true;
                while (true)
                {
                    double[] actualPosition = this.QueryActualPosition();
                    if (Math.Abs(actualPosition[0] - targetPos) < 0.01)
                    {
                        break;
                    }
                    Thread.Sleep(10);
                }
                lock (this.isTrackingMovementLock)
                {
                    this.isTrackingMovement = false;
                }
            }
            return true;
        }

        private void QueueMoveAbsolute(double[] position)
        {
            this.commandQueue.Post(() => this.MoveToAbsoluteTarget("1", position));
        }

        private void MoveToAbsoluteTarget(string axis, double[] targetPos)
        {
            int result = GCS2.MOV(this.controllerID, axis, targetPos);
            if (result == PI_RESULT_FAILURE)
            {
                throw new GCSCommandError("Unable to move to target position.");
            }
        }

        private void MoveToAbsoluteTarget(double[] targetPos)
        {
            if (GCS2.MOV(this.controllerID, "2 3 1", targetPos) == PI_RESULT_FAILURE)
            {
                throw new GCSCommandError("Unable to move to target position.");
            }
        }

        private void SetServoState(string axis, int[] state)
        {
            if (GCS2.SVO(this.controllerID, axis, state) == PI_RESULT_FAILURE)
            {
                throw new GCSCommandError("SVO failed. Exiting");
            }
        }

        public double[] GetVelocity(string axis)
        {
            double[] velocity = Enumerable.Repeat(1.0, axis.Split(' ').Length).ToArray();
            if (GCS2.qVEL(this.controllerID, axis, velocity) == PI_RESULT_FAILURE)
            {
                int error = GCS2.GetError(this.controllerID);
                var sb = new StringBuilder(1024);
                GCS2.TranslateError(error, sb, 1024);

                throw new GCSCommandError($"qVEL() failed: {sb.ToString()}");
            }

            return velocity;
        }

        public void SetVelocity(double velocity)
        {
            if (GCS2.VEL(this.controllerID, "1", new double[] { velocity }) == PI_RESULT_FAILURE)
            {
                int error = GCS2.GetError(this.controllerID);
                var sb = new StringBuilder(1024);
                GCS2.TranslateError(error, sb, 1024);

                throw new GCSCommandError($"VEL() failed: {sb.ToString()}");
            }
        }

        private bool OpenConnection()
        {
            if (this.comPort == "USB")
            {
                StringBuilder connectedUsbController = new StringBuilder(1024);
                int noDevices = GCS2.EnumerateUSB(connectedUsbController, 1024, string.Empty);
                if (noDevices > 0)
                {
                    LogicLogger.Info($"Found: {noDevices} USB controllers, connecting to the first one: {connectedUsbController}");
                    this.controllerID = GCS2.ConnectUSB(
                                        connectedUsbController
                                            .ToString()
                                            .Split(new[] { Environment.NewLine }, StringSplitOptions.None)
                                            [0]);
                }
            }
            else
            {
                if (this.controllerID < 0)
                {
                    LogicLogger.Info($"Connecting to the: {comPort}");
                    int.TryParse(comPort.Substring(3), out int serialPortNumber);
                    this.controllerID = GCS2.ConnectRS232(serialPortNumber, 115200);
                }
            }

            if (this.controllerID < 0)
            {
                LogicLogger.Error("Connection to PI using " + comPort + " failed.");
                return false;
            }

            return true;
        }

        private void PrintControllerIdentification()
        {
            StringBuilder controllerIdentification = new StringBuilder(1024);

            if (GCS2.qIDN(this.controllerID, controllerIdentification, controllerIdentification.Capacity) == PI_RESULT_FAILURE)
            {
                throw new GCSCommandError("qIDN() failed");
            }

            Console.WriteLine("Contoller identification: " + controllerIdentification);
        }

        private string[] GetNamesOfConnectedAxes()
        {
            StringBuilder axesBuffer = new StringBuilder(1024);

            if (GCS2.qSAI(this.controllerID, axesBuffer, axesBuffer.Capacity) == PI_RESULT_FAILURE)
            {
                throw new GCSCommandError("qSAI() failed");
            }

            string[] axisName = axesBuffer.ToString().Replace(" \n", ";").Replace("\n", string.Empty).Split(';');
            for (int index = 0; index < axisName.Count(); ++index)
            {
                axisName[index] = axisName[index].Trim();
                LogicLogger.Info("Name of axis number " + index + ": " + axisName[index]);
            }

            return axisName;
        }

        private void DisconnectController()
        {
            if (this.controllerID >= 0)
            {
                _ = GCS2.CloseConnection(this.controllerID);
                this.controllerID = -1;
            }

            LogicLogger.Info("Close connection.");
        }

        public double MaxPosition
        {
            get
            {
                double[] maxPosLimit = new double[PI_NUMBER_OF_AXIS];

                if (GCS2.qTMX(this.controllerID, "1", maxPosLimit) == PI_RESULT_FAILURE)
                {
                    throw new GCSCommandError("Unable to get maximum position limit.");
                }

                return maxPosLimit[0];
            }
        }

        public double MinPosition
        {
            get
            {
                double[] minPosLimit = new double[PI_NUMBER_OF_AXIS];

                if (GCS2.qTMN(this.controllerID, "1", minPosLimit) == PI_RESULT_FAILURE)
                {
                    throw new GCSCommandError("Unable to get minimum position limit.");
                }
                return minPosLimit[0];
            }
        }

        public double[] QueryActualPosition()
        {
            double[] actualPosition = new double[PI_NUMBER_OF_AXIS];

            if (GCS2.qPOS(this.controllerID, string.Empty, actualPosition) == PI_RESULT_FAILURE)
            {
                throw new GCSCommandError("Unable to query to actual position.");
            }

            return actualPosition;
        }

        private void PrintErrorMessage(GCSCommandError e)
        {
            if (this.controllerID > 0)
            {
                int errorCode = GCS2.GetError(this.controllerID);
                StringBuilder errorMessage = new StringBuilder(1024);
                GCS2.TranslateError(errorCode, errorMessage, errorMessage.Capacity);
                LogicLogger.Error("ERROR: " + e.Message + " - Reported error code: " + errorCode.ToString() + ": " + errorMessage.ToString());
            }
            else
            {
                LogicLogger.Error("ERROR: " + e.Message);
            }
        }

        private void ControlThread(CancellationToken cancellationToken)
        {
            LogicLogger.Info("Motion thread started.");

            var taskDelay = Task.Delay(10, cancellationToken);
            var taskQueue = this.commandQueue.ReceiveAsync();
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    Task.WaitAny(new[] { taskDelay, taskQueue }, cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }

                if (taskQueue.IsCompleted)
                {
                    var queuedCommand = taskQueue.Result;
                    queuedCommand();
                    taskQueue = this.commandQueue.ReceiveAsync();
                }
                else
                {
                    double[] currentPos = this.QueryActualPosition();

                    taskDelay = Task.Delay(this.isTrackingMovement ? 10 : 250, cancellationToken);
                }
            }

            LogicLogger.Info("Motor thread exited.");
        }

        public PositionProducedEventArgs GetPosition()
        {
            double[] position = new double[] { 0.0 };
            if (GCS2.qPOS(this.controllerID, "1", position) == PI_RESULT_FAILURE)
            {
                throw new GCSCommandError("qPOS() failed");
            }
            return new PositionProducedEventArgs(Utils.Now(), position[0]);
        }

    }
}