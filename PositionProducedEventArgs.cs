namespace MotionCamera
{
    /// <summary>
    /// Storage class for motor position and timestamp.
    /// </summary>
    public class PositionProducedEventArgs : EventArgs
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="PositionProducedEventArgs"/> class.
        /// A default constructor that represents an invalid point.
        /// </summary>
        public PositionProducedEventArgs()
        {
            this.Timestamp = -1.0;
            this.Position = -1.0;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="PositionProducedEventArgs"/> class.
        /// </summary>
        /// <param name="timestamp">Timestamp in milliseconds.</param>
        /// <param name="position">Position in millimeters.</param>
        public PositionProducedEventArgs(
            double timestamp,
            double position)
        {
            this.Timestamp = timestamp;
            this.Position = position;
        }

        /// <summary>
        /// Gets the timestamp in milliseconds.
        /// </summary>
        public double Timestamp { get; private set; }

        /// <summary>
        /// Gets the position in millimeters.
        /// </summary>
        public double Position { get; private set; }
    }
}
