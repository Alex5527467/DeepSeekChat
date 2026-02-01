using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

namespace DeepSeekChat.ViewModels
{
    /// <summary>
    /// 设置ViewModel
    /// </summary>
    public class SettingsViewModel : BaseViewModel
    {
        private readonly IConfiguration _configuration;

        private string _apiKey;
        private string _apiBaseUrl = "https://api.deepseek.com/chat/";
        private string _modelName = "completions";
        private int _maxTokens = 4000;
        private double _temperature = 0.7;
        private string _projectPath = string.Empty;

        public string ApiKey
        {
            get => _apiKey;
            set
            {
                _apiKey = value;
                OnPropertyChanged();
            }
        }

        public string ApiBaseUrl
        {
            get => _apiBaseUrl;
            set
            {
                _apiBaseUrl = value;
                OnPropertyChanged();
            }
        }

        public string ModelName
        {
            get => _modelName;
            set
            {
                _modelName = value;
                OnPropertyChanged();
            }
        }

        public int MaxTokens
        {
            get => _maxTokens;
            set
            {
                _maxTokens = value;
                OnPropertyChanged();
            }
        }

        public double Temperature
        {
            get => _temperature;
            set
            {
                _temperature = value;
                OnPropertyChanged();
            }
        }

        public string ProjectPath
        {
            get => _projectPath;
            set
            {
                if (_projectPath != value)
                {
                    _projectPath = value;
                    OnPropertyChanged();
                }
            }
        }

        private int _selectedSettingsIndex = 0;

        public int SelectedSettingsIndex
        {
            get => _selectedSettingsIndex;
            set
            {
                if (_selectedSettingsIndex != value)
                {
                    _selectedSettingsIndex = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(IsApiSettingsVisible));
                    OnPropertyChanged(nameof(IsProjectSettingsVisible));
                }
            }
        }

        public bool IsApiSettingsVisible => SelectedSettingsIndex == 0;
        public bool IsProjectSettingsVisible => SelectedSettingsIndex == 1;

        public ICommand BrowseProjectPathCommand { get; }
        public ICommand SaveCommand { get; }
        public ICommand CancelCommand { get; }
        public ICommand TestConnectionCommand { get; }

        public SettingsViewModel(IConfiguration configuration)
        {
            _configuration = configuration;
            LoadSettings();

            SaveCommand = new RelayCommand(SaveSettings);
            CancelCommand = new RelayCommand(() =>
            {
                // 关闭窗口的逻辑由窗口本身处理
            });
            TestConnectionCommand = new RelayCommand(async () => await TestConnectionAsync());

            BrowseProjectPathCommand = new RelayCommand(BrowseProjectPath);
        }

        private void BrowseProjectPath()
        {
            var dialog = new System.Windows.Forms.FolderBrowserDialog
            {
                Description = "选择工程文件夹",
                ShowNewFolderButton = true
            };

            if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                ProjectPath = dialog.SelectedPath;
            }
        }

        private void LoadSettings()
        {
            // 读取 DeepSeekSettings 部分
            ApiKey = _configuration["DeepSeekSettings:ApiKey"] ?? "";
            ApiBaseUrl = _configuration["DeepSeekSettings:ApiBaseUrl"] ?? "https://api.deepseek.com/chat/";
            ModelName = _configuration["DeepSeekSettings:ModelName"] ?? "completions";

            if (int.TryParse(_configuration["DeepSeekSettings:MaxTokens"], out int maxTokens))
                MaxTokens = maxTokens;

            if (double.TryParse(_configuration["DeepSeekSettings:Temperature"], out double temperature))
                Temperature = temperature;

            // 读取 ProjectSettings 部分
            ProjectPath = _configuration["ProjectSettings:projectPath"] ?? "";
        }

        public void SaveSettings()
        {
            try
            {
                // 创建新的配置字典
                var settings = new Dictionary<string, object>
                {
                    ["DeepSeekSettings"] = new Dictionary<string, object>
                    {
                        ["ApiKey"] = ApiKey,
                        ["ApiBaseUrl"] = ApiBaseUrl,
                        ["ModelName"] = ModelName,
                        ["MaxTokens"] = MaxTokens,
                        ["Temperature"] = Temperature
                    },
                    ["ProjectSettings"] = new Dictionary<string, object>
                    {
                        ["projectPath"] = ProjectPath
                    }
                };

                // 序列化为JSON
                var options = new JsonSerializerOptions
                {
                    WriteIndented = true,
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase // 保持小写字母开头
                };

                var json = JsonSerializer.Serialize(settings, options);
                File.WriteAllText("appsettings.json", json);

                MessageBox.Show("设置已保存成功！ 重启APP生效", "成功", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"保存设置失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async Task TestConnectionAsync()
        {
            try
            {
                // 这里实现测试API连接的方法
                MessageBox.Show("连接测试功能待实现", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"测试连接失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}