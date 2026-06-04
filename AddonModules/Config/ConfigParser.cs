namespace Rename.AddonModules.Config
{
    public class Parser
    {
        internal static Dictionary<string, object> GetConfig(string toolName)
        {
            Dictionary<string, object> root = [];
            root["Config"] = new Dictionary<string, object>();
            Stack<Dictionary<string, object>> stack = new();
            stack.Push(root);
            string filePath = $"{Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)}\\.dotnet\\tools\\ToolBoxConfigs\\{toolName}.config";
            string[] lines = File.ReadAllLines(filePath);
            string Description;
            string Default;
            List<string> Options;
            foreach (string line in lines)
            {
                string trimmedLine = line.TrimStart();
                int headerLineEnd = -1;
                if (!trimmedLine.StartsWith('/')) { headerLineEnd = trimmedLine.IndexOf(':'); }
                int commentLineStart = trimmedLine.IndexOf('/');
                int keyValueLineSeparator = trimmedLine.IndexOf('=');
                if (headerLineEnd != -1)
                {
                    string header = trimmedLine[..headerLineEnd];
                    var newDict = new Dictionary<string, object>();
                    stack.Peek()[header] = newDict;
                    stack.Push(newDict);
                    continue;
                }
                else if (commentLineStart != -1)
                {
                    Description = trimmedLine[(commentLineStart + 1)..trimmedLine.IndexOf('(')];
                    Description = Description.TrimStart();
                    Default = trimmedLine[trimmedLine.IndexOf(':')..trimmedLine.IndexOf(')')];
                    Options = trimmedLine[trimmedLine.IndexOf('[')..trimmedLine.IndexOf(']')];
                    continue;
                }
                else if (keyValueLineSeparator != -1)
                {
                    
                }
                else { continue; }
            }
        }
    }
}