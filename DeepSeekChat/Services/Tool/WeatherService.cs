using DeepSeekChat.Models;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace DeepSeekChat.Services
{
    public class WeatherService
    {
        public WeatherResult GetWeather(string arguments)
        {
            try
            {
                var args = JsonConvert.DeserializeObject<Dictionary<string, string>>(arguments);
                if (args != null && args.ContainsKey("location"))
                {
                    return GetWeatherData(args["location"]);
                }
            }
            catch (Exception ex)
            {
                return new WeatherResult
                {
                    Temperature = "未知",
                    Condition = "未知",
                    Humidity = "未知",
                    Wind = "未知",
                    Note = $"获取天气失败: {ex.Message}"
                };
            }

            return new WeatherResult
            {
                Temperature = "未知",
                Condition = "未知",
                Humidity = "未知",
                Wind = "未知",
                Note = "参数解析失败"
            };
        }

        public async Task<WeatherResult> GetWeatherAsync(string arguments)
        {
            // 异步版本，如果有实际API调用可以使用await
            return await Task.Run(() => GetWeather(arguments));
        }

        private WeatherResult GetWeatherData(string city)
        {
            var mockWeatherData = new Dictionary<string, WeatherResult>
            {
                { "北京", new WeatherResult { Temperature = "22°C", Condition = "晴朗", Humidity = "45%", Wind = "3级" } },
                { "上海", new WeatherResult { Temperature = "25°C", Condition = "多云", Humidity = "60%", Wind = "2级" } },
                { "广州", new WeatherResult { Temperature = "28°C", Condition = "小雨", Humidity = "75%", Wind = "1级" } },
                { "深圳", new WeatherResult { Temperature = "27°C", Condition = "阴天", Humidity = "70%", Wind = "2级" } },
                { "杭州", new WeatherResult { Temperature = "24°C", Condition = "晴转多云", Humidity = "55%", Wind = "2级" } }
            };

            if (mockWeatherData.ContainsKey(city))
            {
                return mockWeatherData[city];
            }

            return new WeatherResult
            {
                Temperature = "23°C",
                Condition = "天气数据未找到",
                Humidity = "未知",
                Wind = "未知",
                Note = $"未找到{city}的天气信息，显示默认数据"
            };
        }
    }
}