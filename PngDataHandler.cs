using System.IO.Compression;
using System.Runtime.CompilerServices;

namespace pixelraster
{
    // Guiding philosophy: use max size that spec allows, stuff relevant data in that, pass that around w/ bitdepth.
    public static class PngDataHandler
    {
        private static readonly int[] ColorSampleCountMap = [1, 0, 3, 1, 2, 0, 4];


        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int GetBitsPerPixel(byte colorType, byte bitDepth) => ColorSampleCountMap[colorType] * bitDepth;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int GetFilterBytesPerPixel(byte colorType, byte bitDepth) => Math.Max(GetBitsPerPixel(colorType, bitDepth) / 8, 1);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int GetBytesPerScanline(byte colorType, byte bitDepth, int width) => (int)Math.Ceiling(GetBitsPerPixel(colorType, bitDepth) * width / 8.0);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static ulong GetLongMask(int bigEndCoverCount) => ((1ul << bigEndCoverCount) - 1) << (sizeof(ulong) * 8 - bigEndCoverCount);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static ushort SampleTo16Bits(ushort sample, byte bitDepthOfSample) =>
            (ushort)Math.Round((double)sample * ((1 << sizeof(ushort) * 8) - 1) / ((1 << bitDepthOfSample) - 1));

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Rgba PaletteToRgba(ReadOnlySpan<byte> paletteBytes) =>
            new(SampleTo16Bits(paletteBytes[0], 8),
                SampleTo16Bits(paletteBytes[1], 8),
                SampleTo16Bits(paletteBytes[2], 8),
                ushort.MaxValue);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
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


        /// <summary>
        /// Takes concatenated bytes from IDAT chunks and turns them into RGBA data.
        /// </summary>
        /// <param name="bytes"></param>
        /// <param name="imageProps"></param>
        /// <returns></returns>
        public static List<Rgba> IdatToRgba(byte[] bytes, IHDRData imageProps, Rgba[] palette)
        {
            byte[] decompressed = Decompress(bytes);
            byte[] unfiltered = Unfilter(decompressed, imageProps);
            return BytesToRgbaList(unfiltered, imageProps, palette);
        }

        private static byte[] Decompress(ReadOnlySpan<byte> bytes)
        {
            using MemoryStream input = new(bytes.ToArray());
            using MemoryStream output = new();
            using ZLibStream zlib = new(input, CompressionMode.Decompress);
            zlib.CopyTo(output);
            return output.ToArray();
        }

        private static byte[] Unfilter(byte[] bytes, IHDRData imageProps)
        {
            // implementing "filter method 0"
            List<byte> unfiltered = [];
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

        private static Func<int, byte[], byte> GetFilterFunc(byte filterType, IHDRData imageProps)
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
                    return (byte)((bytes[ptr] + (prev + prior) / 2.0) % 256);
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

        private static List<Rgba> BytesToRgbaList(byte[] bytes, IHDRData imageProps, Rgba[] palette)
        {
            int sampleCount = ColorSampleCountMap[imageProps.ColorType];
            int bitsPerColor = imageProps.BitDepth * sampleCount;
            List<Rgba> rgbaPixels = [];

            for (int i = 0; i < imageProps.Width * imageProps.Height; i++)
            {
                // packed big-endian long with padded 0's
                ulong rawPixelData = GetRawPixelLong(bytes, i * bitsPerColor, imageProps.BitDepth, imageProps.ColorType);
                ulong sampleMask = GetLongMask(imageProps.BitDepth);
                List<ushort> samples = [];
                for (int sampleIdx = 0; sampleIdx < sampleCount; sampleIdx++, sampleMask >>= imageProps.BitDepth)
                {
                    samples.Add((ushort)((rawPixelData & sampleMask) >> (sizeof(ulong) * 8 - (sampleIdx + 1) * imageProps.BitDepth)));
                }
                rgbaPixels.Add(SampleToColor(samples, imageProps, palette));
            }
            return rgbaPixels;
        }

        private static ulong GetRawPixelLong(byte[] bytes, int ptr, int bitDepth, int colorType)
        {
            // pack a single pixel data into a big-endian ulong, to prevent the headache of dealing
            // with pixel data that crosses byte boundaries (.net may offer something to remedy?).
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
            bits <<= (sizeof(ulong) - usedBytes) * 8;
            return bits & GetLongMask(usedBits);
        }

        private static Rgba SampleToColor(List<ushort> samples, IHDRData imageProps, Rgba[] palette)
        {
            if (imageProps.ColorType == 3) return palette[samples[0]];

            List<ushort> formatted = [];
            foreach (ushort sample in samples) formatted.Add(SampleTo16Bits(sample, imageProps.BitDepth));
            return imageProps.ColorType switch
            {
                0 => new(formatted[0], formatted[0], formatted[0], ushort.MaxValue),
                2 => new(formatted[0], formatted[1], formatted[2], ushort.MaxValue),
                4 => new(formatted[0], formatted[0], formatted[0], formatted[1]),
                6 => new(formatted[0], formatted[1], formatted[2], formatted[3]),
                _ => throw new Exception($"Unhandled color type: {imageProps.ColorType}")
            };
        }
    }
}
