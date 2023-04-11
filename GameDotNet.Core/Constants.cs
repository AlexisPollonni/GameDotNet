namespace GameDotNet.Core;

public static class Constants
{
    public const string EngineName = "GamesDotNet";

    public static string LogsDirectoryPath =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "GamesDotNet", "Logs");
}