using ImageMagick;
using MineOS.Infrastructure.Services;

namespace MineOS.Tests.Unit;

public class ServerIconProcessorTests
{
    [Theory]
    [InlineData(MagickFormat.Png)]
    [InlineData(MagickFormat.Jpeg)]
    [InlineData(MagickFormat.Gif)]
    [InlineData(MagickFormat.Bmp)]
    [InlineData(MagickFormat.WebP)]
    [InlineData(MagickFormat.Tiff)]
    [InlineData(MagickFormat.Ico)]
    public async Task RasterUpload_IsConvertedTo64PixelPng(MagickFormat format)
    {
        using var original = new MagickImage(MagickColors.Red, 96, 48);
        using var input = new MemoryStream(original.ToByteArray(format));
        var png = await new ServerIconProcessor().CreatePngAsync(input, default);
        using var result = new MagickImage(png);
        Assert.Equal(64u, result.Width);
        Assert.Equal(64u, result.Height);
        Assert.Equal(MagickFormat.Png, result.Format);
    }

    [Theory]
    [InlineData("not an image")]
    [InlineData("<svg xmlns='http://www.w3.org/2000/svg'><image href='https://example.test/private'/></svg>")]
    [InlineData("%PDF-1.7")]
    public async Task NonRasterInput_IsRejectedBeforeDecoding(string value)
    {
        using var input = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(value));
        await Assert.ThrowsAsync<ArgumentException>(() => new ServerIconProcessor().CreatePngAsync(input, default));
    }

    [Fact]
    public async Task OversizedUpload_IsRejectedBeforeDecoding()
    {
        using var input = new MemoryStream(new byte[5 * 1024 * 1024 + 1]);
        await Assert.ThrowsAsync<ArgumentException>(() => new ServerIconProcessor().CreatePngAsync(input, default));
    }
}
