namespace Convert
{
    using Newtonsoft.Json;
    using HtmlAgilityPack;
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Text;
    using System.Xml.Linq;

    public class FileRecord
    {
        public string Filename { get; set; }
        public Dictionary<string, string> Payload { get; set; }
    }

    public class Post
    {
        public int Id { get; set; }
        public string Title { get; set; }
        public string Content { get; set; }
        public string Date { get; set; }
        public string FileName { get; set; }
        public string NextUrl { get; set; }
        public List<string> Tags { get; set; }
    }

    public class Converter
    {
        private const string ns = "http://www.w3.org/2005/Atom";
        private const string xmlPath = @"../../data/blog.xml";
        private const string jsonPath = @"../../data/data.json";

        private const bool generateFinal = true;

        private List<Post> posts;
        private List<FileRecord> fileRecords;
        private Dictionary<string, string[]> pathTokensMap;
        private Dictionary<string, string> problemLinkToSourceMap;
        private Dictionary<string, string> linkToPostMap;

        public Converter(List<FileRecord> fileRecords)
        {
            this.fileRecords = fileRecords;
            LoadPosts();
            IndexCompetitionFiles();
        }

        private void LoadPosts()
        {
            this.linkToPostMap = new Dictionary<string, string>();
            this.posts = new List<Post>();
            XDocument doc = XDocument.Load(xmlPath);
            XElement root = doc.Root;
            int index = 0;
            
            foreach (var element in root.Elements(XName.Get("entry", ns)))
            {
                List<string> tags = new List<string>();
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
                    } else if (scheme.Equals("http://www.blogger.com/atom/ns#"))
                    {
                        string term = category.Attribute("term").Value;
                        tags.Add(term);
                    }
                }
                if (post)
                {
                    string title = element.Element(XName.Get("title", ns)).Value.Replace("\"", "").Replace("LeetCode OJ", "LeetCode").Trim();
                    string content = element.Element(XName.Get("content", ns)).Value;
                    string date = element.Element(XName.Get("published", ns)).Value;
                    string fileName = NormalizeFileName(title);
                    string link = null;
                    try {
                        link = element.Elements(XName.Get("link", ns)).Single(e => e.Attribute("rel").Value.Equals("alternate")).Attribute("href").Value;
                    } catch (Exception) {
                        // This happens for draft posts, and that's okay
                    }
                    if (link != null)
                    {
                        link = link.Replace("https", "http");
                        this.linkToPostMap.Add(link, "../" + fileName.Replace(".md",""));
                    }
                    this.posts.Add(new Post
                    {
                        Id = index++,
                        Title = title,
                        Content = content,
                        Date = date,
                        FileName = fileName,
                        Tags = tags,
                    });
                }
                
            }
            int first = -1;
            int last = -1;
            for (int i = 0; i < index; i++)
            {
                if (ShouldGenerate(i))
                {
                    if (first == -1)
                    {
                        Console.WriteLine("first = " + i);
                        first = last = i;
                    }
                    else
                    {
                        Console.WriteLine("set last " + last + " to " + this.posts[i].FileName.Replace(".md",""));
                        this.posts[last].NextUrl = "../" + this.posts[i].FileName.Replace(".md","");
                        last = i;
                    }
                }
            }
            Console.WriteLine("set last " + last + " to " + this.posts[first].FileName.Replace(".md",""));
            this.posts[last].NextUrl = "../" + this.posts[first].FileName.Replace(".md","");
        }

        private bool ShouldGenerate(int i)
        {
            string comment;
            if (this.fileRecords[i].Payload.TryGetValue("comment", out comment) && comment.Equals("LGTM"))
            {
                return generateFinal;
            } else {
                // TODO, additional filtering
                return !generateFinal && (this.fileRecords[i].Filename.IndexOf("osalind") != -1);
            }
        }

        private void IndexCompetitionFiles()
        {
            string[] paths = Directory.GetFiles(@"../../../Competition/Competition", "*.cpp");
            this.pathTokensMap = new Dictionary<string, string[]>();
            this.problemLinkToSourceMap = new Dictionary<string, string>();
            foreach (var path in paths)
            {
                string[] lines = File.ReadAllLines(path);
                foreach (string line in lines)
                {
                    if (line.StartsWith("// http"))
                    {
                        string link = line.Substring(3);
                        if (!problemLinkToSourceMap.ContainsKey(link))
                        {
                            problemLinkToSourceMap.Add(link, path);
                        }
                        break;
                    }
                }
                string rawFilename = Path.GetFileNameWithoutExtension(path);
                StringBuilder fileNameBuilder = new StringBuilder();
                for (int i = 0; i < rawFilename.Length; i++)
                {
                    if (i > 0 && char.IsDigit(rawFilename[i]) && !char.IsDigit(rawFilename[i - 1]))
                    {
                        fileNameBuilder.Append("_");
                    }
                    fileNameBuilder.Append(char.ToLower(rawFilename[i]));
                }
                string fileName = fileNameBuilder.ToString();
                string[] tokens = fileName.Split('_', StringSplitOptions.RemoveEmptyEntries);
                pathTokensMap.Add(path, tokens);
            }
        }

        public void GenerateJson()
        {
            this.fileRecords = new List<FileRecord>();
            foreach (var post in this.posts)
            {
                FileRecord record = new FileRecord
                {
                    Filename = post.FileName,
                    Payload = new Dictionary<string, string>()
                };
                fileRecords.Add(record);
            }
            File.WriteAllText(jsonPath, JsonConvert.SerializeObject(fileRecords, Formatting.Indented));
        }

        public void ConvertAll()
        {
            for (int i = 0; i < this.posts.Count; i++)
            {
                if (ShouldGenerate(i))
                {
                    ConvertPost(i);
                }
            }
        }

        public void ConvertPost(int id)
        {
            Post post = this.posts[id];
            try
            {
                string fileName = post.FileName;
                Dictionary<string, string> info = this.fileRecords.Single(r => r.Filename.Equals(fileName)).Payload;
                string markdown = new Conversion(this.pathTokensMap, this.problemLinkToSourceMap, this.linkToPostMap, post, info).ConvertToMarkDown();
                File.WriteAllText(@"../../content/posts/" + fileName, markdown);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Failed " + post.Title + " because the page has a " + ex.Message + " tag.");
            }
        }

        private static string NormalizeFileName(string title)
        {
            string fileName = string.Join("", title.Where(c => char.IsDigit(c) || char.IsLetter(c) || c == '-' || c == ' ')).Replace(' ', '-').ToLower();
            while (fileName.Contains("--"))
            {
                fileName = fileName.Replace("--", "-");
            }
            if (fileName.EndsWith("-"))
            {
                fileName = fileName.Substring(0, fileName.Length - 1);
            }
            if (fileName.StartsWith("-"))
            {
                fileName = fileName.Substring(1, fileName.Length - 1);
            }
            fileName = fileName + ".md";
            return fileName;
        }
    
        public static void Main(string[] args)
        {
            List<FileRecord> fileRecords = JsonConvert.DeserializeObject<List<FileRecord>>(File.ReadAllText(jsonPath));
            Converter converter = new Converter(fileRecords);
            // TODO: Switch here is reset Json
            // converter.GenerateJson();
            converter.ConvertAll();
        }
    }

    public class Conversion
    {
        private Dictionary<string, string[]> pathTokensMap;
        private Dictionary<string, string> problemLinkToSourceMap;
        private Dictionary<string, string> linkToPostMap;
        private int id;
        private string title;
        private string content;
        private string date;
        private string fileName;
        private string nextUrl;
        private List<string> tags;
        private Dictionary<string, string> info;

        private List<string> links;
        private bool hasCode;

        public Conversion(
            Dictionary<string, string[]> pathTokensMap, 
            Dictionary<string, string> problemLinkToSourceMap, 
            Dictionary<string, string> linkToPostMap,
            Post post, Dictionary<string, string> info)
        {
            this.pathTokensMap = pathTokensMap;
            this.problemLinkToSourceMap = problemLinkToSourceMap;
            this.linkToPostMap = linkToPostMap;
            this.id = post.Id;
            this.title = post.Title;
            this.content = post.Content;
            this.date = post.Date;
            this.tags = post.Tags;
            this.info = info;
            this.fileName = post.FileName;
            this.nextUrl = post.NextUrl;
            this.links = new List<string>();
        }
        public string ConvertToMarkDown()
        {
            string preambleTemplate = @"---
title: ""{0}""
date: {1}
draft: false
tags: [{2}]
---

";
            string preamble = string.Format(preambleTemplate, title, date, string.Join(",", tags));
            HtmlDocument html = new HtmlDocument();
            html.LoadHtml(content);
            IEnumerable<HtmlNode> children = html.DocumentNode.ChildNodes;
            StringBuilder sb = new StringBuilder();
            sb.Append(preamble);

            string comment = null;
            if (!info.TryGetValue("comment", out comment) || !comment.Equals("LGTM"))
            {
                if (comment == null)
                {
                    comment = "LGTM";
                }
                string review = @"

{{{{<review-form ""{0}"" ""{1}"" ""comment"" ""{2}"">}}}}

";
                sb.AppendFormat(review, id, comment, nextUrl);
            }

            hasCode = false;
            ConvertChildNodes(html.DocumentNode, sb);
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
                string codePath;
                bool knownGood = true;
                if (!info.TryGetValue("codepath", out codePath))
                {
                    knownGood = false;
                    if (links.Count != 1 || !this.problemLinkToSourceMap.TryGetValue(this.links[0], out codePath))
                    {
                        codePath = GetCodePath(pathTokensMap, title);
                    }
                    // This generalize to the concept of 
                    // error report, where we can flag suspicious posts
                    if (links.Count == 0)
                    {
                        Console.WriteLine("Wow! " + fileName);
                    }
                }
                string githubLink = @"https://raw.githubusercontent.com/cshung/Competition/main/Competition/" + Path.GetFileName(codePath);
                string append = string.Format("\n\n{{{{<github \"{0}\">}}}}", githubLink);
                string validate = knownGood ? "" : "\n\n{{<validate-form " + id + " \"" + codePath + "\" \"codepath\">}}\n\n";
                markdown = markdown + validate + append;
            }

            return markdown;
        }

        private string GetCodePath(Dictionary<string, string[]> pathTokensMap, string title)
        {
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

            return bestPath;
        }

        private void Convert(HtmlNode node, StringBuilder sb)
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
                string link = node.Attributes["href"].Value;
                this.links.Add(link);
                if (link.IndexOf("andrew-algorithm") != -1)
                {
                    string post = null;
                    if (this.linkToPostMap.TryGetValue(link.Replace("https", "http"), out post)) {
                        link = post;
                    } else {
                        // TODO: There is only one missed link, which is easy to deal with
                        Console.WriteLine("Miss");
                    }
                }
                if (link.EndsWith(".png"))
                {
                    sb.Append("!");
                }
                sb.Append("[");
                sb.Append(node.InnerText);
                sb.Append("]");
                sb.Append("(");
                sb.Append(link);
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
                    string link = scriptSource.Substring("https://gist-it.appspot.com/".Length);
                    link = link.Replace("github.com", "raw.githubusercontent.com");
                    link = link.Replace("blob/", "");
                    string append = string.Format("{{{{<github \"{0}\">}}}}", link);
                    sb.Append(append);
                    hasCode = true;
                }
                else
                {
                    throw new Exception(node.Name);
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

        private void ConvertChildNodes(HtmlNode node, StringBuilder sb)
        {
            foreach (var child in node.ChildNodes)
            {
                Convert(child, sb);
            }
        }
    }
}
