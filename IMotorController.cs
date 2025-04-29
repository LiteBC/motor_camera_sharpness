namespace MotionCamera
{
    /// <summary>
    /// Represents a motor controller interface.
    /// </summary>
    public interface IMotorController
    {
        static IMotorController? instance;

        /// <summary>
        /// Connect to the motor controller.
        /// </summary>
        /// <returns>success or not.</returns>
        bool Connect();

        /// <summary>
        /// Disconnect from the motor controller.
        /// </summary>
        void Disconnect();

        /// <summary>
        /// Move the motor to an absolute position - just send the command.
        /// </summary>
        /// <param name="position">The target position.</param>
        /// <returns>success or not.</returns>
        bool MoveAbsolute(double position, double speed = 1.0 /*mm/s*/, bool waitForCompletion = true);

        /// <summary>
        /// Get the current position of the motor.
        /// </summary>
        /// <returns>The current position as a <see cref="PositionProducedEventArgs"/>.</returns>
        PositionProducedEventArgs GetPosition();

        /// <summary>
        /// Gets the minimum position the motor can move to, in millimeters.
        /// </summary>
        double MinPosition { get; }

        /// <summary>
        /// Gets the maximum position the motor can move to, in millimeters.
        /// </summary>
        double MaxPosition { get; }
    }
}
