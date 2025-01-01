namespace pixelraster
{
    public readonly record struct PixelColor(ushort Red, ushort Green, ushort Blue, ushort Alpha);

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
        List<Tuple<byte, byte, byte>> Colors
    ) : IChunkData;
}
