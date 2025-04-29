using System;
using System.Diagnostics;

namespace MotionCamera
{
    /// <summary>
    /// A fake implementation of the IMotorController interface for testing purposes.
    /// </summary>
    public class FakeMotorController : IMotorController
    {
        private static IMotorController? instance;

        static int Delay = 4; // ms
        private bool isConnected;
        private double currentPosition;

        /// <summary>
        /// Initializes a new instance of the <see cref="FakeMotorController"/> class.
        /// </summary>
        public FakeMotorController()
        {
            this.isConnected = false;
            this.currentPosition = 0.0;
            instance = this;
        }

        /// <summary>
        /// Gets the minimum position the motor can move to, in millimeters.
        /// </summary>
        public double MinPosition => 0.0;

        /// <summary>
        /// Gets the maximum position the motor can move to, in millimeters.
        /// </summary>
        public double MaxPosition => 10.0;

        /// <summary>
        /// Connect to the motor controller.
        /// </summary>
        /// <returns>True if the connection is successful, otherwise false.</returns>
        public bool Connect()
        {
            this.isConnected = true;
            return this.isConnected;
        }

        /// <summary>
        /// Disconnect from the motor controller.
        /// </summary>
        public void Disconnect()
        {
            this.isConnected = false;
        }

        private Thread? movementThread;
        private bool stopMovement;

        /// <summary>
        /// Move the motor to an absolute position - just send the command.
        /// </summary>
        /// <param name="position">The target position in mm.</param>
        /// <param name="speed">The movement speed in mm/s.</param>
        /// <param name="waitForCompletion">Flag indicating whether to wait for the movement to complete.</param>
        /// <returns>True if the move is successful, otherwise false.</returns>
        public bool MoveAbsolute(double position, double speed, bool waitForCompletion)
        {
            if (!this.isConnected)
            {
                return false;
            }

            if (position < this.MinPosition || position > this.MaxPosition)
            {
                return false;
            }

            stopMovement = false;

            if (movementThread != null && movementThread.IsAlive)
            {
                stopMovement = true;
                movementThread.Join();
            }

            movementThread = new Thread(() =>
            {
                double distance = Math.Abs(this.currentPosition - position);
                double direction = position > this.currentPosition ? 1 : -1;
                double step = speed / 100.0; // Update position every 10ms

                while (!stopMovement && Math.Abs(this.currentPosition - position) > step)
                {
                    lock (this)
                    {
                        this.currentPosition += direction * step;
                    }
                    Thread.Sleep(10);
                }

                if (!stopMovement)
                {
                    lock (this)
                    {
                        this.currentPosition = position;
                    }
                }
            });

            movementThread.Start();

            if (waitForCompletion)
            {
                movementThread.Join();
            }

            return true;
        }

        /// <summary>
        /// Get the current position of the motor.
        /// </summary>
        /// <returns>The current position as a <see cref="PositionProducedEventArgs"/>.</returns>
        public PositionProducedEventArgs GetPosition()
        {
            Thread.Sleep(FakeMotorController.Delay);
            var result = new PositionProducedEventArgs(
                timestamp: (double)Stopwatch.GetTimestamp() / (double)Stopwatch.Frequency,
                position: this.currentPosition);
            Thread.Sleep(FakeMotorController.Delay);
            return result;
        }
    }
}
