using DeepSeekChat.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Emit;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace DeepSeekChat.Services
{
    public class CSharpCompilerService
    {
        /// <summary>
        /// 编译C#程序并返回结果
        /// </summary>
        public CompileResult CompileCSharpProgram(string arguments)
        {
            try
            {
                // 解析参数
                var args = Newtonsoft.Json.JsonConvert.DeserializeObject<Dictionary<string, string>>(arguments);

                if (args == null || !args.ContainsKey("code"))
                {
                    return new CompileResult
                    {
                        Success = false,
                        Output = "参数解析失败，需要包含 'code' 字段",
                        Errors = new List<string> { "缺少代码参数" }
                    };
                }

                string code = args["code"];
                string outputPath = args.ContainsKey("output_path") ? args["output_path"] : null;
                bool execute = args.ContainsKey("execute") && bool.TryParse(args["execute"], out bool exec) && exec;

                return CompileAndExecute(code, outputPath, execute);
            }
            catch (Exception ex)
            {
                return new CompileResult
                {
                    Success = false,
                    Output = $"编译失败: {ex.Message}",
                    Errors = new List<string> { ex.ToString() }
                };
            }
        }

        /// <summary>
        /// 异步编译版本
        /// </summary>
        public async Task<CompileResult> CompileCSharpProgramAsync(string arguments)
        {
            return await Task.Run(() => CompileCSharpProgram(arguments)).ConfigureAwait(false);
        }

        private CompileResult CompileAndExecute(string code, string outputPath = null, bool execute = false)
        {
            var errors = new List<string>();
            var outputMessages = new List<string>();

            try
            {
                // 1. 创建临时目录
                var tempDir = Path.Combine(Path.GetTempPath(), $"Compile_{Guid.NewGuid():N}");
                Directory.CreateDirectory(tempDir);

                // 2. 创建临时项目文件
                var projectFile = Path.Combine(tempDir, "TempProject.csproj");
                var codeFile = Path.Combine(tempDir, "Program.cs");

                // 确定目标框架版本
                string targetFramework = GetTargetFrameworkVersion();

                // 3. 写入项目文件
                File.WriteAllText(projectFile, $@"<Project Sdk=""Microsoft.NET.Sdk"">
                                                  <PropertyGroup>
                                                    <OutputType>Exe</OutputType>
                                                    <TargetFramework>{targetFramework}</TargetFramework>
                                                    <ImplicitUsings>enable</ImplicitUsings>
                                                    <Nullable>enable</Nullable>
                                                    <!-- 生成自包含的单个EXE文件 -->
                                                    <PublishSingleFile>true</PublishSingleFile>
                                                    <SelfContained>true</SelfContained>
                                                    <!-- 根据系统选择运行时标识符 -->
                                                    <RuntimeIdentifier>{GetRuntimeIdentifier()}</RuntimeIdentifier>
                                                    <!-- 包含所有依赖 -->
                                                    <IncludeAllContentForSelfExtract>true</IncludeAllContentForSelfExtract>
                                                    <!-- 启用裁剪（可选，减小文件大小） -->
                                                    <PublishTrimmed>false</PublishTrimmed>
                                                  </PropertyGroup>
                                                </Project>");

                // 4. 写入代码文件
                File.WriteAllText(codeFile, code);
                outputMessages.Add($"代码已写入: {codeFile}");

                // 5. 使用 dotnet publish 发布
                outputMessages.Add("正在发布自包含的可执行文件...");
                var publishOutput = RunDotnetCommand($"publish \"{projectFile}\" -c Release -o \"{tempDir}\\publish\"");

                if (publishOutput.Contains("error") || publishOutput.Contains("失败"))
                {
                    errors.Add($"发布失败: {publishOutput}");
                    return new CompileResult
                    {
                        Success = false,
                        Output = "发布失败",
                        Errors = errors
                    };
                }

                outputMessages.Add("发布成功！");

                // 6. 找到生成的可执行文件
                var publishedFiles = Directory.GetFiles(Path.Combine(tempDir, "publish"), "*.exe");
                if (publishedFiles.Length == 0)
                {
                    errors.Add("未找到生成的可执行文件");
                    return new CompileResult
                    {
                        Success = false,
                        Output = "生成EXE失败",
                        Errors = errors
                    };
                }

                var exePath = publishedFiles[0];

                // 7. 如果需要保存到指定路径
                if (!string.IsNullOrEmpty(outputPath))
                {
                    // 确保是 .exe 扩展名
                    outputPath = Path.ChangeExtension(outputPath, ".exe");
                    File.Copy(exePath, outputPath, true);
                    exePath = outputPath;
                    outputMessages.Add($"已保存可执行文件到: {outputPath}");
                }
                else
                {
                    outputMessages.Add($"生成的可执行文件: {exePath}");
                }

                // 8. 如果需要执行
                string executionOutput = "";
                if (execute)
                {
                    outputMessages.Add("开始执行程序...");
                    executionOutput = ExecuteExeFile(exePath);
                }

                // 9. 清理临时文件（可选）
                try
                {
                    Directory.Delete(tempDir, true);
                }
                catch { }

                return new CompileResult
                {
                    Success = true,
                    Output = string.Join("\n", outputMessages) +
                            (execute ? $"\n\n程序输出:\n{executionOutput}" : ""),
                    Errors = errors,
                    AssemblyPath = exePath,
                    Executed = execute
                };
            }
            catch (Exception ex)
            {
                errors.Add(ex.ToString());
                return new CompileResult
                {
                    Success = false,
                    Output = $"编译过程出错: {ex.Message}",
                    Errors = errors
                };
            }
        }

        // 获取目标框架版本
        private string GetTargetFrameworkVersion()
        {
            var version = Environment.Version;

            // 根据当前运行时确定目标框架
            if (version.Major >= 8) return "net8.0";
            if (version.Major >= 7) return "net7.0";
            if (version.Major >= 6) return "net6.0";
            if (version.Major >= 5) return "net5.0";

            return "netcoreapp3.1";
        }

        // 获取运行时标识符
        private string GetRuntimeIdentifier()
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return RuntimeInformation.ProcessArchitecture switch
                {
                    Architecture.X64 => "win-x64",
                    Architecture.X86 => "win-x86",
                    Architecture.Arm64 => "win-arm64",
                    _ => "win-x64"
                };
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                return RuntimeInformation.ProcessArchitecture switch
                {
                    Architecture.X64 => "linux-x64",
                    Architecture.X86 => "linux-x86",
                    Architecture.Arm64 => "linux-arm64",
                    _ => "linux-x64"
                };
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                return RuntimeInformation.ProcessArchitecture switch
                {
                    Architecture.X64 => "osx-x64",
                    Architecture.Arm64 => "osx-arm64",
                    _ => "osx-x64"
                };
            }

            return "win-x64"; // 默认
        }

        // 运行 dotnet 命令
        private string RunDotnetCommand(string arguments)
        {
            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "dotnet",
                    Arguments = arguments,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    StandardOutputEncoding = Encoding.UTF8,
                    StandardErrorEncoding = Encoding.UTF8
                }
            };

            process.Start();
            var output = process.StandardOutput.ReadToEnd();
            var error = process.StandardError.ReadToEnd();
            process.WaitForExit();

            return output + "\n" + error;
        }

        // 执行 EXE 文件
        private string ExecuteExeFile(string exePath)
        {
            try
            {
                var process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = exePath,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        UseShellExecute = false,
                        CreateNoWindow = true,
                        StandardOutputEncoding = Encoding.UTF8,
                        StandardErrorEncoding = Encoding.UTF8
                    }
                };

                process.Start();
                var output = process.StandardOutput.ReadToEnd();
                var error = process.StandardError.ReadToEnd();
                process.WaitForExit();

                if (!string.IsNullOrEmpty(error))
                {
                    return $"输出:\n{output}\n错误:\n{error}";
                }

                return output;
            }
            catch (Exception ex)
            {
                return $"执行失败: {ex.Message}";
            }
        }

        /// <summary>
        /// 编译简单的控制台应用程序
        /// </summary>
        public CompileResult CompileConsoleProgram(string code)
        {
            return CompileAndExecute(code, null, true);
        }
 
    }
}