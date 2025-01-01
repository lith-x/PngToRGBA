using System.Buffers.Binary;
using System.IO.Compression;
using System.Text;

namespace pixelraster
{
    public class PngChunkHandler
    {
        public enum ChunkType
        {
            IHDR, PLTE, IDAT, IEND, bKGD, cHRM, gAMA, hIST, pHYs, sBIT, tEXt, tIME, tRNS, zTXt
        }

        public record PngChunk
        {
            public int Length;
            public required string Type;
            public required IChunkData Data;
            public uint? CRC;
        }

        /// <remark><paramref name="ptr"/> should point to the head of the IHDR chunk (data length field).</remark>
        public static List<PngChunk> ProcessFile(ref int ptr, ReadOnlySpan<byte> fileBytes)
        {
            List<PngChunk> chunks = [];
            IHDRData imageProps = new();
            List<byte> IDATData = [];
            while (ptr < fileBytes.Length)
            {
                int chunkDataLength = GetNextInt(ref ptr, fileBytes);
                string chunkLabel = Encoding.Latin1.GetString(fileBytes.Slice(ptr, 4));
                ptr += 4;
                IChunkData chunkData = new IHDRData { };
                switch (chunkLabel)
                {
                    case "IHDR":
                        {
                            chunkData = new IHDRData
                            {
                                Width = GetNextInt(ref ptr, fileBytes),
                                Height = GetNextInt(ref ptr, fileBytes),
                                BitDepth = fileBytes[ptr++],
                                ColorType = fileBytes[ptr++],
                                CompressionMethod = fileBytes[ptr++],
                                FilterMethod = fileBytes[ptr++],
                                InterlaceMethod = fileBytes[ptr++],
                            };
                            imageProps = (IHDRData)chunkData;
                        }
                        break;
                    case "PLTE":
                        {
                            chunkData = new PLTEData { Colors = [] };
                            for (int i = ptr; i < ptr + chunkDataLength; i++)
                            {
                                ((PLTEData)chunkData).Colors.Add(new(fileBytes[i], fileBytes[i + 1], fileBytes[i + 2]));
                            }
                        }
                        break;
                    case "IDAT":
                        {
                            // decompress

                            // defilter
                        }
                        break;
                    default:
                        break;
                }

                chunks.Add(new PngChunk
                {
                    Length = chunkDataLength,
                    Type = chunkLabel,
                    Data = chunkData
                });
                break;
            }

            return chunks;
        }

        public static byte[] HandleIDATData(byte[] bytes, IHDRData imageProps)
        {
            byte[] decompressed = Decompress(bytes);
            byte[] unfiltered = Unfilter(decompressed, imageProps);
            List<PixelColor> colors = BitsToColorData(unfiltered, imageProps);
            return [];
        }

        // IHDR, PLTE, IDAT, IEND, bKGD, cHRM, gAMA, hIST, pHYs, sBIT, tEXt, tIME, tRNS, zTXt
        private static int GetNextInt(ref int ptr, ReadOnlySpan<byte> fileBytes)
        {
            int val = BinaryPrimitives.ReadInt32BigEndian(fileBytes.Slice(ptr, 4));
            ptr += 4;
            return val;
        }

        public static byte[] Decompress(ReadOnlySpan<byte> bytes)
        {
            using MemoryStream input = new(bytes.ToArray());
            using MemoryStream output = new();
            using ZLibStream zlib = new(input, CompressionMode.Decompress);
            zlib.CopyTo(output);
            return output.ToArray();
        }

        public static byte[] Unfilter(byte[] bytes, IHDRData imageProps)
        {
            // implementing "filter method 0"
            List<byte> unfiltered = [];
            // TODO: tihs is probably incorrect, doesn't take into account color type.
            int byteWidth = imageProps.Width * imageProps.BitDepth / 8;
            for (int scanline = 0; scanline < imageProps.Height; scanline++)
            {
                // scanlines start with filter type
                byte filterType = bytes[scanline * imageProps.Width];
                var unfilterFn = GetFilterFunc(filterType, imageProps);
                for (int i = 1; i < byteWidth; i++)
                {
                    byte unbyte = unfilterFn(scanline * imageProps.Width + i, bytes);
                    unfiltered.Add(unbyte);
                }
            }
            return [.. unfiltered];
        }

        public static Func<int, byte[], byte> GetFilterFunc(byte filterType, IHDRData imageProps)
        {
            int bpp = GetFilterBytesPerPixel(imageProps.ColorType, imageProps.BitDepth);
            int scanlineWidth = GetBytesPerScanline(imageProps.ColorType, imageProps.BitDepth, imageProps.Width);
            return filterType switch
            {
                0 => (int ptr, byte[] bytes) => bytes[ptr],
                1 => (int ptr, byte[] bytes) =>
                {
                    byte prev = ptr % imageProps.Width != 0 ? bytes[ptr - bpp] : (byte)0;
                    return (byte)((bytes[ptr] + prev) % 256);
                }
                ,
                2 => (int ptr, byte[] bytes) =>
                {
                    byte prior = ptr >= scanlineWidth ? bytes[ptr - scanlineWidth] : (byte)0;
                    return (byte)((bytes[ptr] + prior) % 256);
                }
                ,
                3 => (int ptr, byte[] bytes) =>
                {
                    byte prev = ptr % imageProps.Width != 0 ? bytes[ptr - bpp] : (byte)0;
                    byte prior = ptr >= scanlineWidth ? bytes[ptr - scanlineWidth] : (byte)0;
                    return (byte)((bytes[ptr] + (prev + prior) / 2) % 256);
                }
                ,
                4 => (int ptr, byte[] bytes) =>
                {
                    byte left = ptr % imageProps.Width != 0 ? bytes[ptr - bpp] : (byte)0;
                    byte up = ptr >= scanlineWidth ? bytes[ptr - scanlineWidth] : (byte)0;
                    byte upLeft = ptr % imageProps.Width != 0 && ptr >= scanlineWidth ? bytes[ptr - scanlineWidth - bpp] : (byte)0;
                    return (byte)((bytes[ptr] + GetPaethVal(left, up, upLeft)) % 256);
                }
                ,
                _ => throw new Exception($"Unexpected filter type: {filterType}")
            };
        }

        private static int GetBitsPerPixel(byte colorType, byte bitDepth) => ColorSampleCountMap[colorType] * bitDepth;
        private static readonly int[] ColorSampleCountMap = [1, 0, 3, 1, 2, 0, 4];
        private static int GetFilterBytesPerPixel(byte colorType, byte bitDepth) => Math.Max(GetBitsPerPixel(colorType, bitDepth) / 8, 1);
        private static int GetBytesPerScanline(byte colorType, byte bitDepth, int width) => (int)Math.Ceiling((double)(GetBitsPerPixel(colorType, bitDepth) * width) / 8.0);
        private static ulong GetLongMask(int bigEndCoverCount) => ((1ul << bigEndCoverCount) - 1) << (64 - bigEndCoverCount);
        
        private static byte GetPaethVal(byte left, byte up, byte upLeft)
        {
            int p = left + up - upLeft;
            int pa = Math.Abs(p - left);
            int pb = Math.Abs(p - up);
            int pc = Math.Abs(p - upLeft);
            return Math.Min(pa, Math.Min(pb, pc)) switch
            {
                int c when c == pa => left,
                int c when c == pb => up,
                _ => upLeft
            };
        }

        public static List<PixelColor> BitsToColorData(byte[] bytes, IHDRData imageProps)
        {
            int sampleCount = ColorSampleCountMap[imageProps.ColorType];
            int bitsPerColor = imageProps.BitDepth * sampleCount;

            for (int i = 0; i < imageProps.Width * imageProps.Height; i++)
            {
                // packed big-endian long with padded 0's
                ulong rawPixelData = GetRawPixelLong(bytes, i * bitsPerColor, imageProps.BitDepth, imageProps.ColorType);
                ulong sampleMask = GetLongMask(imageProps.BitDepth);
                List<ushort> samples = [];
                for (int sampleIdx = 0; sampleIdx < sampleCount; sampleIdx++, sampleMask >>= imageProps.BitDepth)
                {
                    samples.Add((ushort)((rawPixelData & sampleMask) >> (64 - (sampleIdx + 1) * imageProps.BitDepth)));
                }
                // TODO: samples has all needed for converting to PixelColor. do that here.
            }
            return [];
        }

        public static ulong GetRawPixelLong(byte[] bytes, int ptr, int bitDepth, int colorType)
        {
            // pack a single pixel data into a ulong, to prevent the headache of dealing
            // with pixel data that crosses byte boundaries (may be a simple case, look into doing anyway).
            // this is just a way to pack relevant data into a general package in such
            // a way that I can actually think about how to deal with it.
            int usedBits = bitDepth * ColorSampleCountMap[colorType];
            int usedBytes = (int)Math.Ceiling(usedBits / 8.0);
            ulong bits = 0;
            for (int i = ptr; i < ptr + usedBytes; i++)
            {
                bits <<= 8;
                bits += bytes[i];
            }
            bits <<= 64 - usedBytes * 8;
            return bits & GetLongMask(usedBits);
        }

        public static ushort LongToColor(ulong data, int bitDepth, int colorType)
        {
            /* color types: num:bits
                0: GREYSCALE(1/2/4/8/16)
                2: R(8/16), G(8/16), B(8/16)
                3: 0-indexed PLTE index (1/2/4/8)
                4: GREYSCALE(8/16), Alpha(8/16)
                6: R(8/16), G(8/16), B(8/16), A(8/16) */
            // iterate over all samples.
            // if scanline data does not cleanly fit into byte boundary,
            //   byte is padded with trash (typically 0's)
            ulong sampleMask = ((1ul << bitDepth) - 1) << (64 - bitDepth);
            ushort[] vals = [0, 0, 0, 0];
            for (int i = 0; i < ColorSampleCountMap[colorType]; i++)
            {
            }
            return new();
        }
    }
}
/*

fix the god-awful conversion: think of how to get indexing values to point
to relevant bits

bit depth = 1, samples = 1
80 00 00 00  00 00 00 00

bit depth = 2, samples = 2
F0 00 00 00  00 00 00 00

bit depth = 4, samples = 3
FF F0 00 00  00 00 00 00

(ranges inclusive, determined by bitdepth)
first sample: 60-63 -> 0-3
second sample: 56-59 -> 0-3
third sample: 52-55 -> 0-3

OPEN QUESTION: does 'width' include filter byte?

8 * (8)
FF FF FF FF FF FF FF FF
FF = 255 = byte full

TODO: concatenate IDAT data 
TODO: de-interlacing
*/