using BitMiracle.LibTiff.Classic;
using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace MotionCamera
{
    /// <summary>
    /// A fake implementation of the IFrameProducer interface for testing purposes.
    /// </summary>
    public class FakeCamera : IFrameProducer
    {
        private const int ImageWidth = 1368;
        private const int ImageHeight = 1024;
        private const double DefaultExposure = 100.0; // Default exposure in microseconds
        private double initialTimestamp = 0;

        private bool isRunning;
        private CancellationTokenSource cancellationTokenSource;
        private Dictionary<double, byte[,]> preloadedImages = new Dictionary<double, byte[,]>();

        /// <inheritdoc />
        public double ExposureMin => 10.0;

        /// <inheritdoc />
        public double ExposureMax => 1000.0;

        private double FrameRate => 200.0;

        /// <summary>
        /// Initializes a new instance of the <see cref="FakeCamera"/> class.
        /// </summary>
        public FakeCamera()
        {
            this.Exposure = DefaultExposure;
        }

        /// <inheritdoc />
        public event EventHandler<FrameProducedEventArgs> FrameProduced;

        /// <inheritdoc />
        public double Exposure { get; set; }


        /// <inheritdoc />
        public void Flush()
        {
            // No-op for the fake camera as there is no queue to flush.
        }

        /// <inheritdoc />
        public void InitializeCamera()
        {
            this.PreloadImages();
            this.initialTimestamp = (double)Stopwatch.GetTimestamp() / (double)Stopwatch.Frequency;
        }

        /// <inheritdoc />
        public bool Start()
        {
            if (this.isRunning)
            {
                return false;
            }

            this.isRunning = true;
            this.cancellationTokenSource = new CancellationTokenSource();

            // Start producing frames asynchronously.
            Task.Run(() => this.ProduceFrames(this.cancellationTokenSource.Token));

            return true;
        }

        /// <inheritdoc />
        public void Stop()
        {
            if (!this.isRunning)
            {
                return;
            }

            this.isRunning = false;
            this.cancellationTokenSource.Cancel();
        }

        /// <inheritdoc />
        public void Close()
        {
            this.Stop();
        }

        private void ProduceFrames(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                // Simulate frame acquisition delay based on exposure time.
                Thread.Sleep((int)(1000 / this.FrameRate));

                // Generate a blank frame.
                var pixelData = GetImageBasedOnPosition();

                var pos = GetMotorCurrentPosition();

                // Raise the FrameProduced event.
                this.FrameProduced?.Invoke(this, new FrameProducedEventArgs(
                    timestamp: this.GetTimestamp(),
                    height: ImageHeight,
                    width: ImageWidth,
                    pixelData: pixelData));
            }
        }

        private long GetTimestamp()
        {
            double currentTimestamp = (double)Stopwatch.GetTimestamp() / (double)Stopwatch.Frequency;
            return (long)((currentTimestamp - this.initialTimestamp) * 1100); // Convert to milliseconds  

        }

        /// <summary>
        /// Uses reflection to find an instance of a FakeMotorController in the current AppDomain.
        /// </summary>
        /// <returns>An instance of FakeMotorController if found; otherwise, null.</returns>
        private FakeMotorController FindFakeMotorControllerInstance()
        {
            var fieldInfo = typeof(FakeMotorController).GetField("instance", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            return fieldInfo?.GetValue(null) as FakeMotorController;
        }

        /// <summary>
        /// Uses reflection to find an instance of a FakeMotorController, then gets the currentPosition.
        /// </summary>
        /// <returns>The current position of the motor as a double if found; otherwise, null.</returns>
        private double? GetMotorCurrentPosition()
        {
            var motorController = FindFakeMotorControllerInstance();
            if (motorController != null)
            {
                var currentPositionField = motorController.GetType().GetField("currentPosition", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (currentPositionField != null)
                {
                    return (double?)currentPositionField.GetValue(motorController);
                }
            }
            return null;
        }

        /// <summary>
        /// Retrieves a preloaded image based on the current motor position.
        /// If the position is not available, returns a black frame.
        /// </summary>
        /// <returns>A 2D byte array representing the image pixel data.</returns>
        private byte[,] GetImageBasedOnPosition()
        {
            var position = GetMotorCurrentPosition();

            if (position == null)
            {
                // Return a black frame if position is not available.
                return new byte[ImageHeight, ImageWidth];
            }

            // Find the preloaded image with the closest position.
            var closestImage = preloadedImages
                .OrderBy(entry => Math.Abs(entry.Key - position.Value))
                .FirstOrDefault();

            if (closestImage.Value == null)
            {
                // Return a black frame if no valid preloaded image is found.
                return new byte[ImageHeight, ImageWidth];
            }

            return closestImage.Value;
        }

        /// <summary>
        /// Preloads all image files from the assets folder into a dictionary.
        /// The key is the position parsed from the file name, and the value is the image pixel data.
        /// </summary>
        private void PreloadImages()
        {
            string assetsFolder = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "..", "assets"));
            Console.WriteLine($"Assets folder: {assetsFolder}");

            if (!Directory.Exists(assetsFolder))
            {
                return; // Exit if the assets folder does not exist.
            }

            var imageFiles = Directory.GetFiles(assetsFolder, "*.tif");
            foreach (var file in imageFiles)
            {
                if (double.TryParse(Path.GetFileNameWithoutExtension(file), out var position))
                {
                    using (Tiff image = Tiff.Open(file, "r"))
                    {
                        int width = image.GetField(TiffTag.IMAGEWIDTH)[0].ToInt();
                        int height = image.GetField(TiffTag.IMAGELENGTH)[0].ToInt();
                        var pixelData = new byte[height, width];
                        byte[] buffer = new byte[width];

                        for (int row = 0; row < height; row++)
                        {
                            image.ReadScanline(buffer, row);
                            Buffer.BlockCopy(buffer, 0, pixelData, row * width, width);
                        }

                        preloadedImages[position] = pixelData;
                    }
                }
            }
        }

    }
}
