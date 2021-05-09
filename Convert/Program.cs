namespace Convert
{
    using HtmlAgilityPack;
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Text;
    using System.Xml.Linq;

    class Program
    {
        static void Main(string[] args)
        {
            string ns = "http://www.w3.org/2005/Atom";
            XDocument doc = XDocument.Load(@"C:\dev\blog\Convert\blog.xml");
            XElement root = doc.Root;
            HashSet<string> names = new HashSet<string>();
            string[] paths = Directory.GetFiles(@"C:\dev\Competition\Competition", "*.cpp");
            Dictionary<string, string[]> pathTokensMap = new Dictionary<string, string[]>();
            foreach (var path in paths)
            {
                string rawFilename = Path.GetFileNameWithoutExtension(path);
                StringBuilder filenameBuilder = new StringBuilder();
                for (int i = 0; i < rawFilename.Length; i++)
                {
                    if (i > 0 && char.IsDigit(rawFilename[i]) && !char.IsDigit(rawFilename[i - 1]))
                    {
                        filenameBuilder.Append("_");
                    }
                    filenameBuilder.Append(char.ToLower(rawFilename[i]));
                }
                string filename = filenameBuilder.ToString();
                string[] tokens = filename.Split('_', StringSplitOptions.RemoveEmptyEntries);
                pathTokensMap.Add(path, tokens);
            }
            foreach (var element in root.Elements(XName.Get("entry", ns)))
            {
                bool post = false;
                foreach (var category in element.Elements(XName.Get("category", ns)))
                {
                    string scheme = category.Attribute("scheme").Value;
                    if (scheme.Equals("http://schemas.google.com/g/2005#kind"))
                    {
                        string term = category.Attribute("term").Value;
                        if (term.Equals("http://schemas.google.com/blogger/2008/kind#post"))
                        {
                            post = true;
                        }
                    }
                }
                if (post)
                {
                    string title = element.Element(XName.Get("title", ns)).Value.Replace("\"", "").Trim();
                    string content = element.Element(XName.Get("content", ns)).Value;
                    string date = element.Element(XName.Get("published", ns)).Value;
                    string preambleTemplate = @"---
title: ""{0}""
date: {1}
draft: false
---

";
                    string preamble = string.Format(preambleTemplate, title, date);
                    HtmlDocument html = new HtmlDocument();
                    html.LoadHtml(content);
                    IEnumerable<HtmlNode> children = html.DocumentNode.ChildNodes;
                    StringBuilder sb = new StringBuilder();
                    try
                    {
                        sb.Append(preamble);
                        hasCode = false;
                        ConvertChildNodes(html.DocumentNode, sb);
                        string filename = string.Join("", title.Where(c => char.IsDigit(c) || char.IsLetter(c) || c == '-' || c == ' ')).Replace(' ', '-').ToLower();
                        while (filename.Contains("--"))
                        {
                            filename = filename.Replace("--", "-");
                        }
                        if (filename.EndsWith("-"))
                        {
                            filename = filename.Substring(0, filename.Length - 1);
                        }
                        if (filename.StartsWith("-"))
                        {
                            filename = filename.Substring(1, filename.Length - 1);
                        }
                        filename = filename + ".md";
                        string markdown = sb.ToString();
                        while (markdown.Contains("\n\n\n"))
                        {
                            markdown = markdown.Replace("\n\n\n", "\n\n");
                        }
                        sb.Clear();
                        bool inequation = false;
                        foreach (var c in markdown)
                        {
                            if (c == '$')
                            {
                                if (inequation)
                                {
                                    sb.Append("\\\\)");
                                    inequation = false;
                                }
                                else
                                {
                                    sb.Append("\\\\(");
                                    inequation = true;
                                }
                            }
                            else
                            {
                                sb.Append(c);
                            }
                        }
                        markdown = sb.ToString();

                        // My blog specific - some of the blog entries has a pile of code pasted in it, they are better presented as a gist

                        markdown = markdown.Replace("**Solution**:", "**Solution:**");
                        markdown = markdown.Replace("**Problem**:", "**Problem:**");
                        markdown = markdown.Replace("**Code**:", "**Code:**");
                        markdown = markdown.Replace("**Solution**", "**Solution:**");
                        markdown = markdown.Replace("**Problem**", "**Problem:**");
                        markdown = markdown.Replace("**Code**", "**Code:**");

                        string code = "**Code:**";
                        if (markdown.IndexOf(code) != -1 && !hasCode)
                        {
                            markdown = markdown.Substring(0, markdown.IndexOf(code) + code.Length);
                            string[] titleTokens = title.ToLower().Split(' ', StringSplitOptions.RemoveEmptyEntries);
                            double bestScore = -1;
                            string bestPath = null;
                            foreach (var kvp in pathTokensMap)
                            {
                                string[] pathTokens = kvp.Value;
                                int count = 0;
                                foreach (var pathToken in pathTokens)
                                {
                                    foreach (var titleToken in titleTokens)
                                    {
                                        if (titleToken.Equals(pathToken))
                                        {
                                            count++;
                                        }
                                    }
                                }
                                double score = (count + 0.0) / pathTokens.Length;
                                if (score > bestScore)
                                {
                                    bestScore = score;
                                    bestPath = kvp.Key;
                                }
                            }
                            // TODO: The trick is right most of the time, but it fails sometimes.
                            // We need a mechanism for reviewing to automatically converted files
                            // Console.WriteLine(title + " -> " + bestPath);
                            string githubLink = @"https://github.com/cshung/Competition/blob/main/Competition/" + Path.GetFileName(bestPath);
                            string append = string.Format("\n\n{{{{<github \"{0}\">}}}}", githubLink);
                            markdown = markdown + append;
                        }
                    }
                    catch (Exception ex)
                    {
                        // TODO, what is the case 
                        Console.WriteLine("Failed " + title + " because the page has a " + ex.Message + " tag.");
                    }
                }
            }
            foreach (var name in names)
            {
                Console.WriteLine(name);
            }
        }

        private static bool hasCode;

        private static void Convert(HtmlNode node, StringBuilder sb)
        {
            if (node.Name == "p" || node.Name == "div" || node.Name == "ol" || node.Name == "ul" || node.Name == "li")
            {
                ConvertChildNodes(node, sb);
                sb.Append("\n\n");
            }
            else if (node.Name == "i")
            {
                StringBuilder inner = new StringBuilder();
                ConvertChildNodes(node, inner);
                string innerText = inner.ToString().Trim();
                if (innerText.Length > 0)
                {
                    sb.Append("*");
                    sb.Append(innerText);
                    sb.Append("*");
                }
            }
            else if (node.Name == "strike")
            {
                StringBuilder inner = new StringBuilder();
                ConvertChildNodes(node, inner);
                string innerText = inner.ToString().Trim();
                if (innerText.Length > 0)
                {
                    sb.Append("~~");
                    sb.Append(innerText);
                    sb.Append("~~");
                }
            }
            else if (node.Name == "b")
            {
                StringBuilder inner = new StringBuilder();
                ConvertChildNodes(node, inner);
                string innerText = inner.ToString().Trim();
                if (innerText.Length > 0)
                {
                    sb.Append("**");
                    sb.Append(innerText);
                    sb.Append("**");
                }
            }
            else if (node.Name == "#text")
            {
                sb.Append(HtmlEntity.DeEntitize(node.InnerText));
            }
            else if (node.Name == "a")
            {
                sb.Append("[");
                sb.Append(node.InnerText);
                sb.Append("]");
                sb.Append("(");
                sb.Append(node.Attributes["href"].Value);
                sb.Append(")");
            }
            else if (node.Name == "br")
            {
                sb.Append("\n\n");
            }
            else if (node.Name == "script")
            {
                string scriptSource = node.Attributes["src"].Value;
                if (scriptSource.StartsWith("https://gist-it.appspot.com/"))
                {
                    string append = string.Format("{{{{<github \"{0}\">}}}}", scriptSource.Substring("https://gist-it.appspot.com/".Length));
                    sb.Append(append);
                    hasCode = true;
                }
            }
            else if (node.Name == "pre" || node.Name == "span")
            {
                ConvertChildNodes(node, sb);
            }
            else
            {
                throw new Exception(node.Name);
            }
        }

        private static void ConvertChildNodes(HtmlNode node, StringBuilder sb)
        {
            foreach (var child in node.ChildNodes)
            {
                Convert(child, sb);
            }
        }
    }
}
