using DeepSeekChat.Agent;
using DeepSeekChat.Models;
using DeepSeekChat.Services;
using DeepSeekChat.ViewModels;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;

namespace DeepSeekChat
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // 创建配置
            var configuration = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory()) // 确保工作目录正确
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                .Build();

            // 配置依赖注入
            string apiKey = configuration["DeepSeekSettings:ApiKey"];

            var apiService = new ChatService(apiKey);
            var msgBus = new InMemoryMessageBus();
            var toolService = new ToolService(msgBus);
            var toolApiService = new ToolApiService(apiKey);
            var windowService = new AgentWindowService();
            var mainViewModel = new MainViewModel(apiService, 
                toolService, 
                msgBus, 
                toolApiService, 
                windowService,
                configuration);

            var mainWindow = new MainWindow
            {
                DataContext = mainViewModel
            };

            mainWindow.Show();
        }
    }
}
