using System.Diagnostics;
using System.IO.Compression;
using System.Runtime.CompilerServices;

namespace PngToFF
{
    public static class PngData
    {
        private static readonly int[] ColorSampleCountMap = [1, 0, 3, 1, 2, 0, 4];


        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int GetBitsPerPixel(byte colorType, byte bitDepth) => ColorSampleCountMap[colorType] * bitDepth;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int GetFilterBytesPerPixel(byte colorType, byte bitDepth) => (int)Math.Max(GetBitsPerPixel(colorType, bitDepth) / 8.0, 1);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int GetBytesPerScanline(byte colorType, byte bitDepth, int width) => (int)Math.Ceiling(GetBitsPerPixel(colorType, bitDepth) * width / 8.0);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static ulong GetBigEndianMask(int bigEndCoverCount) => ((1ul << bigEndCoverCount) - 1) << (sizeof(ulong) * 8 - bigEndCoverCount);

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
            byte[] decompressed = DecompressIdat(bytes);
            byte[] unfiltered = UnfilterIdat(decompressed, imageProps);
            Program.WriteFileStringEarlyExit(decompressed, "unfiltered.txt", 128);

            // TODO: de-interlacing
            return RawIdatBytesToRgbaList(unfiltered, imageProps, palette);
        }

        private static byte[] DecompressIdat(byte[] bytes)
        {
            using MemoryStream input = new(bytes);
            using MemoryStream output = new();
            using ZLibStream zlib = new(input, CompressionMode.Decompress);
            zlib.CopyTo(output);
            return output.ToArray();
        }

        private static byte[] UnfilterIdat(byte[] bytes, IHDRData imageProps)
        {
            // implementing "filter method 0", handled byte by byte, no need for ptr
            List<byte> unfiltered = [];
            // Width of scanline in bytes (extra byte at beginning as filter type)
            int scanlineByteWidth = GetBytesPerScanline(imageProps.ColorType, imageProps.BitDepth, imageProps.Width) + 1;
            for (int scanline = 0; scanline < imageProps.Height; scanline++)
            {
                int scanlineStart = scanline * scanlineByteWidth;
                int scanlineEnd = (scanline + 1) * scanlineByteWidth;
                byte filterType = bytes[scanlineStart];
                var unfilterFn = GetFilterFunc(filterType, imageProps);
                for (int i = scanlineStart + 1; i < scanlineEnd; i++)
                {
                    byte unbyte = unfilterFn(i, scanlineStart, bytes);
                    unfiltered.Add(unbyte);
                }
            }
            return [.. unfiltered];
        }

        private static Func<int, int, byte[], byte> GetFilterFunc(byte filterType, IHDRData imageProps)
        {
            int bpp = GetFilterBytesPerPixel(imageProps.ColorType, imageProps.BitDepth);
            int scanlineWidth = GetBytesPerScanline(imageProps.ColorType, imageProps.BitDepth, imageProps.Width);
            return filterType switch
            {
                0 => (int ptr, int scanlineStart, byte[] bytes) => bytes[ptr],
                1 => (int ptr, int scanlineStart, byte[] bytes) =>
                {
                    byte prev = ptr > scanlineStart + bpp ? bytes[ptr - bpp] : (byte)0;
                    return (byte)((bytes[ptr] + prev) % 256);
                }
                ,
                2 => (int ptr, int scanlineStart, byte[] bytes) =>
                {
                    byte prior = ptr > scanlineWidth ? bytes[ptr - scanlineWidth] : (byte)0;
                    return (byte)((bytes[ptr] + prior) % 256);
                }
                ,
                3 => (int ptr, int scanlineStart, byte[] bytes) =>
                {
                    byte prev = ptr > scanlineStart + bpp ? bytes[ptr - bpp] : (byte)0;
                    byte prior = ptr > scanlineWidth ? bytes[ptr - scanlineWidth] : (byte)0;
                    return (byte)((bytes[ptr] + (prev + prior) / 2.0) % 256);
                }
                ,
                4 => (int ptr, int scanlineStart, byte[] bytes) =>
                {
                    byte left = ptr > scanlineStart + bpp ? bytes[ptr - bpp] : (byte)0;
                    byte up = ptr >= scanlineWidth ? bytes[ptr - scanlineWidth] : (byte)0;
                    byte upLeft = ptr > scanlineStart + bpp && ptr > scanlineWidth ? bytes[ptr - scanlineWidth - bpp] : (byte)0;
                    return (byte)((bytes[ptr] + GetPaethVal(left, up, upLeft)) % 256);
                }
                ,
                _ => throw new Exception($"Unexpected filter type: {filterType}")
            };
        }

        private static List<Rgba> RawIdatBytesToRgbaList(byte[] bytes, IHDRData imageProps, Rgba[] palette)
        {
            int sampleCount = ColorSampleCountMap[imageProps.ColorType];
            List<Rgba> rgbaPixels = [];
            BitPointer ptr = new();

            for (int i = 0; i < imageProps.Width * imageProps.Height; i++)
            {
                ulong rawPixelData = GetNextPixelData(bytes, ptr, imageProps.BitDepth, imageProps.ColorType);
                ulong sampleMask = GetBigEndianMask(imageProps.BitDepth);
                List<ushort> samples = [];
                for (int sampleIdx = 0; sampleIdx < sampleCount; sampleIdx++, sampleMask >>= imageProps.BitDepth)
                {
                    samples.Add((ushort)((rawPixelData & sampleMask) >> (sizeof(ulong) * 8 - (sampleIdx + 1) * imageProps.BitDepth)));
                }
                rgbaPixels.Add(SamplesToRgba(samples, imageProps, palette));
            }
            return rgbaPixels;
        }

        /// <summary>
        /// Packs all following pixel data into a big-endian ulong.
        /// </summary>
        /// <remarks>(Moves ptr to next pixel.)</remarks>
        private static ulong GetNextPixelData(byte[] bytes, BitPointer ptr, int bitDepth, int colorType)
        {
            int sampleCount = ColorSampleCountMap[colorType];
            ulong bits = 0;
            ulong sampleBits;
            for (int i = 0; i < sampleCount; i++, ptr.Offset += bitDepth)
            {
                bits <<= bitDepth;
                if (bitDepth <= 8)
                    sampleBits = (ulong)bytes[ptr.Byte] >> 8 - (ptr.Offset + bitDepth);
                else // bitDepth == 16
                {
                    sampleBits = ((ulong)bytes[ptr.Byte] << 8) & bytes[ptr.Byte + 1];
                }
                bits += sampleBits;
            }
            return bits << (sizeof(ulong) * sizeof(byte) - bitDepth * sampleCount);
        }

        private static Rgba SamplesToRgba(List<ushort> samples, IHDRData imageProps, Rgba[] palette)
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

    public class BitPointer
    {
        private int _bit = 0;
        public int Byte
        {
            get => _bit / 8;
            set => _bit = value * 8 + Offset;
        }
        public int Offset
        {
            get => _bit % 8;
            set => _bit = Byte * 8 + value;
        }
    }
}

/*
unfilter indexing

width = 4, height = 3
X0000
X0000
X0000
scanlineByteLength * i
*/