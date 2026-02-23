using DeepSeekChat.ViewModels;
using System.Windows;
using System.Windows.Controls;

namespace DeepSeekChat.ViewModels
{
    public class FileSystemTemplateSelector : DataTemplateSelector
    {
        public DataTemplate FolderTemplate { get; set; }
        public DataTemplate FileTemplate { get; set; }

        public override DataTemplate SelectTemplate(object item, DependencyObject container)
        {
            if (item is FileSystemItem fileItem)
            {
                return fileItem.Type == FileSystemItemType.Directory
                    ? FolderTemplate
                    : FileTemplate;
            }

            return base.SelectTemplate(item, container);
        }
    }
}