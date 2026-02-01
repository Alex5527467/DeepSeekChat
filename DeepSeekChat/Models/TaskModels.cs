using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AgentApp1.Models
{
    public class TaskDefinition
    {
        [JsonProperty("task_id")]
        public string TaskId { get; set; } = System.Guid.NewGuid().ToString();

        [JsonProperty("project_name")]
        public string ProjectName { get; set; } = string.Empty;

        [JsonProperty("file_name")]
        public string FileName { get; set; } = string.Empty;

        [JsonProperty("file_path")]
        public string FilePath { get; set; } = string.Empty;

        [JsonProperty("function")]
        public string Function { get; set; } = string.Empty;

        [JsonProperty("dependencies")]
        public List<string> Dependencies { get; set; } = new List<string>();

        [JsonProperty("requirements")]
        public string Requirements { get; set; } = string.Empty;

        [JsonProperty("estimated_complexity")]
        public string EstimatedComplexity { get; set; } = "medium";

        [JsonProperty("technology_requirements")]
        public List<string> TechnologyRequirements { get; set; } = new List<string>();
    }

    public class TaskResponse
    {
        [JsonProperty("tasks")]
        public List<TaskDefinition> Tasks { get; set; } = new List<TaskDefinition>();

    }
}
