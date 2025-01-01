using System.Buffers.Binary;
using System.Text;

namespace pixelraster
{
    public class PngFile
    {
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
                                ((PLTEData)chunkData).Colors.Add(PngDataHandler.HandlePaletteColorBytes(fileBytes.Slice(i, 3)));
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

        private static int GetNextInt(ref int ptr, ReadOnlySpan<byte> fileBytes)
        {
            int val = BinaryPrimitives.ReadInt32BigEndian(fileBytes.Slice(ptr, 4));
            ptr += 4;
            return val;
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