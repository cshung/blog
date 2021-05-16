namespace index
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Text;

    internal static class Program
    {
        private static void Main(string[] args)
        {
            var terms = new Dictionary<string, int>();
            var index = new Dictionary<int, List<Tuple<int, int>>>();
            string[] documents = Directory.GetFiles(@"c:\dev\blog\content\posts", "*.md");
            var documentLength = new double[documents.Length];
            for (int documentId = 0; documentId < documents.Length; documentId++)
            {
                string path = documents[documentId];
                string content = File.ReadAllText(path);
                string[] documentTerms = ParseContent(content);
                // TODO: Stemming
                var termFrequencies = new Dictionary<string, int>();
                foreach (string term in documentTerms)
                {
                    int frequency = 0;
                    if (!termFrequencies.TryGetValue(term, out frequency))
                    {
                        termFrequencies.Add(term, 1);
                    }
                    else
                    {
                        termFrequencies.Remove(term);
                        termFrequencies.Add(term, frequency + 1);
                    }
                }
                double lengthSquared = 0;
                foreach (var termFrequency in termFrequencies)
                {
                    string term = termFrequency.Key;
                    int frequency = termFrequency.Value;
                    int termId = 0;
                    if (!terms.TryGetValue(term, out termId))
                    {
                        termId = terms.Count;
                        terms.Add(term, termId);
                        index.Add(termId, new List<Tuple<int, int>>());
                    }
                    index[termId].Add(Tuple.Create(documentId, frequency));
                    lengthSquared = lengthSquared + frequency * frequency;
                }
                documentLength[documentId] = Math.Sqrt(lengthSquared);
            }
            BkTree correction = new BkTree();
            foreach (var term in terms.Keys)
            {
                correction.Insert(term);
            }
            string query = Console.ReadLine();
            foreach (var result in Query(terms, index, documents, documentLength, correction, query))
            {
                Console.WriteLine("{0} {1}", result.Item1, result.Item2);
            }
        }

        // TODO: We should have an index abstraction that group these things together
        private static IEnumerable<Tuple<string, double>> Query(Dictionary<string, int> terms, Dictionary<int, List<Tuple<int, int>>> index, string[] documents, double[] documentLength, BkTree correction, string query)
        {
            var queryTerms = new List<string>();
            foreach (var queryTerm in query.Split(' ', StringSplitOptions.RemoveEmptyEntries).Select(t => t.ToLower()))
            {
                var corrections = correction.Query(queryTerm, 1);
                string correctedQueryTerm = corrections.FirstOrDefault();
                if (correctedQueryTerm != null)
                {
                    queryTerms.Add(correctedQueryTerm);
                }
            }
            var documentNumerators = new Dictionary<int, int>();
            foreach (var queryTerm in queryTerms)
            {
                int termId = terms[queryTerm];
                foreach (var hit in index[termId])
                {
                    int documentId = hit.Item1;
                    int termFrequency = hit.Item2;
                    int n = 0;
                    if (!documentNumerators.TryGetValue(documentId, out n))
                    {
                        documentNumerators.Add(documentId, termFrequency);
                    }
                    else
                    {
                        documentNumerators.Remove(documentId);
                        documentNumerators.Add(documentId, n + termFrequency);
                    }
                }
            }
            var rankedDocuments = new List<Tuple<double, int>>();
            foreach (var documentNumerator in documentNumerators)
            {
                int documentId = documentNumerator.Key;
                double documentScore = documentNumerator.Value / documentLength[documentId];
                rankedDocuments.Add(Tuple.Create(documentScore, documentId));
            }
            foreach (var rankedDocument in rankedDocuments.OrderBy(t => -t.Item1))
            {
                yield return Tuple.Create(documents[rankedDocument.Item2], rankedDocument.Item1);
            }
        }

        private static string[] ParseContent(string content)
        {
            // TODO: Improve parsing
            StringBuilder sb = new StringBuilder();
            foreach (var c in content)
            {
                if (char.IsLetter(c))
                {
                    sb.Append(c);
                }
                else
                {
                    sb.Append(' ');
                }
            }
            return sb.ToString().Split(' ', StringSplitOptions.RemoveEmptyEntries).Select(t => t.ToLower()).ToArray();
        }
    }
}
