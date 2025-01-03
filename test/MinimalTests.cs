using PngToFF;

namespace PngToFFTests;

public class MinimalTests
{
    [Theory]
    [InlineData("../../../../resources/tinypng.png", "../../../../resources/tinypng.ff")]
    [InlineData("../../../../resources/img.png", "../../../../resources/img.ff")]
    public void PngFFEquivalence(string pngPath, string ffPath)
    {
        Rgba[][] pngRgba = PngFile.ProcessFile(pngPath);
        Rgba[][] ffRgba = Program.FarbFeldToRgba(ffPath);
        Assert.Equal(ffRgba, pngRgba);
    }
}
