namespace MotionCamera
{
    /// <summary>
    /// Interface for frame producers.
    /// </summary>
    public interface IFrameProducer
    {
        /// <summary>
        /// Fired whenever a new frame is acquired from the camera.
        /// </summary>
        event EventHandler<FrameProducedEventArgs> FrameProduced;

        /// <summary>
        /// Gets or sets the exposure time.
        /// </summary>
        double Exposure { get; set; }

        /// <summary>
        /// Gets the minimal exposure supported by the camera, in microseconds.
        /// </summary>
        double ExposureMin { get; }

        /// <summary>
        /// Gets the maximal exposure supported by the camera, in microseconds.
        /// </summary>
        double ExposureMax { get; }

        /// <summary>
        /// Flush the images from the queue.
        /// </summary>
        void Flush();

        /// <summary>
        /// Initialize the camera.
        /// </summary>
        /// <exception cref="HardwareFaultException">failed.</exception>
        void InitializeCamera();

        /// <summary>
        /// Start the continuous acquisition.
        /// </summary>
        /// <returns>Success.</returns>
        bool Start();

        /// <summary>
        /// Stops the acquisition.
        /// </summary>
        void Stop();

        /// <summary>
        /// Close the device.
        /// </summary>
        void Close();

        /// <summary>
        /// Gets the internal timestamp of the camera.
        /// </summary>
        long GetTimestamp();

    }
}
