using System.Text.RegularExpressions;

namespace ResumeScreening.API.Helpers
{
    /// <summary>
    /// Lightweight TF-IDF keyword extraction without external ML runtime dependencies.
    /// Operates on raw text: tokenises, removes stop-words, computes TF-IDF scores,
    /// and returns the top-N keywords.
    /// </summary>
    public static class TfIdfHelper
    {
        // ── Common English stop-words ─────────────────────────────────────────
        private static readonly HashSet<string> StopWords = new(StringComparer.OrdinalIgnoreCase)
        {
            "a","an","the","and","or","but","is","are","was","were","be","been","being",
            "have","has","had","do","does","did","will","would","shall","should","may",
            "might","must","can","could","i","me","my","myself","we","our","ours",
            "ourselves","you","your","yours","yourself","yourselves","he","him","his",
            "himself","she","her","hers","herself","it","its","itself","they","them",
            "their","theirs","themselves","what","which","who","whom","this","that",
            "these","those","am","at","by","for","with","about","against","between",
            "through","during","before","after","above","below","to","from","up","down",
            "in","out","on","off","over","under","again","further","then","once","here",
            "there","when","where","why","how","all","both","each","few","more","most",
            "other","some","such","no","nor","not","only","own","same","so","than",
            "too","very","just","don","t","s","re","ve","ll","d","m","o","ain",
            "aren","couldn","didn","doesn","hadn","hasn","haven","isn","ma","mightn",
            "mustn","needn","shan","shouldn","wasn","weren","won","wouldn",
            "of","if","also","etc","using","used","use","including","include","includes",
            "able","like","well","e","g","ie","eg","vs","via","per","etc"
        };

        /// <summary>
        /// Tokenise text into lowercase word tokens (letters/digits, min-length 2).
        /// </summary>
        public static List<string> Tokenise(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return new List<string>();

            return Regex.Matches(text.ToLowerInvariant(), @"[a-z][a-z0-9#+.]{1,40}")
                .Select(m => m.Value)
                .Where(w => !StopWords.Contains(w))
                .ToList();
        }

        /// <summary>
        /// Compute term-frequency (TF) for a single document's tokens.
        /// TF = count / totalTokens.
        /// </summary>
        public static Dictionary<string, double> ComputeTf(List<string> tokens)
        {
            var counts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            foreach (var t in tokens)
            {
                counts.TryGetValue(t, out var c);
                counts[t] = c + 1;
            }

            var tf = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
            double total = tokens.Count;
            foreach (var (word, count) in counts)
                tf[word] = count / total;

            return tf;
        }

        /// <summary>
        /// Extract top-N keywords from a single document using TF scoring.
        /// When only one document is available (like a JD), IDF is omitted — 
        /// TF alone is sufficient to rank keywords by importance within that document.
        /// </summary>
        public static List<string> ExtractKeywords(string text, int topN = 30)
        {
            var tokens = Tokenise(text);
            if (tokens.Count == 0)
                return new List<string>();

            var tf = ComputeTf(tokens);
            return tf.OrderByDescending(kvp => kvp.Value)
                     .Take(topN)
                     .Select(kvp => kvp.Key)
                     .ToList();
        }

        /// <summary>
        /// Compute TF-IDF scores across a corpus of documents and return per-document keyword vectors.
        /// </summary>
        /// <param name="documents">Each string is one document (e.g. JD + all resume texts).</param>
        /// <param name="topN">Number of keywords to keep per document.</param>
        public static List<Dictionary<string, double>> ComputeTfIdf(List<string> documents, int topN = 30)
        {
            if (documents.Count == 0)
                return new List<Dictionary<string, double>>();

            var allTokens = documents.Select(Tokenise).ToList();
            var allTfs = allTokens.Select(ComputeTf).ToList();

            // Document frequency (DF): how many documents contain each term
            var df = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            foreach (var tfs in allTfs)
            {
                foreach (var word in tfs.Keys)
                {
                    df.TryGetValue(word, out var d);
                    df[word] = d + 1;
                }
            }

            int n = documents.Count;
            var results = new List<Dictionary<string, double>>();

            foreach (var tf in allTfs)
            {
                var tfidf = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
                foreach (var (word, tfScore) in tf)
                {
                    var idf = Math.Log((double)n / (1 + df.GetValueOrDefault(word, 0)));
                    tfidf[word] = tfScore * idf;
                }

                // Keep only top-N by TF-IDF score
                var topEntries = tfidf.OrderByDescending(kvp => kvp.Value)
                                      .Take(topN)
                                      .ToDictionary(kvp => kvp.Key, kvp => kvp.Value, StringComparer.OrdinalIgnoreCase);
                results.Add(topEntries);
            }

            return results;
        }
    }
}
