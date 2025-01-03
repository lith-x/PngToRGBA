using PngToFF;

namespace PngToFFTests;

public class MinimalTests
{
    [Theory]
    [InlineData("../../../tinypng.png", "../../../tinypng.ff")]
    [InlineData("../../../img.png", "../../../img.ff")]
    public void PngFFEquivalence(string pngPath, string ffPath)
    {
        Rgba[][] pngRgba = PngFile.ProcessFile(pngPath);
        Rgba[][] ffRgba = Program.FarbFeldToRgba(ffPath);
        Assert.Equal(ffRgba, pngRgba);
    }
}
