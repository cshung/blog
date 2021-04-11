namespace index
{
    using System;
    using System.Linq;
    using System.Collections.Generic;

    public class BkTree
    {
        private Node root;

        public void Insert(string word)
        {
            if (this.root == null)
            {
                this.root = new Node(word);
            }
            else
            {
                Node current = this.root;
                while (true)
                {
                    int distance = Levenshtein(word, current.Word);
                    Node next = null;
                    if (current.Children.TryGetValue(distance, out next))
                    {
                        current = next;
                    }
                    else
                    {
                        current.Children.Add(distance, new Node(word));
                        break;
                    }
                }
            }
        }

        public IEnumerable<string> Query(string word, int n)
        {
            if (this.root == null)
            {
                return new string[0];
            }
            else
            {
                return this.Query(this.root, word, n).OrderBy(t => t).Select(t => t.Item2);
            }
        }

        private static int Levenshtein(string str1, string str2)
        {
            int m = str1.Length;
            int n = str2.Length;
            int[,] cost = new int[m + 1, n + 1];
            for (int i = 0; i < m + 1; i++)
            {
                cost[i, 0] = i;
            }

            for (int j = 0; j < n + 1; j++)
            {
                cost[0, j] = j;
            }

            for (int i = 1; i < m + 1; i++)
            {
                for (int j = 1; j < n + 1; j++)
                {
                    char c1 = str1[i - 1];
                    char c2 = str2[j - 1];
                    if (c1 == c2)
                    {
                        cost[i, j] = cost[i - 1, j - 1];
                    }
                    else
                    {
                        int insertion_cost = 1 + cost[i, j - 1];
                        int deletion_cost = 1 + cost[i - 1, j];
                        cost[i, j] = Math.Min(insertion_cost, deletion_cost);
                        int replacement_cost = 1 + cost[i - 1, j - 1];
                        cost[i, j] = Math.Min(cost[i, j], replacement_cost);
                    }
                }
            }

            return cost[m, n];
        }

        private IEnumerable<Tuple<int, string>> Query(Node node, string word, int n)
        {
            int distance = Levenshtein(word, node.Word);
            if (distance <= n)
            {
                yield return Tuple.Create(distance, node.Word);
            }

            for (int i = distance - n; i < distance + n + 1; i++)
            {
                Node children;
                if (node.Children.TryGetValue(i, out children))
                {
                    foreach (Tuple<int, string> result in this.Query(children, word, n))
                    {
                        yield return result;
                    }
                }
            }
        }

        private class Node
        {
            private string word;
            private Dictionary<int, Node> children;

            public Node(string word)
            {
                this.word = word;
                this.children = new Dictionary<int, Node>();
            }

            public string Word
            {
                get
                {
                    return this.word;
                }
            }

            public Dictionary<int, Node> Children
            {
                get
                {
                    return this.children;
                }
            }
        }
    }
}
