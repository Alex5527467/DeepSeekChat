using DeepSeekChat.Services;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace DeepSeekChat.Agent
{
    public static class AgentFactory
    {
        public static BaseAgent CreateAgent(string configFilePath,
                                          IMessageBus messageBus,
                                          ToolApiService toolApiService,
                                          ToolService toolService,
                                          CancellationToken cancellationToken = default)
        {
            try
            {
                // 直接从配置文件创建GenericAgent
                return new GenericAgent(
                    configFilePath,
                    messageBus,
                    toolApiService,
                    toolService,
                    cancellationToken);
            }
            catch (Exception ex)
            {
                // 记录错误并返回空Agent或抛出异常
                throw new InvalidOperationException($"Failed to create agent from config: {configFilePath}", ex);
            }
        }

        public class GenericAgent : BaseAgent
        {
            public GenericAgent(string configFilePath,
                              IMessageBus messageBus,
                              ToolApiService toolApiService,
                              ToolService toolService,
                              CancellationToken cancellationToken = default)
                : base(configFilePath, messageBus, toolApiService, toolService, cancellationToken)
            {
            }
        }

    }
}