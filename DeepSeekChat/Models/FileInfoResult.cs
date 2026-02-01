using System.Collections.Generic;
using System;

namespace DeepSeekChat.Models
{
    public class FileOperationResult
    {
        public bool Success { get; set; }
        public string Message { get; set; }
        public string Error { get; set; }
        public string FilePath { get; set; }
        public long FileSize { get; set; }
        public DateTime CreatedTime { get; set; }
        public DateTime LastModified { get; set; }
    }

    public class FileReadResult
    {
        public bool Success { get; set; }
        public string Error { get; set; }
        public string FilePath { get; set; }
        public string Content { get; set; }
        public long FileSize { get; set; }
        public string FileSizeReadable { get; set; }
        public string Encoding { get; set; }
        public DateTime CreatedTime { get; set; }
        public DateTime LastModified { get; set; }
        public DateTime LastAccessed { get; set; }
    }

    // 原有的模型类保持不变
    public class BrowseFolderResult
    {
        public string FolderPath { get; set; }
        public List<FileInfoResult> Files { get; set; }
        public List<DirectoryInfoResult> Subdirectories { get; set; }
        public int TotalFiles { get; set; }
        public int TotalSubdirectories { get; set; }
        public string Error { get; set; }
        public string Note { get; set; }
        public string Info { get; set; }
    }

    public class FileInfoResult
    {
        public string Name { get; set; }
        public string FullPath { get; set; }
        public long SizeBytes { get; set; }
        public string SizeReadable { get; set; }
        public DateTime LastModified { get; set; }
        public DateTime CreatedTime { get; set; }
        public string Extension { get; set; }
    }

    public class DirectoryInfoResult
    {
        public string Name { get; set; }
        public string FullPath { get; set; }
        public DateTime LastModified { get; set; }
        public DateTime CreatedTime { get; set; }
    }

    public class FolderInfoResult
    {
        public string FolderPath { get; set; }
        public string FolderName { get; set; }
        public int FileCount { get; set; }
        public int SubdirectoryCount { get; set; }
        public long TotalSizeBytes { get; set; }
        public string TotalSizeReadable { get; set; }
        public DateTime CreatedTime { get; set; }
        public DateTime LastModified { get; set; }
        public List<FileTypeInfo> TopFileTypes { get; set; }
        public string Error { get; set; }
    }

    public class FileTypeInfo
    {
        public string Extension { get; set; }
        public int Count { get; set; }
        public long TotalSizeBytes { get; set; }
        public string TotalSizeReadable { get; set; }
    }
}