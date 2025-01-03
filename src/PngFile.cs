using System.Buffers.Binary;
using System.Text;

namespace PngToFF
{
    public class PngFile
    {
        private static readonly byte[] PNG_HEADER = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];

        public static Rgba[][] ProcessFile(string filePath)
        {
            ReadOnlySpan<byte> fileBytes = File.ReadAllBytes(filePath);
            // header
            if (!fileBytes[..8].SequenceEqual(PNG_HEADER))
                throw new Exception("Only PNG files can be accepted (file does not have PNG header).");
            BitPointer ptr = new() { Byte = 8 };
            IHDRData imageProps = new();
            List<Rgba> palette = [];
            List<byte> IDATData = [];
            while (ptr.Byte < fileBytes.Length)
            {
                int chunkDataLength = GetNextInt(ptr, fileBytes);
                string chunkLabel = Encoding.Latin1.GetString(fileBytes.Slice(ptr.Byte, 4));
                ptr.Byte += 4;
                switch (chunkLabel)
                {
                    case "IHDR":
                        {
                            imageProps = new IHDRData
                            {
                                Width = GetNextInt(ptr, fileBytes),
                                Height = GetNextInt(ptr, fileBytes),
                                BitDepth = fileBytes[ptr.Byte++],
                                ColorType = fileBytes[ptr.Byte++],
                                CompressionMethod = fileBytes[ptr.Byte++],
                                FilterMethod = fileBytes[ptr.Byte++],
                                InterlaceMethod = fileBytes[ptr.Byte++],
                            };
                        }
                        break;
                    case "PLTE":
                        {
                            int chunkDataEnd = ptr.Byte + chunkDataLength;
                            for (; ptr.Byte < chunkDataEnd; ptr.Byte += 3)
                                palette.Add(PngDataHandler.PaletteToRgba(fileBytes.Slice(ptr.Byte, 3)));
                        }
                        break;
                    case "IDAT":
                        {
                            int chunkDataEnd = ptr.Byte + chunkDataLength;
                            for (; ptr.Byte < chunkDataEnd; ptr.Byte++)
                            {
                                IDATData.Add(fileBytes[ptr.Byte]);
                            }
                        }
                        break;
                    case "IEND":
                    default:
                        // TODO: handle more chunk types
                        ptr.Byte += chunkDataLength;
                        break;
                }
                ptr.Byte += 4; // TODO: handle CRC check?
            }
            return PngDataHandler.IdatToRgba([.. IDATData], imageProps, [.. palette])
                .Chunk(imageProps.Width).ToArray();
        }

        private static int GetNextInt(BitPointer ptr, ReadOnlySpan<byte> fileBytes)
        {
            int val = BinaryPrimitives.ReadInt32BigEndian(fileBytes.Slice(ptr.Byte, 4));
            ptr.Byte += 4;
            return val;
        }
    }
}
