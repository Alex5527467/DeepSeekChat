using DeepSeekChat.ViewModels;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace DeepSeekChat
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            Loaded += MainWindow_Loaded;
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            // 将 Window 的 DataContext 存储到 TreeView 的 Tag 中
            ProjectTreeView.Tag = DataContext;
        }

        // 在代码后台中处理 Expanded 事件
        private void TreeViewItem_Expanded(object sender, RoutedEventArgs e)
        {
            if (e.OriginalSource is TreeViewItem treeViewItem && treeViewItem.DataContext is FileSystemItem item)
            {
                // 调用 ViewModel 的加载方法
                ((MainViewModel)DataContext).OnTreeViewItemExpanded(item);
            }
        }

        private void TreeView_ContextMenuOpening(object sender, ContextMenuEventArgs e)
        {
            try
            {
                var treeView = sender as TreeView;
                if (treeView == null) return;

                // 获取鼠标位置
                var position = Mouse.GetPosition(treeView);

                // 使用 HitTest 找到元素
                var hitResult = VisualTreeHelper.HitTest(treeView, position);
                if (hitResult == null)
                {
                    e.Handled = true;
                    return;
                }

                // 查找 TreeViewItem
                var treeViewItem = GetTreeViewItemFromHitTest(hitResult.VisualHit);
                if (treeViewItem != null)
                {
                    // 选中该项
                    treeViewItem.IsSelected = true;

                    // 确保 TreeView 的 SelectedItem 被更新
                    treeView.Focus();

                    e.Handled = false;
                }
                else
                {
                    e.Handled = true;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"ContextMenuOpening 错误: {ex.Message}");
                e.Handled = true;
            }
        }
        private TreeViewItem GetTreeViewItemFromHitTest(DependencyObject hitObject)
        {
            var current = hitObject;

            // 限制查找深度，避免无限循环
            int maxDepth = 20;
            int currentDepth = 0;

            while (current != null && currentDepth < maxDepth)
            {
                if (current is TreeViewItem treeViewItem)
                {
                    return treeViewItem;
                }

                // 尝试获取父级
                DependencyObject parent = null;

                // 先尝试视觉树
                parent = VisualTreeHelper.GetParent(current);

                // 如果视觉树没有，尝试逻辑树（针对某些特殊情况）
                if (parent == null && current is FrameworkElement frameworkElement)
                {
                    parent = frameworkElement.Parent;
                }

                current = parent;
                currentDepth++;
            }

            return null;
        }

    }
}

