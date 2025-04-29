using System;
using System.Threading;
using MotionCamera;

class Program
{
    static void Main(string[] args)
    {
        //List<(string FileName, FrameProducedEventArgs Frame)> frames = Utils.LoadSortedFrames($"C:\\Users\\user\\Downloads\\MotionCamera\\MotionCamera\\assets\\");
        //foreach (var frame in frames)
        //{
        //    double score = Utils.EstimateSharpness(frame.Frame);
        //    Console.WriteLine($"File: {frame.FileName}, Timestamp: {frame.Frame.Timestamp}, score: {score}");
        //}
        //return;

        IMotorController motor = new FakeMotorController();
        motor.Connect();
        motor.MoveAbsolute(0.0, 10.0, true);

        IFrameProducer camera = new FakeCamera();
        camera.InitializeCamera();
        camera.Start();
        List<(FrameProducedEventArgs frame, double position, double score) > tuples = new List<(FrameProducedEventArgs, double, double)>();
        camera.FrameProduced += (sender, e) =>
        {
            Console.WriteLine($"Frame produced: {e.Timestamp}");

            PositionProducedEventArgs position = motor.GetPosition();

            double score = Utils.EstimateSharpness(e);

            Console.WriteLine($"Sharpness score: {score:F2}, Position: {position.Position}");

            tuples.Add((e, position.Position, score));
        };  

        motor.MoveAbsolute(3.0, 1.0, false);

        Console.WriteLine("Starting motor movement...");

        PositionProducedEventArgs position = motor.GetPosition();

        while (position.Position < 3.0)
        {
            //Console.WriteLine($"Current Position: {position.Position:F2} mm, t={position.Timestamp}");
            Thread.Sleep(200); // 100ms delay

            position = motor.GetPosition();
        }

        camera.Stop();

        var bestTuple = tuples.MaxBy(t => t.Item3);

        Console.WriteLine($"Best frame: {bestTuple.frame.Timestamp}, Position: {bestTuple.position}, Score: {bestTuple.score}");

        motor.MoveAbsolute(bestTuple.position, 1.0, true);

        Console.WriteLine($"Motor reached target position. {motor.GetPosition().Position}");
    }
}
