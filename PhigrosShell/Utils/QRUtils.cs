using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using ZXing;
using ZXing.ImageSharp;
using ZXing.QrCode;

namespace PhigrosShell.Utils;

public static class QRUtils
{
    public static void OutputToConsole(string text)
    {
        var image = new ZXing.ImageSharp.BarcodeWriter<Rgba32>
        {
            Format = BarcodeFormat.QR_CODE,
            Options = new QrCodeEncodingOptions
            {
                Width = 33,
                Height = 33,
                Margin = 1
            }
        }.WriteAsImageSharp<Rgba32>(text);

        var pixels = new int[image.Width, image.Height];
        for (int x = 0; x < image.Width; x++)
        {
            for (int y = 0; y < image.Height; y++)
            {
                pixels[x, y] = image[x, y].B <= 180 ? 1 : 0;
            }
        }

        for (int y = 0; y < image.Height; y++)
        {
            for (int x = 0; x < image.Width; x++)
            {
                if (pixels[x, y] == 0)
                {
                    Console.BackgroundColor = ConsoleColor.Black;
                    Console.ForegroundColor = ConsoleColor.Black;
                    Console.Write("  ");
                }
                else
                {
                    Console.BackgroundColor = ConsoleColor.White;
                    Console.ForegroundColor = ConsoleColor.White;
                    Console.Write("  ");
                }
                Console.ResetColor();
            }
            Console.Write("\n");
        }
    }
}
