namespace edit
{
    using System;
    using System.Collections.Generic;
    using System.IO;

    class FileRecord
    {
        public string Filename { get; set; }
        public Dictionary<string, string> Payload { get; set; }
    }

    class Program
    {
        static void Main(string[] args)
        {
            string jsonPath = @"C:\dev\blog\data\data.json";
            List<FileRecord> fileRecords = Newtonsoft.Json.JsonConvert.DeserializeObject<List<FileRecord>>(File.ReadAllText(jsonPath));
            while (true)
            {
                Console.Write("filename : ");
                string filename = Console.ReadLine();
                if (filename.Equals(""))
                {
                    break;
                }
                for (int i = 0; i < fileRecords.Count; i++)
                {
                    if (fileRecords[i].Filename.Equals(filename))
                    {
                        foreach (var kvp in fileRecords[i].Payload)
                        {
                            Console.WriteLine($"{kvp.Key} -> {kvp.Value}");
                        }
                        Console.Write("key      : ");
                        string key = Console.ReadLine();
                        Console.Write("value    : ");
                        string value = Console.ReadLine();
                        if (fileRecords[i].Payload.ContainsKey(key))
                        {
                            fileRecords[i].Payload.Remove(key);
                        }
                        fileRecords[i].Payload.Add(key, value);
                        break;
                    }
                }
            }
            File.WriteAllText(jsonPath, Newtonsoft.Json.JsonConvert.SerializeObject(fileRecords));
        }
    }
}
