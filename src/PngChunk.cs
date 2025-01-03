// Data types go here

// TODO: It would be nice to transform all necessary data into a nice little record struct to pass around

namespace PngToFF
{
    public readonly record struct Rgba(ushort Red, ushort Green, ushort Blue, ushort Alpha);

    public enum ChunkType
    {
        IHDR, PLTE, IDAT, IEND, bKGD, cHRM, gAMA, hIST, pHYs, sBIT, tEXt, tIME, tRNS, zTXt
    }

    public record struct PngChunk
    (
        int Length,
        string Type,
        IChunkData Data,
        uint? CRC
    );

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
        List<Rgba> Colors
    ) : IChunkData;
}
