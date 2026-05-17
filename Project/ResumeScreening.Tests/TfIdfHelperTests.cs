using ResumeScreening.API.Helpers;

namespace ResumeScreening.Tests;

public class TfIdfHelperTests
{
    [Fact]
    public void Tokenise_RemovesStopWords()
    {
        var tokens = TfIdfHelper.Tokenise("the quick brown fox jumps over the lazy dog");

        Assert.DoesNotContain("the", tokens);
        Assert.DoesNotContain("over", tokens);
        Assert.Contains("quick", tokens);
        Assert.Contains("brown", tokens);
        Assert.Contains("fox", tokens);
    }

    [Fact]
    public void Tokenise_ReturnsEmptyForEmptyInput()
    {
        var tokens = TfIdfHelper.Tokenise("");
        Assert.Empty(tokens);
    }

    [Fact]
    public void Tokenise_ReturnsEmptyForNullLike()
    {
        var tokens = TfIdfHelper.Tokenise("   ");
        Assert.Empty(tokens);
    }

    [Fact]
    public void Tokenise_HandlesSpecialCharacters()
    {
        var tokens = TfIdfHelper.Tokenise("C# .NET ASP.NET React.js Node.js");

        // Should find technology tokens
        Assert.True(tokens.Count > 0);
    }

    [Fact]
    public void ExtractKeywords_ReturnsRequestedCount()
    {
        var text = "Python machine learning deep learning neural networks " +
                   "TensorFlow Keras data science natural language processing " +
                   "computer vision regression classification clustering";

        var keywords = TfIdfHelper.ExtractKeywords(text, 5);

        Assert.Equal(5, keywords.Count);
    }

    [Fact]
    public void ExtractKeywords_ReturnsLowercase()
    {
        var text = "Java Spring Boot Microservices Docker Kubernetes";
        var keywords = TfIdfHelper.ExtractKeywords(text, 5);

        foreach (var kw in keywords)
            Assert.Equal(kw.ToLower(), kw);
    }

    [Fact]
    public void ComputeTf_CalculatesCorrectFrequencies()
    {
        var tokens = new List<string> { "java", "java", "python", "java" };
        var tf = TfIdfHelper.ComputeTf(tokens);

        Assert.Equal(0.75, tf["java"], 2);
        Assert.Equal(0.25, tf["python"], 2);
    }

    [Fact]
    public void ComputeTf_HandlesEmptyList()
    {
        var tf = TfIdfHelper.ComputeTf(new List<string>());
        Assert.Empty(tf);
    }

    [Fact]
    public void ExtractKeywords_DoesNotReturnStopWords()
    {
        var text = "the the the and and is is was were been for with this that from";
        var keywords = TfIdfHelper.ExtractKeywords(text, 10);

        Assert.DoesNotContain("the", keywords);
        Assert.DoesNotContain("and", keywords);
        Assert.DoesNotContain("is", keywords);
    }
}
