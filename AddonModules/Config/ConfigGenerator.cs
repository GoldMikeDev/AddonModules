namespace Rename.AddonModules.Config
{
    public class Generator
    {
        public static void CreateFile(string toolName)
        {
            string filePath = $"{Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)}\\.dotnet\\tools\\ToolBoxConfigs\\{toolName}.config";
            if (File.Exists(filePath)) { return; }
            Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);
            File.WriteAllLines(filePath, Schema.GetSchema());
        }
    }
}