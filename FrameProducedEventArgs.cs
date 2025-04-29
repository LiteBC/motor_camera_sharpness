namespace MotionCamera
{
    using System;

    public class FrameProducedEventArgs : EventArgs
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="FrameProducedEventArgs"/> class.
        /// </summary>
        /// <param name="timestamp">Timestamp of the frame.</param>
        /// <param name="height">Height of the frame.</param>
        /// <param name="width">Width of the frame.</param>
        /// <param name="pixelData">Pixel data as 2D byte array.</param>
        public FrameProducedEventArgs(
            long timestamp,
            int height,
            int width,
            byte[,] pixelData)
        {
            this.Timestamp = timestamp;
            this.Height = height;
            this.Width = width;
            this.PixelData = pixelData;
        }

        /// <summary>
        /// Gets the timestamp of the frame.
        /// </summary>
        public long Timestamp { get; private set; }

        /// <summary>
        /// Gets the height of the frame.
        /// </summary>
        public int Height { get; private set; }

        /// <summary>
        /// Gets the width of the frame.
        /// </summary>
        public int Width { get; private set; }

        /// <summary>
        /// Gets the pixel data of the frame as a 2D array.
        /// </summary>
        public byte[,] PixelData { get; private set; }
    }
}
