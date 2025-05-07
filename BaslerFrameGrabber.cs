// ----------------------------------------------------------------------------------
// Basler Camera Implementation
// (c) LiteBC, 2025
// ----------------------------------------------------------------------------------
namespace BaslerCamera
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Linq;
    using System.Threading;
    using Basler.Pylon;
    using MotionCamera;
    using NLog;

    /// <summary>
    /// Class for data acquisition from the Basler cameras.
    /// </summary>
    public class BaslerFrameGrabber : IFrameProducer
    {
        public Logger LogicLogger = NLog.LogManager.GetCurrentClassLogger();

        private const double DefaultExposure = 200;
        private const double DefaultGain = 1;
        private readonly Stopwatch lastFrameStopwatch = new Stopwatch();
        private readonly BlockingCollection<FrameProducedEventArgs> processQueue = new BlockingCollection<FrameProducedEventArgs>();
        private readonly Random random = new Random();
        private readonly Thread queueThread;
        private readonly CancellationTokenSource cancellationSourceThread;

        private bool grabbing;

        /// <summary>
        /// Initializes a new instance of the <see cref="BaslerFrameGrabber"/> class.
        /// </summary>
        public BaslerFrameGrabber()
        {

            this.cancellationSourceThread = new CancellationTokenSource();
            this.queueThread = new Thread(() => this.DispatchingThread(this.cancellationSourceThread.Token))
            {
                IsBackground = true,
                Name = "BaslerDispatcher",
                Priority = ThreadPriority.Highest,
            };
            this.queueThread.Start();
        }

        /// <summary>
        /// Fired whenever the camera starts or stops grabbing.
        /// </summary>
        public event EventHandler GrabbingStatusChanged;

        /// <summary>
        /// The event that is raised when the camera is opened.
        /// </summary>
        public event EventHandler CameraOpened;

        /// <summary>
        /// Fired whenever the frame is received from the camera.
        /// </summary>
        public event EventHandler<FrameProducedEventArgs> FrameProduced;

        /// <summary>
        /// Gets the camera object.
        /// </summary>
        public Camera Camera { get; private set; } = null;

        /// <summary>
        /// Gets the minimal exposure supported by the caqmera, in microseconds.
        /// </summary>
        public double ExposureMin => (this.Camera != null) ? this.Camera.Parameters[PLCamera.ExposureTime].GetMinimum() : 50;

        /// <summary>
        /// Gets the maximal exposure supported by the caqmera, in microseconds.
        /// </summary>
        public double ExposureMax => (this.Camera != null) ? this.Camera.Parameters[PLCamera.ExposureTime].GetMaximum() : 2000;

        /// <summary>
        /// Gets or sets current exposure in microseconds.
        /// </summary>
        public double Exposure
        {
            get
            {
                double exposure = 0;
                try
                {
                    if (this.Camera != null &&
                        this.Camera.IsOpen &&
                        this.Camera.Parameters.Contains(PLCamera.ExposureTime))
                    {
                        exposure = this.Camera.Parameters[PLCamera.ExposureTime].GetValue();
                    }
                }
                catch (Exception exception)
                {
                    this.LogicLogger.Error(exception, "Exception caught:");
                }

                return exposure;
            }

            set
            {
                this.LogicLogger.Debug($"[AutoExposureSection.Exposure] Inside 'Exposure' setter'");
                try
                {
                    if (this.Camera != null &&
                        this.Camera.IsOpen &&
                        this.Camera.Parameters.Contains(PLCamera.ExposureTime))
                    {
                        double acceptedValue = Math.Min(Math.Max(this.ExposureMin, value), this.ExposureMax);
                        this.Camera.Parameters[PLCamera.ExposureTime].SetValue(acceptedValue);
                        this.LogicLogger.Debug($"[AutoExposureSection.Exposure] Inside 'Exposure' setter', input value: {value}, acceptedValue: {acceptedValue}");
                    }
                }
                catch (Exception exception)
                {
                    this.LogicLogger.Error($"Exception caught: Inside 'Exposure' setter', exception {exception}");
                }
            }
        }

        /// <summary>
        /// Closes the camera object and handles exceptions.
        /// </summary>
        public void Close()
        {
            this.cancellationSourceThread.Cancel();
            if (this.Camera != null)
            {
                this.Camera.Close();
                this.Camera = null;
            }
        }

        /// <summary>
        /// Start the continuous acquisition.
        /// </summary>
        /// <returns>Success.</returns>
        public bool Start()
        {
            if (this.Camera == null || !this.Camera.IsOpen)
            {
                this.Open();
            }

            if (this.Camera == null || !this.Camera.IsOpen)
            {
                return false;
            }

            // Register for the events of the image provider needed for proper operation.
            this.Camera.StreamGrabber.GrabStarted -= this.OnGrabStarted;
            this.Camera.StreamGrabber.ImageGrabbed -= this.OnImageGrabbedInternal;
            this.Camera.StreamGrabber.GrabStopped -= this.OnGrabStopped;

            this.Camera.StreamGrabber.GrabStarted += this.OnGrabStarted;
            this.Camera.StreamGrabber.ImageGrabbed += this.OnImageGrabbedInternal;
            this.Camera.StreamGrabber.GrabStopped += this.OnGrabStopped;

            var fullWidth = this.Camera.Parameters[PLCamera.Width].GetValue();
            var fullHeight = this.Camera.Parameters[PLCamera.Height].GetValue();

            this.lastFrameStopwatch.Start();

            this.ContinuousShot();
            this.grabbing = true;
            return true;
        }

        /// <summary>
        /// Stops the acquisition.
        /// </summary>
        public void Stop()
        {
            // Ensure camera is not null before checking grabbing and calling Stop
            if (this.Camera != null && this.grabbing)
            {
                this.Camera.StreamGrabber.Stop();
            }

            if (this.Camera != null)
            {
                // Register for the events of the image provider needed for proper operation.
                this.Camera.StreamGrabber.GrabStarted -= this.OnGrabStarted;
                this.Camera.StreamGrabber.ImageGrabbed -= this.OnImageGrabbedInternal;
                this.Camera.StreamGrabber.GrabStopped -= this.OnGrabStopped;
            }
        }

        
        /// <summary>
        /// Maximize the grabbed image area of interest (Image AOI).
        /// </summary>
        public void ClearRegionOfInterest()
        {
            if (this.Camera == null || !this.Camera.IsOpen || this.grabbing)
            {
                return;
            }

            this.Camera.Parameters[PLCamera.OffsetX].TrySetToMinimum();
            this.Camera.Parameters[PLCamera.OffsetY].TrySetToMinimum();

            string model = this.Camera.Parameters[PLCamera.DeviceModelName].GetValue();
            if (model.Contains("CamEmu"))
            {
                this.Camera.Parameters[PLCamera.Width].TrySetValue(1920, IntegerValueCorrection.Nearest);
                this.Camera.Parameters[PLCamera.Height].TrySetValue(1200, IntegerValueCorrection.Nearest);
            }
            else
            {
                this.Camera.Parameters[PLCamera.Width].SetToMaximum();
                this.Camera.Parameters[PLCamera.Height].SetToMaximum();
            }
        }

        /// <summary>
        /// Flush the images from the queue(s).
        /// </summary>
        public void Flush()
        {
            while (this.processQueue.Count > 0)
            {
                this.processQueue.Take();
            }
        }

        /// <summary>
        /// Reads hardware timestamp from the camera.
        /// </summary>
        /// <returns>Timestamp, in nanoseconds.</returns>
        public long GetTimestamp()
        {
            try
            {
                // Take a "snapshot" of the camera's current timestamp value
                this.Camera.Parameters[PLCamera.TimestampLatch].Execute();
                return this.Camera.Parameters[PLCamera.TimestampLatchValue].GetValue();
            }
            catch (Exception ex)
            {
                this.LogicLogger.Error(ex, "Error reading hardware timestamp from Basler camera");
                throw ex;
            }
        }

        /// <summary>
        /// Initialize the camera.
        /// </summary>
        /// <exception cref="HardwareFaultException">failed.</exception>
        public void InitializeCamera()
        {
        }

        /// <summary>
        /// Get the first camera found.
        /// </summary>
        /// <param name="selectedCamera">set the camera within the ref value.</param>
        /// <returns>success or not.</returns>
        private static bool GetFirstCamera(out ICameraInfo selectedCamera)
        {
            // Ask the camera finder for a list of camera devices.
            List<ICameraInfo> allCameras = CameraFinder.Enumerate();
            selectedCamera = allCameras.FirstOrDefault(info =>
            {
                string deviceType = info.GetValueOrDefault(CameraInfoKey.DeviceType, string.Empty);
                if (deviceType.Equals(DeviceType.CameraEmulator))
                {
                    return true;
                }

                if (deviceType.Equals(DeviceType.Usb))
                {
                    return true;
                }

                return false;
            });

            if (selectedCamera == null)
            {
                return false;
            }

            return true;
        }

        private void DispatchingThread(CancellationToken token)
        {
            this.LogicLogger.Info("Basler dispatcher thread started.");

            while (!token.IsCancellationRequested)
            {
                try
                {
                    var frameProduced = this.processQueue.Take(token);
                    this.FrameProduced?.Invoke(this, frameProduced);

                }
                catch (OperationCanceledException)
                {
                    this.LogicLogger.Info("Basler dispatcher thread cancelled.");
                    return;
                }
            }

            this.LogicLogger.Info("Basler dispatcher thread exited.");
        }

        private void Open()
        {
            if (this.grabbing)
            {
                this.Stop();
            }

            // Create a new camera object.
            try
            {
                if (GetFirstCamera(out ICameraInfo cameraInfo))
                {
                    this.Camera = new Camera(cameraInfo);
                    this.Camera.CameraOpened += Configuration.AcquireContinuous;

                    // Register for the events of the image provider needed for proper operation.
                    this.Camera.CameraOpened += this.OnCameraOpened;
                    this.Camera.ConnectionLost += this.OnConnectionLost;

                    // Open the connection to the camera device.
                    _ = this.Camera.Open();
                }
            }
            catch (Exception exception)
            {
                this.LogicLogger.Error(exception, "Exception caught:");
            }
        }

        /// <summary>
        /// Occurs when the connection to a camera device is opened.
        /// </summary>
        private void OnCameraOpened(object sender, EventArgs e)
        {
            // Logging connected device information.
            ICamera camera = (ICamera)sender;
            string deviceVendorName = camera.Parameters[PLCamera.DeviceVendorName].GetValue();
            string deviceModelName = camera.Parameters[PLCamera.DeviceModelName].GetValue();
            string dviceFirmwareVersion = camera.Parameters[PLCamera.DeviceFirmwareVersion].GetValue();
            this.LogicLogger.Info($"Camera {deviceVendorName} opened, model {deviceModelName}, firmware version {dviceFirmwareVersion}.");

            // Set the parameter for the controls.
            camera.Parameters[PLCamera.PixelFormat].TrySetValue(PLCamera.PixelFormat.Mono8);
            camera.Parameters[PLCameraInstance.MaxNumBuffer].SetValue(20);

            camera.Parameters[PLCamera.LineSelector].SetValue(PLCamera.LineSelector.Line2);
            camera.Parameters[PLCamera.LineSource].SetValue(PLCamera.LineSource.ExposureActive);
            camera.Parameters[PLCamera.LineMode].SetValue(PLCamera.LineMode.Output);

            camera.Parameters[PLCamera.ReverseX].SetValue(true); // For Alpha2 HemoScope from HW release VB03 / oct 2024
            camera.Parameters[PLCamera.ReverseY].SetValue(false); // For Alpha2 HemoScope from HW release VB03 / oct 2024

            camera.Parameters[PLCamera.AcquisitionFrameRateEnable].SetValue(false);

            //// camera.Parameters[PLCamera.LineSelector].SetValue(PLCamera.LineSelector.Line3);
            //// camera.Parameters[PLCamera.LineSource].SetValue(PLCamera.LineSource.FlashWindow);
            //// camera.Parameters[PLCamera.LineMode].SetValue(PLCamera.LineMode.Output);v

            if (camera.Parameters.Contains(PLCamera.ExposureTime))
            {
                if (camera.Parameters[PLCamera.ExposureTime].TrySetValue(DefaultExposure))
                {
                    this.LogicLogger.Info($"Set ExposureTime value to {DefaultExposure}");
                }
                else
                {
                    this.LogicLogger.Info($"The ExposureTime cannot be set to {DefaultExposure}");
                }
            }
            else
            {
                this.LogicLogger.Info($"The ExposureTime does not supported.");
            }

            if (camera.Parameters.Contains(PLCamera.Gain))
            {
                if (camera.Parameters[PLCamera.Gain].TrySetValue(DefaultGain))
                {
                    this.LogicLogger.Info($"Set Gain value to {DefaultGain}");
                }
                else
                {
                    this.LogicLogger.Info($"The Gain cannot be set to {DefaultGain}");
                }
            }
            else
            {
                this.LogicLogger.Info($"The Gain does not supported.");
            }

            this.ClearRegionOfInterest();
            this.CameraOpened?.Invoke(this, EventArgs.Empty);
        }

        private void OnConnectionLost(object sender, EventArgs e)
        {
            this.Close();
        }

        /// <summary>
        /// Occurs when a camera starts grabbing.
        /// </summary>
        private void OnGrabStarted(object sender, EventArgs e)
        {
            this.grabbing = true;
            this.GrabbingStatusChanged?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// Occurs when an image has been acquired and is ready to be processed.
        /// </summary>
        private void OnImageGrabbedInternal(object sender, ImageGrabbedEventArgs e)
        {
            try
            {
                this.lastFrameStopwatch.Restart();
                IGrabResult grabResult = e.GrabResult;
                if (grabResult.GrabSucceeded)
                {
                    var timestamp = (double)Stopwatch.GetTimestamp() / (double)Stopwatch.Frequency;
                    var clone = (grabResult.PixelData as byte[]).Clone() as byte[];

                    var height = grabResult.Height;
                    var width = grabResult.Width;
                    byte[,] reshapedClone = new byte[height, width];

                    // Copy the buffer directly into the 2D array
                    Buffer.BlockCopy(clone, 0, reshapedClone, 0, random.Next(100) == 0 ? 0 : clone.Length);

                    this.processQueue.Add(new FrameProducedEventArgs((long)(timestamp*1000), height, width, reshapedClone));
                    Console.WriteLine($"Queue length: {this.processQueue.Count}");
                }
            }
            catch (Exception exception)
            {
                this.LogicLogger.Error(exception, "Exception caught:");
            }
            finally
            {
                // Dispose the grab result if needed for returning it to the grab loop.
                e.DisposeGrabResultIfClone();
            }
        }

        /// <summary>
        /// Occurs when a camera has stopped grabbing.
        /// </summary>
        private void OnGrabStopped(object sender, GrabStopEventArgs e)
        {
            // If the grabbed stop due to an error, display the error message.
            if (e.Reason != GrabStopReason.UserRequest)
            {
                this.LogicLogger.Error($"A grab error occured:\n{e.ErrorMessage} Error");
            }
            else
            {
            }

            this.grabbing = false;
            this.GrabbingStatusChanged?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// Starts the continuous grabbing of images and handles exceptions.
        /// </summary>
        private void ContinuousShot()
        {
            try
            {
                // Start the grabbing of images until grabbing is stopped.
                Configuration.AcquireContinuous(this.Camera, null);

                this.Camera.StreamGrabber.Start(GrabStrategy.OneByOne, GrabLoop.ProvidedByStreamGrabber);
            }
            catch (Exception exception)
            {
                this.LogicLogger.Error(exception, "Exception caught:");
            }
        }
    }
}
