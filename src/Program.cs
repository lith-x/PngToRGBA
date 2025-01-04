using System.Buffers.Binary;
using System.Text;

namespace PngToFF
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
            else { args = ["../../../../resources/avocado16.png"]; }
            // Rgba[][] pixels = FarbFeldToRgba("../../../../test/ti.ff");
            // ToFarbfeldFile("mytinypng", pixels);
            Rgba[][] pixels = PngFile.ProcessFile(args[0]);
            ToFarbfeldFile(args.Length >= 2 ? args[1] : "out", pixels);
        }

        // DEBUG / TESTING

        /// <summary>
        /// Converts RGBA array to simplest image format I could find, "farbfeld"
        /// Comes with C-based conversion tools, good for testing.
        /// https://tools.suckless.org/farbfeld/
        /// </summary>
        /// <param name="fileName"></param>
        /// <param name="pixels"></param>
        public static void ToFarbfeldFile(string fileName, Rgba[][] pixels)
        {
            string outputpath;
            outputpath = DEBUG ? $"../../../../{fileName}.ff" : $"./{fileName}.ff";
            using FileStream file = File.Create(outputpath);
            file.Write(Encoding.ASCII.GetBytes("farbfeld"));
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
            FileStream fs = File.Create($"../../../../{fileAndExt}");
            MemoryStream ms = new(Encoding.ASCII.GetBytes(chunked));
            ms.CopyTo(fs);
            ms.Close();
            fs.Close();
            Environment.Exit(0);
        }

        public static Rgba[][] FarbFeldToRgba(string filePath)
        {
            ReadOnlySpan<byte> fileBytes = File.ReadAllBytes(filePath);
            string header = Encoding.ASCII.GetString(fileBytes[..8]);
            if (header != "farbfeld") throw new ArgumentException($"{filePath} does not contain farbfeld header.");
            int byteWidth = BinaryPrimitives.ReadInt32BigEndian(fileBytes[8..12]) * 8;
            int height = BinaryPrimitives.ReadInt32BigEndian(fileBytes[12..16]);
            var rgbaBytes = fileBytes[16..];
            List<Rgba[]> pixels = [];
            for (int y = 0; y < height; y++)
            {
                List<Rgba> line = [];
                for (int x = 0; x < byteWidth; x += 8)
                {
                    int pixelIdx = y * byteWidth + x;
                    line.Add(new()
                    {
                        Red = (ushort)BinaryPrimitives.ReadInt16BigEndian(rgbaBytes[pixelIdx..(pixelIdx + 2)]),
                        Green = (ushort)BinaryPrimitives.ReadInt16BigEndian(rgbaBytes[(pixelIdx + 2)..(pixelIdx + 4)]),
                        Blue = (ushort)BinaryPrimitives.ReadInt16BigEndian(rgbaBytes[(pixelIdx + 4)..(pixelIdx + 6)]),
                        Alpha = (ushort)BinaryPrimitives.ReadInt16BigEndian(rgbaBytes[(pixelIdx + 6)..(pixelIdx + 8)])
                    });
                }
                pixels.Add([.. line]);
            }
            return [.. pixels];
        }
    }
}
