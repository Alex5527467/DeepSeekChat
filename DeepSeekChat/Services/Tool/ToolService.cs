using DeepSeekChat.Agent;
using DeepSeekChat.Models;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace DeepSeekChat.Services
{
    public class ToolService : IToolService
    {
        private readonly FileSystemService _fileSystemService;
        private readonly CSharpCompilerService _compilerService;
        private readonly CommandExecService _commandExecService;

        public ToolService(InMemoryMessageBus msgBus)
        {
            _fileSystemService = new FileSystemService();
            _compilerService = new CSharpCompilerService();
            _commandExecService = new CommandExecService();
        }

        public async Task<object> ExecuteToolAsync(string toolName, Dictionary<string, object> arguments)
        {
            return toolName switch
            {
                "browse_local_folder" => await _fileSystemService.BrowseLocalFolderAsync(arguments).ConfigureAwait(false),
                "get_folder_info" => await _fileSystemService.GetFolderInfoAsync(arguments).ConfigureAwait(false),
                "create_file" => await _fileSystemService.CreateFileAsync(arguments).ConfigureAwait(false),
                "delete_file" => await _fileSystemService.DeleteFileAsync(arguments).ConfigureAwait(false),
                "read_file" => await _fileSystemService.ReadFileAsync(arguments).ConfigureAwait(false),
                "write_file" => await _fileSystemService.WriteFileAsync(arguments).ConfigureAwait(false),

                "get_folder_structure_description" => await _fileSystemService.GetFolderStructureDescription(arguments).ConfigureAwait(false),

                // 添加编译工具
                "compile_csharp" => await _compilerService.CompileCSharpProgramAsync(arguments).ConfigureAwait(false),
                "compile_and_execute" => await _compilerService.CompileCSharpProgramAsync(arguments).ConfigureAwait(false),

                "execute_command" => await _commandExecService.ExecuteCommandAsync(arguments).ConfigureAwait(false),

                _ => throw new NotSupportedException($"工具 '{toolName}' 不支持")
            };
        }

        // 同步方法用于兼容性
        public object ExecuteTool(string toolName, Dictionary<string, object> arguments)
        {
            return ExecuteToolAsync(toolName, arguments).GetAwaiter().GetResult();
        }
    }

}