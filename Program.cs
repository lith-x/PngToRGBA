using System.Buffers.Binary;
using System.Runtime.InteropServices;
using System.Text;

namespace pixelraster
{
    public class Program
    {
        public static readonly bool DEBUG = true;

        public static void Main(string[] args)
        {
            if (!DEBUG && args.Length != 1)
            {
                Console.WriteLine($"Usage: {AppDomain.CurrentDomain.FriendlyName} input.png");
            }
            else { args = ["../../../tinypng.png"]; }
            var fileBytes = File.ReadAllBytes(args[0]);
            Rgba[][] pixels = PngFile.ProcessFile(fileBytes);
            ToFaldbeld("firstgo", pixels);


            // byte[] samplebytes = [0x12, 0x34, 0x56, 0x78];
            // IHDRData sampleData = new()
            // {
            //     Width = 2,
            //     Height = 2,
            //     BitDepth = 4,
            //     ColorType = 2
            // };
            // var byteArr = new ReadOnlySpan<byte>(fileBytes);
            // IHDRData tinyPngData = new()
            // {
            //     Width = 256,
            //     Height = 256,
            //     BitDepth = 8,
            //     ColorType = 3,
            //     CompressionMethod = 0,
            //     FilterMethod = 0,
            //     InterlaceMethod = 0
            // };
            // byte[] idat = PngChunkHandler.HandleIDATData(byteArr.Slice(0x38, 84).ToArray(), tinyPngData);
            // PixelColor[] sampleColors = [new(0xACAC, 0xC8C8, 0xF2F2, 0xFFFF)];
            // SampleOutput(tinyPngData, sampleColors);
        }

        /* -------- UTILS/DEBUG -------- */
        public static void SampleOutput(IHDRData data, Rgba[] pixels)
        {
            string outputpath;
            if (DEBUG) outputpath = "../../../output.ff";
            else outputpath = "./output.ff";
            using FileStream file = File.Create(outputpath);
            file.Write(Encoding.ASCII.GetBytes("faldbeld"));
            Span<byte> intbytes = new(new byte[4]);
            BinaryPrimitives.WriteInt32BigEndian(intbytes, data.Width);
            file.Write(intbytes);
            BinaryPrimitives.WriteInt32BigEndian(intbytes, data.Height);
            file.Write(intbytes);
            Span<byte> pixelbyte = new(new byte[2]);
            foreach (var pixel in pixels)
            {
                BinaryPrimitives.WriteUInt16BigEndian(pixelbyte, pixel.Red);
                file.Write(pixelbyte);
                BinaryPrimitives.WriteUInt16BigEndian(pixelbyte, pixel.Green);
                file.Write(pixelbyte);
                BinaryPrimitives.WriteUInt16BigEndian(pixelbyte, pixel.Blue);
                file.Write(pixelbyte);
                BinaryPrimitives.WriteUInt16BigEndian(pixelbyte, pixel.Alpha);
                file.Write(pixelbyte);
            }
        }

        /// <summary>
        /// Converts RGBA array to simplest image format I could find, "faldbeld".
        /// Comes with C-based conversion tools, good for testing.
        /// https://tools.suckless.org/farbfeld/
        /// </summary>
        /// <param name="fileName"></param>
        /// <param name="pixels"></param>
        public static void ToFaldbeld(string fileName, Rgba[][] pixels)
        {
            string outputpath;
            if (DEBUG) outputpath = $"../../../{fileName}.ff";
            else outputpath = $"./{fileName}.ff";
            using FileStream file = File.Create(outputpath);
            file.Write(Encoding.ASCII.GetBytes("faldbeld"));
            Span<byte> intbytes = new(new byte[4]);
            // Width
            BinaryPrimitives.WriteInt32BigEndian(intbytes, pixels[0].Length);
            file.Write(intbytes);
            // Height
            BinaryPrimitives.WriteInt32BigEndian(intbytes, pixels.Length);
            file.Write(intbytes);
            Span<byte> pixelbyte = new(new byte[2]);
            foreach (var line in pixels)
            {
                foreach (var pixel in line)
                {
                    BinaryPrimitives.WriteUInt16BigEndian(pixelbyte, pixel.Red);
                    file.Write(pixelbyte);
                    BinaryPrimitives.WriteUInt16BigEndian(pixelbyte, pixel.Green);
                    file.Write(pixelbyte);
                    BinaryPrimitives.WriteUInt16BigEndian(pixelbyte, pixel.Blue);
                    file.Write(pixelbyte);
                    BinaryPrimitives.WriteUInt16BigEndian(pixelbyte, pixel.Alpha);
                    file.Write(pixelbyte);
                }
            }
        }
    }
}

enum Shape
{
    SQUARE,
    HEXAGON
}