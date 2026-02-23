using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DeepSeekChat.Models
{
    // DeepSeekSettings.cs
    public class DeepSeekSettings
    {
        public string ApiKey { get; set; } = string.Empty;
        public string ApiBaseUrl { get; set; } = string.Empty;
        public string ModelName { get; set; } = string.Empty;
        public int MaxTokens { get; set; }
        public double Temperature { get; set; }
    }

    // ProjectSettings.cs
    public class ProjectSettings
    {
        public string ProjectPath { get; set; } = string.Empty;
    }
}
