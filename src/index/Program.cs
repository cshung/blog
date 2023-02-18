namespace index
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Text;
    using Newtonsoft.Json;

    public class JavaScriptIndex
    {
        public Dictionary<string, List<int>> Terms { get; set; }
        public string[] Documents { get; set; }
        public int[] DocumentLength { get; set; }
    }

    internal static class Program
    {
        private static void Main(string[] args)
        {
            string[] documents = Directory.GetFiles(@"../../content/posts", "*.md");
            Index index = Index(documents);
            JavaScriptIndex jsIndex = new JavaScriptIndex();
            jsIndex.Terms = new Dictionary<string, List<int>>();
            foreach (string term in index.Terms.Keys)
            {
                List<int> values = new List<int>();
                List<Tuple<int, int>> hits = index.InvertedIndex[index.Terms[term]];
                foreach (Tuple<int, int> hit in hits)
                {
                    values.Add(hit.Item1);
                    values.Add(hit.Item2);
                }
                jsIndex.Terms.Add(term, values);
            }
            jsIndex.Documents = documents;
            jsIndex.DocumentLength = index.DocumentLength.Select(d => (int)(d * d)).ToArray();
            System.IO.File.WriteAllText("index.js", "var index = " + JsonConvert.SerializeObject(jsIndex));
            /*
            while (true)
            {
                Console.WriteLine("Please enter the query");
                string query = Console.ReadLine();
                foreach (var result in Query(index, query))
                {
                    string filename = result.Item1;
                    string url = filename.Replace(".md","").Replace("../../content/", "http://localhost:1313/");
                    Console.WriteLine("{0} {1}", url, result.Item2);
                }
            }
            */
        }

        private static Index Index(string[] documents)
        {
            // TODO: Implement inverse document frequency
            var terms = new Dictionary<string, int>();
            var invertedIndex = new Dictionary<int, List<Tuple<int, int>>>();
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
                        invertedIndex.Add(termId, new List<Tuple<int, int>>());
                    }
                    invertedIndex[termId].Add(Tuple.Create(documentId, frequency));
                    lengthSquared = lengthSquared + frequency * frequency;
                }
                documentLength[documentId] = Math.Sqrt(lengthSquared);
            }
            var correction = new BkTree();
            foreach (var term in terms.Keys)
            {
                correction.Insert(term);
            }
            return new Index(terms, invertedIndex, documents, documentLength, correction);
        }

        private static IEnumerable<Tuple<string, double>> Query(Index index, string query)
        {
            Dictionary<string, int> terms = index.Terms;
            Dictionary<int, List<Tuple<int, int>>> invertedIndex = index.InvertedIndex;
            string[] documents = index.Documents;
            double[] documentLength = index.DocumentLength;
            BkTree correction = index.Correction;

            var queryTermFrequencies = new Dictionary<string, int>();
            foreach (var queryTerm in query.Split(' ', StringSplitOptions.RemoveEmptyEntries).Select(t => t.ToLower()))
            {
                var corrections = correction.Query(queryTerm, 1);
                string correctedQueryTerm = corrections.FirstOrDefault();
                if (correctedQueryTerm != null)
                {
                    Console.Write(correctedQueryTerm);
                    Console.Write(" ");
                    int queryTermFrequency = 0;
                    if (!queryTermFrequencies.TryGetValue(correctedQueryTerm, out queryTermFrequency))
                    {
                        queryTermFrequencies.Add(correctedQueryTerm, 1);
                    }
                    else
                    {
                        queryTermFrequencies.Remove(correctedQueryTerm);
                        queryTermFrequencies.Add(correctedQueryTerm, queryTermFrequency + 1);
                    }
                }
            }
            Console.WriteLine();
            var documentNumerators = new Dictionary<int, int>();
            double queryLengthSquared = 0;
            foreach (var queryTermFrequency in queryTermFrequencies)
            {
                string queryTerm = queryTermFrequency.Key;
                int queryFrequency = queryTermFrequency.Value;
                int termId = terms[queryTerm];
                foreach (var hit in invertedIndex[termId])
                {
                    int documentId = hit.Item1;
                    int termFrequency = hit.Item2;
                    int n = 0;
                    if (!documentNumerators.TryGetValue(documentId, out n))
                    {
                        documentNumerators.Add(documentId, termFrequency * queryFrequency);
                    }
                    else
                    {
                        documentNumerators.Remove(documentId);
                        documentNumerators.Add(documentId, n + termFrequency * queryFrequency);
                    }
                }
                queryLengthSquared = queryLengthSquared + queryFrequency * queryFrequency;
            }
            var rankedDocuments = new List<Tuple<double, int>>();
            foreach (var documentNumerator in documentNumerators)
            {
                int documentId = documentNumerator.Key;
                double documentScore = documentNumerator.Value / documentLength[documentId] / Math.Sqrt(queryLengthSquared);
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
