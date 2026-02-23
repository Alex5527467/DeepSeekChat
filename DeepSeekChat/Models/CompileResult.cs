using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DeepSeekChat.Models
{
    /// <summary>
    /// 编译结果模型
    /// </summary>
    public class CompileResult
    {
        public bool Success { get; set; }
        public string Output { get; set; }
        public List<string> Errors { get; set; }
        public string AssemblyPath { get; set; }
        public bool Executed { get; set; }

        public CompileResult()
        {
            Errors = new List<string>();
        }
    }
}
