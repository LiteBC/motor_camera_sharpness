using System;
using System.Threading;
using MotionCamera;

class Program
{
    [STAThread]
    static void Main(string[] args)
    {
        double startPosition = 0.0; // mm
        double targetPosition = 5.0; // mm
        double speed = 1.0; // mm
        IMotorController motor = new PiMotorController.PiMotorController("COM3");
        motor.Connect();
        motor.MoveAbsolute(startPosition, speed, true);
        
        IFrameProducer camera = new BaslerCamera.BaslerFrameGrabber();
        camera.InitializeCamera();
        camera.Start();
        List<(FrameProducedEventArgs frame, double position, double score) > tuples = new List<(FrameProducedEventArgs, double, double)>();
        camera.FrameProduced += (sender, e) =>
        {
            Console.WriteLine($"Frame produced: {e.Timestamp}");

            PositionProducedEventArgs position = motor.GetPosition();

            double score = Utils.EstimateSharpness(e);

            Console.WriteLine($"Sharpness score: {score:F2}, Position: {position.Position}");

            tuples.Add((e, 0, score));
        };  

        motor.MoveAbsolute(targetPosition, speed, false);

        Console.WriteLine("Starting motor movement...");

        PositionProducedEventArgs position = motor.GetPosition();

        while (position.Position < targetPosition)
        {
            Console.WriteLine($"Current Position: {position.Position:F2} mm, t={position.Timestamp}");
            Thread.Sleep(100); // 100ms delay

            position = motor.GetPosition();
        }

        Console.WriteLine($"Motor reached target position. {motor.GetPosition().Position}");

        camera.Stop();

        var bestTuple = tuples.MaxBy(t => t.Item3);

        Console.WriteLine($"Best frame: {bestTuple.frame.Timestamp}, Position: {bestTuple.position}, Score: {bestTuple.score}");

        motor.MoveAbsolute(bestTuple.position, speed, true);
    }
}
