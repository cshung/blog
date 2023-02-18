namespace index
{
    using System;
    using System.Collections.Generic;

    class Index
    {
        // A map from a term to its id
        private Dictionary<string, int> terms;
        // A map from a term id to a list of (document id, term frequency) pairs
        private Dictionary<int, List<Tuple<int, int>>> invertedIndex;
        private string[] documents;
        private double[] documentLength;
        private BkTree correction;

        public Index(Dictionary<string, int> terms, Dictionary<int, List<Tuple<int, int>>> invertedIndex, string[] documents, double[] documentLength, BkTree correction)
        {
            this.terms = terms;
            this.invertedIndex = invertedIndex;
            this.documents = documents;
            this.documentLength = documentLength;
            this.correction = correction;
        }

        public Dictionary<string, int> Terms { get { return this.terms; } }
        public Dictionary<int, List<Tuple<int, int>>> InvertedIndex { get { return this.invertedIndex; } }
        public string[] Documents { get { return this.documents; } }
        public double[] DocumentLength { get { return this.documentLength; } }
        public BkTree Correction { get { return this.correction; } }
    }
}
