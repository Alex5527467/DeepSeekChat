using DeepSeekChat.Models;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DeepSeekChat.Services
{
    public class FileSystemService
    {
        public async Task<BrowseFolderResult> BrowseLocalFolderAsync(string arguments)
        {
            try
            {
                var args = JsonConvert.DeserializeObject<Dictionary<string, object>>(arguments);
                if (args == null || !args.ContainsKey("folder_path"))
                {
                    return new BrowseFolderResult
                    {
                        Error = "缺少必要参数 folder_path"
                    };
                }

                string folderPath = args["folder_path"].ToString();
                bool includeSubdirectories = false;
                string[] filterExtensions = null;

                if (args.ContainsKey("include_subdirectories"))
                {
                    bool.TryParse(args["include_subdirectories"].ToString(), out includeSubdirectories);
                }

                if (args.ContainsKey("filter_extensions"))
                {
                    try
                    {
                        var extensions = JsonConvert.DeserializeObject<List<string>>(
                            args["filter_extensions"].ToString());
                        filterExtensions = extensions?.ToArray();
                    }
                    catch
                    {
                        // 如果解析失败，忽略过滤参数
                    }
                }

                return await BrowseLocalFolderInternalAsync(folderPath, includeSubdirectories, filterExtensions).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                return new BrowseFolderResult
                {
                    Error = $"参数解析失败: {ex.Message}"
                };
            }
        }

        public async Task<FolderInfoResult> GetFolderInfoAsync(string arguments)
        {
            try
            {
                var args = JsonConvert.DeserializeObject<Dictionary<string, string>>(arguments);
                if (args == null || !args.ContainsKey("folder_path"))
                {
                    return new FolderInfoResult
                    {
                        Error = "缺少必要参数 folder_path"
                    };
                }

                return await GetFolderInfoInternalAsync(args["folder_path"]).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                return new FolderInfoResult
                {
                    Error = $"参数解析失败: {ex.Message}"
                };
            }
        }

        // 同步方法用于兼容性
        public BrowseFolderResult BrowseLocalFolder(string arguments)
        {
            return BrowseLocalFolderAsync(arguments).GetAwaiter().GetResult();
        }

        public FolderInfoResult GetFolderInfo(string arguments)
        {
            return GetFolderInfoAsync(arguments).GetAwaiter().GetResult();
        }

        private async Task<BrowseFolderResult> BrowseLocalFolderInternalAsync(string folderPath,
            bool includeSubdirectories = false,
            string[] filterExtensions = null)
        {
            var result = new BrowseFolderResult
            {
                FolderPath = folderPath,
                Files = new List<FileInfoResult>(),
                Subdirectories = new List<DirectoryInfoResult>()
            };

            try
            {
                if (!Directory.Exists(folderPath))
                {
                    result.Info = $"文件夹路径不存在: {folderPath}";
                    return result;
                }

                var searchOption = includeSubdirectories
                    ? SearchOption.AllDirectories
                    : SearchOption.TopDirectoryOnly;

                // 获取文件列表
                var fileEntries = Directory.GetFiles(folderPath, "*.*", searchOption);

                // 应用文件扩展名过滤
                if (filterExtensions != null && filterExtensions.Length > 0)
                {
                    fileEntries = fileEntries.Where(file =>
                        filterExtensions.Any(ext =>
                            file.EndsWith(ext, StringComparison.OrdinalIgnoreCase)))
                        .ToArray();
                }

                // 获取目录列表
                var dirEntries = Directory.GetDirectories(folderPath, "*", searchOption);

                result.TotalFiles = fileEntries.Length;
                result.TotalSubdirectories = dirEntries.Length;

                // 处理文件信息（限制数量防止数据过大）
                int maxFiles = 50;
                foreach (var filePath in fileEntries.Take(maxFiles))
                {
                    try
                    {
                        var fileInfo = new FileInfo(filePath);
                        result.Files.Add(new FileInfoResult
                        {
                            Name = fileInfo.Name,
                            FullPath = fileInfo.FullName,
                            SizeBytes = fileInfo.Length,
                            SizeReadable = FormatFileSize(fileInfo.Length),
                            LastModified = fileInfo.LastWriteTime,
                            CreatedTime = fileInfo.CreationTime,
                            Extension = fileInfo.Extension
                        });
                    }
                    catch (UnauthorizedAccessException)
                    {
                        // 跳过无权限访问的文件
                        continue;
                    }
                }

                // 处理目录信息
                int maxDirs = 20;
                foreach (var dirPath in dirEntries.Take(maxDirs))
                {
                    try
                    {
                        var dirInfo = new DirectoryInfo(dirPath);
                        result.Subdirectories.Add(new DirectoryInfoResult
                        {
                            Name = dirInfo.Name,
                            FullPath = dirInfo.FullName,
                            LastModified = dirInfo.LastWriteTime,
                            CreatedTime = dirInfo.CreationTime
                        });
                    }
                    catch (UnauthorizedAccessException)
                    {
                        // 跳过无权限访问的目录
                        continue;
                    }
                }

                if (fileEntries.Length > maxFiles || dirEntries.Length > maxDirs)
                {
                    result.Note = $"显示前 {maxFiles} 个文件和前 {maxDirs} 个目录，" +
                                 $"共 {fileEntries.Length} 个文件，{dirEntries.Length} 个目录";
                }

                return result;
            }
            catch (UnauthorizedAccessException)
            {
                result.Error = $"无权访问文件夹: {folderPath}";
                return result;
            }
            catch (PathTooLongException)
            {
                result.Error = $"路径过长: {folderPath}";
                return result;
            }
            catch (Exception ex)
            {
                result.Error = $"浏览文件夹失败: {ex.Message}";
                return result;
            }
        }

        private async Task<FolderInfoResult> GetFolderInfoInternalAsync(string folderPath)
        {
            var result = new FolderInfoResult
            {
                FolderPath = folderPath,
                TopFileTypes = new List<FileTypeInfo>()
            };

            try
            {
                if (!Directory.Exists(folderPath))
                {
                    result.Error = $"文件夹路径不存在: {folderPath}";
                    return result;
                }

                var dirInfo = new DirectoryInfo(folderPath);
                result.FolderName = dirInfo.Name;
                result.CreatedTime = dirInfo.CreationTime;
                result.LastModified = dirInfo.LastWriteTime;

                // 获取所有文件（递归）
                var allFiles = new List<FileInfo>();
                await Task.Run(() =>
                {
                    try
                    {
                        allFiles = dirInfo.GetFiles("*.*", SearchOption.AllDirectories).ToList();
                    }
                    catch (UnauthorizedAccessException)
                    {
                        // 部分目录可能无权限访问
                    }
                });

                // 计算总大小和统计文件类型
                long totalSize = 0;
                var fileTypeStats = new Dictionary<string, FileTypeInfo>();

                foreach (var file in allFiles)
                {
                    try
                    {
                        totalSize += file.Length;

                        var ext = string.IsNullOrEmpty(file.Extension)
                            ? "无扩展名"
                            : file.Extension.ToLower();

                        if (!fileTypeStats.ContainsKey(ext))
                        {
                            fileTypeStats[ext] = new FileTypeInfo
                            {
                                Extension = ext,
                                Count = 0,
                                TotalSizeBytes = 0
                            };
                        }

                        fileTypeStats[ext].Count++;
                        fileTypeStats[ext].TotalSizeBytes += file.Length;
                    }
                    catch
                    {
                        // 跳过无法访问的文件
                    }
                }

                result.FileCount = allFiles.Count;
                result.TotalSizeBytes = totalSize;
                result.TotalSizeReadable = FormatFileSize(totalSize);

                // 获取子目录数量
                try
                {
                    var allDirs = dirInfo.GetDirectories("*", SearchOption.AllDirectories);
                    result.SubdirectoryCount = allDirs.Length;
                }
                catch (UnauthorizedAccessException)
                {
                    result.SubdirectoryCount = -1; // 表示无法完全统计
                }

                // 获取前5个最常见的文件类型
                result.TopFileTypes = fileTypeStats.Values
                    .OrderByDescending(x => x.Count)
                    .Take(5)
                    .Select(x => new FileTypeInfo
                    {
                        Extension = x.Extension,
                        Count = x.Count,
                        TotalSizeBytes = x.TotalSizeBytes,
                        TotalSizeReadable = FormatFileSize(x.TotalSizeBytes)
                    })
                    .ToList();

                return result;
            }
            catch (Exception ex)
            {
                result.Error = $"获取文件夹信息失败: {ex.Message}";
                return result;
            }
        }

        /// <summary>
        /// 创建新文件
        /// </summary>
        public async Task<FileOperationResult> CreateFileAsync(string arguments)
        {
            try
            {
                var args = JsonConvert.DeserializeObject<Dictionary<string, string>>(arguments);
                if (args == null || !args.ContainsKey("file_path"))
                {
                    return new FileOperationResult
                    {
                        Success = false,
                        Error = "缺少必要参数 file_path"
                    };
                }

                string filePath = args["file_path"];
                string content = args.ContainsKey("content") ? args["content"] : string.Empty;
                bool overwrite = args.ContainsKey("overwrite") &&
                                bool.TryParse(args["overwrite"], out bool ov) && ov;

                return await CreateFileInternalAsync(filePath, content, overwrite).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                return new FileOperationResult
                {
                    Success = false,
                    Error = $"参数解析失败: {ex.Message}"
                };
            }
        }

        /// <summary>
        /// 删除文件
        /// </summary>
        public async Task<FileOperationResult> DeleteFileAsync(string arguments)
        {
            try
            {
                var args = JsonConvert.DeserializeObject<Dictionary<string, string>>(arguments);
                if (args == null || !args.ContainsKey("file_path"))
                {
                    return new FileOperationResult
                    {
                        Success = false,
                        Error = "缺少必要参数 file_path"
                    };
                }

                string filePath = args["file_path"];
                return await DeleteFileInternalAsync(filePath).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                return new FileOperationResult
                {
                    Success = false,
                    Error = $"参数解析失败: {ex.Message}"
                };
            }
        }

        /// <summary>
        /// 读取文件内容
        /// </summary>
        public async Task<FileReadResult> ReadFileAsync(string arguments)
        {
            try
            {
                var args = JsonConvert.DeserializeObject<Dictionary<string, string>>(arguments);
                if (args == null || !args.ContainsKey("file_path"))
                {
                    return new FileReadResult
                    {
                        Success = false,
                        Error = "缺少必要参数 file_path"
                    };
                }

                string filePath = args["file_path"];
                string encodingName = args.ContainsKey("encoding") ? args["encoding"] : "UTF-8";
                long maxSize = args.ContainsKey("max_size_bytes") &&
                              long.TryParse(args["max_size_bytes"], out long max) ? max : 10 * 1024 * 1024; // 默认10MB

                return await ReadFileInternalAsync(filePath, encodingName, maxSize).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                return new FileReadResult
                {
                    Success = false,
                    Error = $"参数解析失败: {ex.Message}"
                };
            }
        }

        /// <summary>
        /// 写入文件内容
        /// </summary>
        public async Task<FileOperationResult> WriteFileAsync(string arguments)
        {
            try
            {
                var args = JsonConvert.DeserializeObject<Dictionary<string, string>>(arguments);
                if (args == null || !args.ContainsKey("file_path"))
                {
                    return new FileOperationResult
                    {
                        Success = false,
                        Error = "缺少必要参数 file_path"
                    };
                }

                string filePath = args["file_path"];
                string content = args.ContainsKey("content") ? args["content"] : string.Empty;
                string encodingName = args.ContainsKey("encoding") ? args["encoding"] : "UTF-8";

                // 修改：使用 mode 参数替代 append
                // create: 仅创建新文件（文件存在则失败）
                // overwrite: 覆盖文件（文件存在则覆盖）
                // append: 追加到文件末尾
                string mode = args.ContainsKey("mode") ? args["mode"].ToLower() : "create";

                return await WriteFileInternalAsync(filePath, content, encodingName, mode).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                return new FileOperationResult
                {
                    Success = false,
                    Error = $"参数解析失败: {ex.Message}"
                };
            }
        }


        // 同步方法用于兼容性
        public FileOperationResult CreateFile(string arguments)
        {
            return CreateFileAsync(arguments).GetAwaiter().GetResult();
        }

        public FileOperationResult DeleteFile(string arguments)
        {
            return DeleteFileAsync(arguments).GetAwaiter().GetResult();
        }

        public FileReadResult ReadFile(string arguments)
        {
            return ReadFileAsync(arguments).GetAwaiter().GetResult();
        }

        public FileOperationResult WriteFile(string arguments)
        {
            return WriteFileAsync(arguments).GetAwaiter().GetResult();
        }

        private async Task<FileOperationResult> CreateFileInternalAsync(string filePath, string content, bool overwrite)
        {
            try
            {
                if (File.Exists(filePath) && !overwrite)
                {
                    return new FileOperationResult
                    {
                        Success = false,
                        Error = $"文件已存在: {filePath}，如需覆盖请设置 overwrite=true"
                    };
                }

                // 确保目录存在
                var directory = Path.GetDirectoryName(filePath);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                await File.WriteAllTextAsync(filePath, content, Encoding.UTF8).ConfigureAwait(false);

                var fileInfo = new FileInfo(filePath);
                return new FileOperationResult
                {
                    Success = true,
                    Message = $"文件创建成功: {filePath}",
                    FilePath = filePath,
                    FileSize = fileInfo.Length,
                    CreatedTime = fileInfo.CreationTime,
                    LastModified = fileInfo.LastWriteTime
                };
            }
            catch (UnauthorizedAccessException)
            {
                return new FileOperationResult
                {
                    Success = false,
                    Error = $"无权创建文件: {filePath}"
                };
            }
            catch (PathTooLongException)
            {
                return new FileOperationResult
                {
                    Success = false,
                    Error = $"路径过长: {filePath}"
                };
            }
            catch (IOException ex)
            {
                return new FileOperationResult
                {
                    Success = false,
                    Error = $"文件操作失败: {ex.Message}"
                };
            }
            catch (Exception ex)
            {
                return new FileOperationResult
                {
                    Success = false,
                    Error = $"创建文件失败: {ex.Message}"
                };
            }
        }

        private async Task<FileOperationResult> DeleteFileInternalAsync(string filePath)
        {
            try
            {
                if (!File.Exists(filePath))
                {
                    return new FileOperationResult
                    {
                        Success = false,
                        Error = $"文件不存在: {filePath}"
                    };
                }

                var fileInfo = new FileInfo(filePath);
                var info = new
                {
                    Path = fileInfo.FullName,
                    Size = fileInfo.Length,
                    Created = fileInfo.CreationTime,
                    LastModified = fileInfo.LastWriteTime
                };

                await Task.Run(() => File.Delete(filePath)).ConfigureAwait(false);

                return new FileOperationResult
                {
                    Success = true,
                    Message = $"文件删除成功: {filePath}",
                    FilePath = info.Path,
                    FileSize = info.Size,
                    CreatedTime = info.Created,
                    LastModified = info.LastModified
                };
            }
            catch (UnauthorizedAccessException)
            {
                return new FileOperationResult
                {
                    Success = false,
                    Error = $"无权删除文件: {filePath}"
                };
            }
            catch (IOException ex)
            {
                return new FileOperationResult
                {
                    Success = false,
                    Error = $"文件操作失败: {ex.Message}，文件可能正在被使用"
                };
            }
            catch (Exception ex)
            {
                return new FileOperationResult
                {
                    Success = false,
                    Error = $"删除文件失败: {ex.Message}"
                };
            }
        }

        private async Task<FileReadResult> ReadFileInternalAsync(string filePath, string encodingName, long maxSize)
        {
            try
            {
                if (!File.Exists(filePath))
                {
                    return new FileReadResult
                    {
                        Success = false,
                        Error = $"文件不存在: {filePath}"
                    };
                }

                var fileInfo = new FileInfo(filePath);

                // 检查文件大小
                if (fileInfo.Length > maxSize)
                {
                    return new FileReadResult
                    {
                        Success = false,
                        Error = $"文件过大 ({FormatFileSize(fileInfo.Length)})，超过限制 {FormatFileSize(maxSize)}"
                    };
                }

                // 获取编码
                Encoding encoding = GetEncoding(encodingName);
                if (encoding == null)
                {
                    return new FileReadResult
                    {
                        Success = false,
                        Error = $"不支持的编码格式: {encodingName}"
                    };
                }

                // 读取文件内容
                string content;
                try
                {
                    content = await File.ReadAllTextAsync(filePath, encoding).ConfigureAwait(false);
                }
                catch (DecoderFallbackException)
                {
                    // 如果指定的编码读取失败，尝试使用默认编码
                    content = await File.ReadAllTextAsync(filePath, Encoding.Default).ConfigureAwait(false);
                }

                return new FileReadResult
                {
                    Success = true,
                    FilePath = fileInfo.FullName,
                    Content = content,
                    FileSize = fileInfo.Length,
                    FileSizeReadable = FormatFileSize(fileInfo.Length),
                    Encoding = encoding.EncodingName,
                    CreatedTime = fileInfo.CreationTime,
                    LastModified = fileInfo.LastWriteTime,
                    LastAccessed = fileInfo.LastAccessTime
                };
            }
            catch (UnauthorizedAccessException)
            {
                return new FileReadResult
                {
                    Success = false,
                    Error = $"无权读取文件: {filePath}"
                };
            }
            catch (PathTooLongException)
            {
                return new FileReadResult
                {
                    Success = false,
                    Error = $"路径过长: {filePath}"
                };
            }
            catch (IOException ex)
            {
                return new FileReadResult
                {
                    Success = false,
                    Error = $"文件操作失败: {ex.Message}"
                };
            }
            catch (Exception ex)
            {
                return new FileReadResult
                {
                    Success = false,
                    Error = $"读取文件失败: {ex.Message}"
                };
            }
        }

        private async Task<FileOperationResult> WriteFileInternalAsync(string filePath, string content, string encodingName, string mode)
        {
            try
            {
                // 检查模式
                if (mode == "create" && File.Exists(filePath))
                {
                    return new FileOperationResult
                    {
                        Success = false,
                        Error = $"文件已存在: {filePath}，如需覆盖请设置 mode=overwrite"
                    };
                }

                // 确保目录存在
                var directory = Path.GetDirectoryName(filePath);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                // 获取编码
                Encoding encoding = GetEncoding(encodingName);
                if (encoding == null)
                {
                    return new FileOperationResult
                    {
                        Success = false,
                        Error = $"不支持的编码格式: {encodingName}"
                    };
                }

                // 根据模式写入文件
                switch (mode)
                {
                    case "append":
                        await File.AppendAllTextAsync(filePath, content, encoding).ConfigureAwait(false);
                        break;

                    case "overwrite":
                    case "create":
                    default:
                        await File.WriteAllTextAsync(filePath, content, encoding).ConfigureAwait(false);
                        break;
                }

                var fileInfo = new FileInfo(filePath);
                return new FileOperationResult
                {
                    Success = true,
                    Message = $"文件写入成功 ({mode}模式): {filePath}",
                    FilePath = filePath,
                    FileSize = fileInfo.Length,
                    CreatedTime = fileInfo.CreationTime,
                    LastModified = fileInfo.LastWriteTime
                };
            }
            catch (UnauthorizedAccessException)
            {
                return new FileOperationResult
                {
                    Success = false,
                    Error = $"无权写入文件: {filePath}"
                };
            }
            catch (PathTooLongException)
            {
                return new FileOperationResult
                {
                    Success = false,
                    Error = $"路径过长: {filePath}"
                };
            }
            catch (IOException ex)
            {
                return new FileOperationResult
                {
                    Success = false,
                    Error = $"文件操作失败: {ex.Message}"
                };
            }
            catch (Exception ex)
            {
                return new FileOperationResult
                {
                    Success = false,
                    Error = $"写入文件失败: {ex.Message}"
                };
            }
        }
        public async Task<string> GetFolderStructureDescription(string folderPath)
        {
            try
            {
                // 正确创建 JSON 参数
                var parameters = new Dictionary<string, object>
                {
                    ["folder_path"] = folderPath,
                    ["include_subdirectories"] = true
                };

                // 序列化为 JSON
                string arguments = JsonConvert.SerializeObject(parameters);

                var toolResult = await BrowseLocalFolderAsync(arguments);

                // 转换为BrowseFolderResult对象
                var browseResult = JsonConvert.DeserializeObject<BrowseFolderResult>(
                    JsonConvert.SerializeObject(toolResult));

                if (browseResult == null)
                    return "无法解析文件夹信息";

                // 构建描述
                var structureBuilder = new StringBuilder();
                structureBuilder.AppendLine($"当前工作目录: {browseResult.FolderPath}");

                if (!string.IsNullOrEmpty(browseResult.Info))
                {
                    structureBuilder.AppendLine($"{browseResult.Info}");

                    return structureBuilder.ToString();
                }

                // 构建树形结构显示
                structureBuilder.AppendLine("\n文件夹结构:");

                // 获取根目录信息
                var rootDir = new DirectoryInfo(folderPath);

                // 构建目录树
                BuildDirectoryTree(structureBuilder, rootDir, "", true, browseResult);

                // 如果有备注信息
                if (!string.IsNullOrEmpty(browseResult.Note))
                {
                    structureBuilder.AppendLine($"\n 备注: {browseResult.Note}");
                }

                return structureBuilder.ToString();
            }
            catch (Exception ex)
            {
                return $" 获取文件夹结构时出错: {ex.Message}";
            }
        }

        // 辅助方法：构建目录树
        private void BuildDirectoryTree(StringBuilder builder, DirectoryInfo directory,
            string indent, bool isLast, BrowseFolderResult browseResult)
        {
            // 获取相对于根目录的路径
            string relativePath = GetRelativePath(browseResult.FolderPath, directory.FullName);

            // 如果是根目录，特殊处理
            if (directory.FullName == browseResult.FolderPath)
            {
                builder.AppendLine($"{indent} ./");
            }
            else
            {
                builder.AppendLine($"{indent}├──  {relativePath}/");
            }

            // 构建新的缩进
            string newIndent = indent + (isLast ? "    " : "│   ");

            try
            {
                // 获取当前目录下的所有文件
                var files = directory.GetFiles();
                if (files.Any())
                {
                    // 按文件名排序
                    var sortedFiles = files.OrderBy(f => f.Name).ToList();

                    for (int i = 0; i < sortedFiles.Count; i++)
                    {
                        bool isFileLast = (i == sortedFiles.Count - 1) &&
                                         (directory.GetDirectories().Length == 0);

                        string fileRelativePath = GetRelativePath(browseResult.FolderPath, sortedFiles[i].FullName);

                        if (isFileLast)
                        {
                            builder.AppendLine($"{newIndent}└──  {fileRelativePath}");
                        }
                        else
                        {
                            builder.AppendLine($"{newIndent}├──  {fileRelativePath}");
                        }
                    }
                }

                // 获取当前目录下的所有子目录并递归处理
                var subDirectories = directory.GetDirectories();
                if (subDirectories.Any())
                {
                    // 按目录名排序
                    var sortedDirs = subDirectories.OrderBy(d => d.Name).ToList();

                    for (int i = 0; i < sortedDirs.Count; i++)
                    {
                        bool isSubDirLast = i == sortedDirs.Count - 1;
                        BuildDirectoryTree(builder, sortedDirs[i], newIndent, isSubDirLast, browseResult);
                    }
                }
            }
            catch (UnauthorizedAccessException)
            {
                builder.AppendLine($"{newIndent}└──  无访问权限");
            }
            catch (Exception ex)
            {
                builder.AppendLine($"{newIndent}└──  错误: {ex.Message}");
            }
        }

        // 辅助方法：获取相对路径（使用Path类）
        private string GetRelativePath(string rootPath, string fullPath)
        {
            // 确保根路径以目录分隔符结尾
            if (!rootPath.EndsWith(Path.DirectorySeparatorChar.ToString()))
            {
                rootPath += Path.DirectorySeparatorChar;
            }

            // 使用Path类获取相对路径
            try
            {
                var rootUri = new Uri(rootPath);
                var fullUri = new Uri(fullPath);
                var relativeUri = rootUri.MakeRelativeUri(fullUri);
                return Uri.UnescapeDataString(relativeUri.ToString())
                    .Replace('/', Path.DirectorySeparatorChar);
            }
            catch
            {
                // 如果URI方法失败，使用简单方法
                if (fullPath.StartsWith(rootPath))
                {
                    return fullPath.Substring(rootPath.Length);
                }
                return fullPath;
            }
        }

        private Encoding GetEncoding(string encodingName)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(encodingName))
                    return Encoding.UTF8;

                return encodingName.ToUpper() switch
                {
                    "UTF8" or "UTF-8" => Encoding.UTF8,
                    "ASCII" => Encoding.ASCII,
                    "UTF32" or "UTF-32" => Encoding.UTF32,
                    "UTF7" or "UTF-7" => Encoding.UTF7,
                    "UNICODE" or "UTF16" or "UTF-16" => Encoding.Unicode,
                    "BIGENDIANUNICODE" => Encoding.BigEndianUnicode,
                    "DEFAULT" => Encoding.Default,
                    _ => Encoding.GetEncoding(encodingName)
                };
            }
            catch
            {
                return null;
            }
        }

        private string FormatFileSize(long bytes)
        {
            string[] sizes = { "B", "KB", "MB", "GB", "TB" };
            double len = bytes;
            int order = 0;
            while (len >= 1024 && order < sizes.Length - 1)
            {
                order++;
                len = len / 1024;
            }
            return string.Format("{0:0.##} {1}", len, sizes[order]);
        }
    }
}