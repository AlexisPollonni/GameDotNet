using Assimp;
using Microsoft.Extensions.Logging;

namespace GameDotNet.Graphics.Services;

[RegisterSingleton<AssimpLoggerStream>]
internal sealed partial class AssimpLoggerStream(ILogger<AssimpLoggerStream> logger) : LogStream
{
    protected override void LogMessage(string msg, string userData)
    {
        LogAssimpMessage(logger, msg);

        base.LogMessage(msg, userData);
    }

    [LoggerMessage(LogLevel.Debug, "[Assimp] {assimpMessage}")]
    static partial void LogAssimpMessage(ILogger logger, string assimpMessage);
}