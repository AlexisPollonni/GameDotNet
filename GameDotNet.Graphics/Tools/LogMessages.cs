using Microsoft.Extensions.Logging;
using static Microsoft.Extensions.Logging.LogLevel;

namespace GameDotNet.Graphics.Tools;

internal static partial class LogMessages
{
    [LoggerMessage(Information, Message = "Compilation SUCCESS: {ShaderName}, {WarnNber} warnings")]
    public static partial void SucceededShaderCompilation(this ILogger l, string shaderName, uint warnNber);

    [LoggerMessage(Error, Message = "")]
    public static partial void ShaderEntryPointInvalidForInput(this ILogger l, Exception e);
}