using System.Text;
using UglyToad.PdfPig;

namespace ResumeScreening.API.Helpers
{
    public static class PdfTextExtractor
    {
        public static string? TryExtractText(Stream pdfStream)
        {
            try
            {
                pdfStream.Position = 0;
                using var document = PdfDocument.Open(pdfStream, new ParsingOptions { UseLenientParsing = true });
                var sb = new StringBuilder();
                foreach (var page in document.GetPages())
                    sb.AppendLine(string.Join(" ", page.GetWords().Select(w => w.Text)));
                return sb.Length == 0 ? null : sb.ToString();
            }
            catch
            {
                return null;
            }
        }
    }
}
