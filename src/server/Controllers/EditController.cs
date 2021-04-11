using Convert;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace server.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class EditController : ControllerBase
    {
        private ILogger<EditController> _logger;
        private static List<FileRecord> fileRecords;
        private static Convert.Converter converter;

        public EditController(ILogger<EditController> logger)
        {
            _logger = logger;
        }

        [HttpPost]
        public Edit Post(Edit edit)
        {
            string jsonPath = @"../../data/data.json";
            if (fileRecords == null)
            {
                fileRecords = JsonConvert.DeserializeObject<List<FileRecord>>(System.IO.File.ReadAllText(jsonPath));
            }
            if (converter == null)
            {
                converter = new Convert.Converter(fileRecords);
            }
            int id = edit.Id;
            string key = edit.Key;
            string value = edit.Value;
            if (fileRecords[id].Payload.ContainsKey(key))
            {
                fileRecords[id].Payload.Remove(key);
            }
            fileRecords[id].Payload.Add(key, value);
            System.IO.File.WriteAllText(jsonPath, JsonConvert.SerializeObject(fileRecords, Formatting.Indented));
            Console.WriteLine("Building " + EditController.fileRecords[id].Filename);
            converter.ConvertPost(id);
            return edit;
        }
    }
}
