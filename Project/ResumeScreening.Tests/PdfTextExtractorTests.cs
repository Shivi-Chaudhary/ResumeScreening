using ResumeScreening.API.Helpers;

namespace ResumeScreening.Tests;

public class PdfTextExtractorTests
{
    [Fact]
    public void TryExtractText_ReturnsNullForInvalidStream()
    {
        // Corrupt / non-PDF stream
        using var stream = new MemoryStream(new byte[] { 0x00, 0x01, 0x02, 0x03 });
        var result = PdfTextExtractor.TryExtractText(stream);

        Assert.Null(result);
    }

    [Fact]
    public void TryExtractText_ReturnsNullForEmptyStream()
    {
        using var stream = new MemoryStream(Array.Empty<byte>());
        var result = PdfTextExtractor.TryExtractText(stream);

        Assert.Null(result);
    }

    [Fact]
    public void TryExtractText_DoesNotThrowOnCorruptData()
    {
        // Should gracefully handle corrupt data without throwing
        var randomBytes = new byte[1024];
        new Random(42).NextBytes(randomBytes);
        using var stream = new MemoryStream(randomBytes);

        var exception = Record.Exception(() => PdfTextExtractor.TryExtractText(stream));

        // Should either return null or not throw — both are acceptable
        Assert.Null(exception);
    }
}
