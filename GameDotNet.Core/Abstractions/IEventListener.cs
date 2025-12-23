namespace GameDotNet.Core.Abstractions;

public interface IEventListener
{
    void Configure(IEventRegistry registry);
}