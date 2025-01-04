using PngToFF;

namespace PngToFFTests;

public class MinimalTests
{
    [Theory]
    [InlineData("../../../../resources/tinypng.png", "../../../../resources/tinypng.ff")]
    [InlineData("../../../../resources/img.png", "../../../../resources/img.ff")]
    [InlineData("../../../../resources/avocado8.png", "../../../../resources/avocado8.ff")]
    [InlineData("../../../../resources/avocado16.png", "../../../../resources/avocado16.ff")]
    public void PngFFEquivalence(string pngPath, string ffPath)
    {
        Rgba[][] pngRgba = PngFile.ProcessFile(pngPath);
        Rgba[][] ffRgba = Program.FarbFeldToRgba(ffPath);
        Assert.Equal(ffRgba, pngRgba);
    }
}
