using DeepSeekChat.ViewModels;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;

namespace DeepSeekChat.ViewModels
{
    public class FileSystemItem : ViewModelBase
    {
        private string _name;
        private string _fullPath;
        private bool _isExpanded;
        private bool _isSelected;
        private bool _hasLoadedChildren;
        private int _level;  // 添加层级属性

        public string Name
        {
            get => _name;
            set => SetProperty(ref _name, value);
        }

        public string FullPath
        {
            get => _fullPath;
            set => SetProperty(ref _fullPath, value);
        }

        public FileSystemItemType Type { get; set; }

        public ObservableCollection<FileSystemItem> Children { get; set; }

        // 添加Level属性
        public int Level
        {
            get => _level;
            set => SetProperty(ref _level, value);
        }

        // 修改IsExpanded属性，当展开状态变化时更新图标
        public bool IsExpanded
        {
            get => _isExpanded;
            set
            {
                if (SetProperty(ref _isExpanded, value))
                {
                    // 展开状态变化时，更新图标
                    OnPropertyChanged(nameof(Icon));
                }
            }
        }

        public bool IsSelected
        {
            get => _isSelected;
            set => SetProperty(ref _isSelected, value);
        }

        public bool HasLoadedChildren
        {
            get => _hasLoadedChildren;
            set => SetProperty(ref _hasLoadedChildren, value);
        }

        // 添加 IsDirectory 属性
        public bool IsDirectory => Type == FileSystemItemType.Directory;

        // 添加 HasChildren 属性
        public bool HasChildren => Children?.Count > 0;

        // 计算缩进值（用于XAML绑定）
        public int Indent
        {
            get
            {
                // 每个层级缩进16像素，根层级额外缩进4像素
                return Level * 16 + 4;
            }
        }

        // 修改Icon属性，使用Unicode字符而不是HTML实体
        public string Icon
        {
            get
            {
                return Type switch
                {
                    FileSystemItemType.Directory => IsExpanded ? "\uE019" : "\uE017",
                    FileSystemItemType.CSharpFile => "\uE160",
                    FileSystemItemType.XamlFile => "\uE160",
                    FileSystemItemType.ImageFile => "\uE160",
                    FileSystemItemType.TextFile => "\uE160",
                    _ => "\uE160"
                };
            }
        }

        public FileSystemItem()
        {
            Children = new ObservableCollection<FileSystemItem>();
            _level = 0; // 默认层级为0
        }

        // 刷新HasChildren（当Children集合变化时调用）
        public void RefreshHasChildren()
        {
            OnPropertyChanged(nameof(HasChildren));
        }

        // 设置子项的层级
        public void UpdateChildrenLevels()
        {
            foreach (var child in Children)
            {
                child.Level = this.Level + 1;
                child.UpdateChildrenLevels(); // 递归更新所有子项
            }
        }

        // 添加子项并设置层级
        public void AddChild(FileSystemItem child)
        {
            child.Level = this.Level + 1;
            Children.Add(child);
            OnPropertyChanged(nameof(HasChildren));
        }

        // 批量添加子项并设置层级
        public void AddChildren(IEnumerable<FileSystemItem> children)
        {
            foreach (var child in children)
            {
                child.Level = this.Level + 1;
                Children.Add(child);
            }
            OnPropertyChanged(nameof(HasChildren));
        }
    }

    public enum FileSystemItemType
    {
        Directory,
        CSharpFile,
        XamlFile,
        ImageFile,
        TextFile,
        OtherFile
    }
}