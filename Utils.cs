using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MotionCamera;

namespace MotionCamera
{
    internal class Utils
    {

        public static double EstimateSharpness(FrameProducedEventArgs frame)
        {
            int height = frame.Height;
            int width = frame.Width;
            byte[,] pixels = frame.PixelData;

            int[,] kernel = new int[,]
            {
        { 0, -1,  0 },
        { -1, 4, -1 },
        { 0, -1,  0 }
            };

            double sum = 0;
            double sumSquares = 0;
            int count = 0;

            for (int y = 1; y < height - 1; y++)
            {
                for (int x = 1; x < width - 1; x++)
                {
                    double laplacian = 0;

                    for (int ky = -1; ky <= 1; ky++)
                    {
                        for (int kx = -1; kx <= 1; kx++)
                        {
                            laplacian += kernel[ky + 1, kx + 1] * pixels[y + ky, x + kx];
                        }
                    }

                    sum += laplacian;
                    sumSquares += laplacian * laplacian;
                    count++;
                }
            }

            double mean = sum / count;
            double variance = (sumSquares / count) - (mean * mean);
            return variance; // Higher = sharper
        }

        public static List<(string FileName, FrameProducedEventArgs Frame)> LoadSortedFrames(string folderPath)
        {
            return Directory.GetFiles(folderPath, "*.tif")
                .Select(path => new
                {
                    Path = path,
                    Key = float.Parse(Path.GetFileNameWithoutExtension(path), CultureInfo.InvariantCulture)
                })
                .OrderBy(x => x.Key)
                .Select(x => (
                    FileName: Path.GetFileName(x.Path),
                    Frame: CreateFrameProducedEventArgsFromTif(x.Path)
                ))
                .ToList();
        }


        private static FrameProducedEventArgs CreateFrameProducedEventArgsFromTif(string imagePath)
        {
            using (Bitmap bmp = new Bitmap(imagePath))
            {
                int width = bmp.Width;
                int height = bmp.Height;
                byte[,] pixelData = new byte[height, width];

                for (int y = 0; y < height; y++)
                {
                    for (int x = 0; x < width; x++)
                    {
                        Color color = bmp.GetPixel(x, y);
                        // Assuming grayscale: use R channel
                        pixelData[y, x] = color.R;
                    }
                }

                return new FrameProducedEventArgs(
                    timestamp: DateTime.UtcNow.Ticks, // You can adjust this logic
                    height: height,
                    width: width,
                    pixelData: pixelData
                );
            }
        }
    }
}
