using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DeepSeekChat.Models
{
    // 输入记录
    public class RequirementInput
    {
        public DateTime Timestamp { get; set; }
        public string Input { get; set; }
        public bool IsClarifyingQuestion { get; set; }
        public bool IsOriginalRequirement { get; set; }
        public Dictionary<string, object> SourceMetadata { get; set; } = new Dictionary<string, object>();
    }
}
