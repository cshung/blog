using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace server.Controllers
{
    class FileRecord
    {
        public string Filename { get; set; }
        public Dictionary<string, string> Payload { get; set; }
    }

    [ApiController]
    [Route("[controller]")]
    public class EditController : ControllerBase
    {
        private ILogger<EditController> _logger;

        public EditController(ILogger<EditController> logger)
        {
            _logger = logger;
        }

        [HttpPost]
        public Edit Post(Edit edit)
        {
            string jsonPath = @"C:\dev\blog\data\data.json";
            List<FileRecord> fileRecords = Newtonsoft.Json.JsonConvert.DeserializeObject<List<FileRecord>>(System.IO.File.ReadAllText(jsonPath));
            string filename = edit.FileName;
            for (int i = 0; i < fileRecords.Count; i++)
            {
                if (fileRecords[i].Filename.Equals(filename))
                {
                    string key = edit.Key;
                    string value = edit.Value;
                    if (fileRecords[i].Payload.ContainsKey(key))
                    {
                        fileRecords[i].Payload.Remove(key);
                    }
                    fileRecords[i].Payload.Add(key, value);
                    break;
                }
            }
            System.IO.File.WriteAllText(jsonPath, Newtonsoft.Json.JsonConvert.SerializeObject(fileRecords));
            Convert.Program.Main(null);
            return edit;
        }
    }
}
