using System.Threading.Tasks;

public interface IToolService
{
    Task<object> ExecuteToolAsync(string toolName, string arguments);
    object ExecuteTool(string toolName, string arguments);
}
