using System.Collections.Generic;
using System.Threading.Tasks;

public interface IToolService
{
    Task<object> ExecuteToolAsync(string toolName, Dictionary<string, object> arguments);
    object ExecuteTool(string toolName, Dictionary<string, object> arguments);
}
