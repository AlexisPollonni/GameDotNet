using Assimp;

namespace GameDotNet.Graphics.Assets.Assimp;

internal sealed class SerilogLogStream : LogStream
{
    /// <summary>Constructs a new console logstream.</summary>
    public SerilogLogStream()
    { }

    /// <summary>Constructs a new console logstream.</summary>
    /// <param name="userData">User supplied data</param>
    public SerilogLogStream(string userData)
        : base(userData)
    { }

    /// <summary>Log a message to the console.</summary>
    /// <param name="msg">Message</param>
    /// <param name="userData">Userdata</param>
    protected override void LogMessage(string msg, string userData)
    {
        if (string.IsNullOrEmpty(userData))
            Serilog.Log.Debug("<Assimp> {AssimpMsg}", msg);
        else
            Serilog.Log.Debug("<Assimp> Data = {{ {Data} }}, {AssimpMsg}", userData, msg);
    }
}