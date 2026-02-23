using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DeepSeekChat.Services
{
    public class CommandExecService
    {
        /// <summary>
        /// 执行Windows命令行程序
        /// </summary>
        public async Task<string> ExecuteCommandAsync(Dictionary<string, object> args)
        {
            try
            {
                if (args == null || !args.ContainsKey("command"))
                {
                    return "缺少必要参数 command";
                }

                string command = args["command"].ToString();
                string arguments = args.ContainsKey("arguments") ? args["arguments"].ToString() : string.Empty;
                string workingDirectory = args.ContainsKey("working_directory") ? args["working_directory"].ToString() : Directory.GetCurrentDirectory();
                bool waitForExit = args.ContainsKey("wait_for_exit")
                                && bool.TryParse(args["wait_for_exit"].ToString(), out bool wait) && wait;
                int timeoutSeconds = args.ContainsKey("timeout_seconds") &&
                                    int.TryParse(args["timeout_seconds"].ToString(), out int timeout) ? timeout : 30;
                bool showWindow = args.ContainsKey("show_window") &&
                                 bool.TryParse(args["show_window"].ToString(), out bool show) && show;

                return await ExecuteCommandInternalAsync(command, arguments, workingDirectory, waitForExit, timeoutSeconds, showWindow).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                return $"参数解析失败: {ex.Message}";
            }
        }

        private async Task<string> ExecuteCommandInternalAsync(string command, string arguments, string workingDirectory, bool waitForExit, int timeoutSeconds, bool showWindow)
        {
            try
            {
                Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

                // 验证工作目录是否存在
                if (!string.IsNullOrEmpty(workingDirectory) && !Directory.Exists(workingDirectory))
                {
                    return $"工作目录不存在: {workingDirectory}";
                }

                string originalCommand = command;
                string originalArguments = arguments;

                // 处理Windows内部命令和脚本
                if (IsWindowsInternalCommand(command) ||
                    command.EndsWith(".bat", StringComparison.OrdinalIgnoreCase) ||
                    command.EndsWith(".cmd", StringComparison.OrdinalIgnoreCase))
                {
                    arguments = $"/c {command} {arguments}";
                    command = "cmd.exe";
                }
                else if (!File.Exists(command))
                {
                    string fullPath = FindExecutableInPath(command);
                    if (!string.IsNullOrEmpty(fullPath))
                    {
                        command = fullPath;
                    }
                    else
                    {
                        arguments = $"/c {command} {arguments}";
                        command = "cmd.exe";
                    }
                }

                var processStartInfo = new ProcessStartInfo
                {
                    FileName = command,
                    Arguments = arguments,
                    WorkingDirectory = workingDirectory,
                };

                // 根据 waitForExit 和 showWindow 参数设置进程启动选项
                if (waitForExit)
                {
                    // 需要等待退出时，必须使用输出重定向来获取输出
                    processStartInfo.UseShellExecute = false;
                    processStartInfo.CreateNoWindow = true;
                    processStartInfo.RedirectStandardOutput = true;
                    processStartInfo.RedirectStandardError = true;
                    processStartInfo.StandardOutputEncoding = Encoding.GetEncoding("GBK");
                    processStartInfo.StandardErrorEncoding = Encoding.GetEncoding("GBK");
                }
                else if (showWindow)
                {
                    // 不等待退出但显示控制台窗口
                    processStartInfo.UseShellExecute = true;
                    processStartInfo.CreateNoWindow = false;
                    processStartInfo.RedirectStandardOutput = false;
                    processStartInfo.RedirectStandardError = false;
                    processStartInfo.WindowStyle = ProcessWindowStyle.Normal;
                }
                else
                {
                    // 不等待退出且不显示控制台窗口（后台运行）
                    processStartInfo.UseShellExecute = true;
                    processStartInfo.CreateNoWindow = true;
                    processStartInfo.RedirectStandardOutput = false;
                    processStartInfo.RedirectStandardError = false;
                    processStartInfo.WindowStyle = ProcessWindowStyle.Hidden;
                }

                using (var process = new Process())
                {
                    process.StartInfo = processStartInfo;

                    var outputBuilder = new StringBuilder();
                    var errorBuilder = new StringBuilder();

                    if (waitForExit)
                    {
                        process.OutputDataReceived += (sender, e) =>
                        {
                            if (e.Data != null)
                            {
                                outputBuilder.AppendLine(e.Data);
                            }
                        };
                        process.ErrorDataReceived += (sender, e) =>
                        {
                            if (e.Data != null)
                            {
                                errorBuilder.AppendLine(e.Data);
                            }
                        };
                    }

                    try
                    {
                        process.Start();

                        if (waitForExit)
                        {
                            process.BeginOutputReadLine();
                            process.BeginErrorReadLine();

                            // 等待进程退出，带超时
                            var completed = await Task.Run(() => process.WaitForExit(timeoutSeconds * 1000)).ConfigureAwait(false);

                            if (!completed)
                            {
                                try
                                {
                                    process.Kill();
                                    return $"命令执行超时（{timeoutSeconds}秒），已终止进程";
                                }
                                catch
                                {
                                    return $"命令执行超时（{timeoutSeconds}秒），但无法终止进程";
                                }
                            }

                            var output = outputBuilder.ToString();
                            var error = errorBuilder.ToString();

                            var result = new StringBuilder();
                            result.AppendLine($"命令执行完成，退出代码: {process.ExitCode}");

                            if (!string.IsNullOrEmpty(output))
                            {
                                result.AppendLine("输出信息:");
                                result.AppendLine(output);
                            }

                            if (!string.IsNullOrEmpty(error))
                            {
                                result.AppendLine("错误信息:");
                                result.AppendLine(error);
                            }

                            return result.ToString();
                        }
                        else
                        {
                            // 不等待，直接返回进程ID
                            return $"命令已启动，进程ID: {process.Id}";
                        }
                    }
                    catch (Exception ex)
                    {
                        return $"启动进程失败: {ex.Message}";
                    }
                }
            }
            catch (UnauthorizedAccessException)
            {
                return $"无权执行命令: {command}";
            }
            catch (Exception ex)
            {
                return $"执行命令失败: {ex.Message}";
            }
        }

        /// <summary>
        /// 检查是否为Windows内部命令
        /// </summary>
        private bool IsWindowsInternalCommand(string command)
        {
            if (string.IsNullOrEmpty(command))
                return false;

            // 移除可能的扩展名和路径
            string cmdName = Path.GetFileNameWithoutExtension(command).ToLowerInvariant();

            // Windows cmd.exe 内部命令列表
            var internalCommands = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "dir", "cd", "md", "rd", "mkdir", "rmdir", "copy", "del", "erase",
        "ren", "rename", "type", "cls", "echo", "set", "path", "prompt",
        "pushd", "popd", "if", "else", "for", "goto", "call", "exit",
        "pause", "rem", "shift", "assoc", "ftype", "color", "date", "time",
        "ver", "vol", "label", "title", "chcp", "chdir", "clink", "cmdextversion",
        "start", "taskkill", "tasklist", "find", "findstr", "more", "sort",
        "attrib", "cacls", "icacls", "xcopy", "robocopy", "move", "replace",
        "mklink", "deltree", "tree", "fc", "comp", "diskcomp", "diskcopy",
        "format", "label", "vol", "chkdsk", "chkntfs", "convert", "mountvol",
        "defrag", "diskpart", "cleanmgr", "sfc", "verifier", "powercfg",
        "shutdown", "logoff", "msg", "tscon", "tsdiscon", "tskill", "tsshutdn"
    };

            return internalCommands.Contains(cmdName);
        }

        /// <summary>
        /// 在PATH环境变量中查找可执行文件
        /// </summary>
        private string FindExecutableInPath(string executable)
        {
            if (string.IsNullOrEmpty(executable))
                return null;

            // 如果已经是完整路径，直接返回
            if (File.Exists(executable))
                return executable;

            // 获取PATH环境变量
            string pathEnv = Environment.GetEnvironmentVariable("PATH");
            if (string.IsNullOrEmpty(pathEnv))
                return null;

            // 常见的可执行文件扩展名
            string[] extensions = { "", ".exe", ".bat", ".cmd", ".com", ".ps1" };

            // 分割PATH并查找
            foreach (string path in pathEnv.Split(Path.PathSeparator))
            {
                if (string.IsNullOrWhiteSpace(path))
                    continue;

                try
                {
                    foreach (string ext in extensions)
                    {
                        string fullPath = Path.Combine(path.Trim(), executable + ext);
                        if (File.Exists(fullPath))
                        {
                            return fullPath;
                        }
                    }
                }
                catch
                {
                    // 忽略无效路径，继续下一个
                    continue;
                }
            }

            return null;
        }

        /// <summary>
        /// 启动应用程序
        /// </summary>
        public async Task<string> LaunchApplicationAsync(Dictionary<string, object> args)
        {
            try
            {
                if (args == null || !args.ContainsKey("app_path"))
                {
                    return "缺少必要参数 app_path";
                }

                string appPath = args["app_path"].ToString();
                string appArguments = args.ContainsKey("app_arguments") ? args["app_arguments"].ToString() : string.Empty;
                string windowStyle = args.ContainsKey("window_style") ? args["window_style"].ToString() : "Normal";

                return await LaunchApplicationInternalAsync(appPath, appArguments, windowStyle).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                return $"参数解析失败: {ex.Message}";
            }
        }

        private async Task<string> LaunchApplicationInternalAsync(string appPath, string appArguments, string windowStyle)
        {
            try
            {
                if (!File.Exists(appPath))
                {
                    return $"应用程序不存在: {appPath}";
                }

                var processStartInfo = new ProcessStartInfo
                {
                    FileName = appPath,
                    Arguments = appArguments,
                    UseShellExecute = true, // 使用ShellExecute启动应用程序
                    WorkingDirectory = Path.GetDirectoryName(appPath)
                };

                // 设置窗口样式
                switch (windowStyle.ToLower())
                {
                    case "minimized":
                        processStartInfo.WindowStyle = ProcessWindowStyle.Minimized;
                        break;
                    case "maximized":
                        processStartInfo.WindowStyle = ProcessWindowStyle.Maximized;
                        break;
                    case "hidden":
                        processStartInfo.WindowStyle = ProcessWindowStyle.Hidden;
                        break;
                    default:
                        processStartInfo.WindowStyle = ProcessWindowStyle.Normal;
                        break;
                }

                using (var process = new Process())
                {
                    process.StartInfo = processStartInfo;
                    process.Start();
                    return $"应用程序已启动: {Path.GetFileName(appPath)}，进程ID: {process.Id}";
                }
            }
            catch (Exception ex)
            {
                return $"启动应用程序失败: {ex.Message}";
            }
        }

        /// <summary>
        /// 执行PowerShell脚本
        /// </summary>
        public async Task<string> RunPowerShellScriptAsync(Dictionary<string, object> args)
        {
            try
            {
                string scriptContent = args.ContainsKey("script_content") ? args["script_content"].ToString() : string.Empty;
                string scriptFile = args.ContainsKey("script_file") ? args["script_file"].ToString() : string.Empty;
                string executionPolicy = args.ContainsKey("execution_policy") ? args["execution_policy"].ToString() : "RemoteSigned";

                if (string.IsNullOrEmpty(scriptContent) && string.IsNullOrEmpty(scriptFile))
                {
                    return "需要提供 script_content 或 script_file";
                }

                return await RunPowerShellInternalAsync(scriptContent, scriptFile, executionPolicy).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                return $"参数解析失败: {ex.Message}";
            }
        }

        private async Task<string> RunPowerShellInternalAsync(string scriptContent, string scriptFile, string executionPolicy)
        {
            try
            {
                var arguments = new StringBuilder();

                // 设置执行策略
                arguments.Append($"-ExecutionPolicy {executionPolicy} ");

                if (!string.IsNullOrEmpty(scriptFile))
                {
                    if (!File.Exists(scriptFile))
                    {
                        return $"PowerShell脚本文件不存在: {scriptFile}";
                    }
                    arguments.Append($"-File \"{scriptFile}\" ");
                }
                else if (!string.IsNullOrEmpty(scriptContent))
                {
                    arguments.Append($"-Command \"& {{ {scriptContent.Replace("\"", "\\\"")} }}\" ");
                }

                var processStartInfo = new ProcessStartInfo
                {
                    FileName = "powershell.exe",
                    Arguments = arguments.ToString(),
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                    StandardOutputEncoding = Encoding.UTF8,
                    StandardErrorEncoding = Encoding.UTF8
                };

                using (var process = new Process())
                {
                    process.StartInfo = processStartInfo;

                    var outputBuilder = new StringBuilder();
                    var errorBuilder = new StringBuilder();

                    process.OutputDataReceived += (sender, e) =>
                    {
                        if (e.Data != null)
                        {
                            outputBuilder.AppendLine(e.Data);
                        }
                    };
                    process.ErrorDataReceived += (sender, e) =>
                    {
                        if (e.Data != null)
                        {
                            errorBuilder.AppendLine(e.Data);
                        }
                    };

                    process.Start();
                    process.BeginOutputReadLine();
                    process.BeginErrorReadLine();

                    await Task.Run(() => process.WaitForExit(30000)).ConfigureAwait(false); // 30秒超时

                    var result = new StringBuilder();
                    result.AppendLine($"PowerShell脚本执行完成，退出代码: {process.ExitCode}");

                    if (outputBuilder.Length > 0)
                    {
                        result.AppendLine("输出信息:");
                        result.Append(outputBuilder.ToString());
                    }

                    if (errorBuilder.Length > 0)
                    {
                        result.AppendLine("错误信息:");
                        result.Append(errorBuilder.ToString());
                    }

                    return result.ToString();
                }
            }
            catch (Exception ex)
            {
                return $"执行PowerShell脚本失败: {ex.Message}";
            }
        }
    }
}
