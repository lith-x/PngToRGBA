// Data types go here

// TODO: It would be nice to transform all necessary data into a nice little record struct to pass around

namespace pixelraster
{
    public readonly record struct PixelColor(ushort Red, ushort Green, ushort Blue, ushort Alpha);

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

    public interface IChunkData { }
    public record struct IHDRData
    (
        int Width,
        int Height,
        byte BitDepth,
        byte ColorType,
        byte CompressionMethod,
        byte FilterMethod,
        byte InterlaceMethod
    ) : IChunkData;

    public record struct PLTEData
    (
        List<PixelColor> Colors
    ) : IChunkData;
}
