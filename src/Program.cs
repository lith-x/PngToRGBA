using System.Buffers.Binary;
using System.Text;

namespace pixelraster
{
    public class Program
    {
        public static readonly bool DEBUG = true;

        public static void Main(string[] args)
        {
            if (!DEBUG && args.Length != 2)
            {
                Console.WriteLine($"Usage: {AppDomain.CurrentDomain.FriendlyName} input.png outputname");
            }
            else { args = ["../../../test/img.png"]; }
            var fileBytes = File.ReadAllBytes(args[0]);
            Rgba[][] pixels = PngFile.ProcessFile(fileBytes);
            ToFaldbeld(args[1], pixels);
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
            outputpath = DEBUG ? $"../../../{fileName}.ff" : $"./{fileName}.ff";
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

        public static void WriteFileStringEarlyExit(byte[] bytes, string fileAndExt, int width)
        {
            var chunked = bytes.Chunk(width)
                .Select(x => x
                    .ToList()
                    .Select(x => $"{x:X2} ".ToCharArray())
                    .SelectMany(x => x)
                    .Append('\n')
                    .ToArray()
                ).SelectMany(x => x)
                .ToArray();
            FileStream fs = File.Create($"../../../test/{fileAndExt}");
            MemoryStream ms = new(Encoding.ASCII.GetBytes(chunked));
            ms.CopyTo(fs);
            ms.Close();
            fs.Close();
            Environment.Exit(0);
        }
    }
}
