namespace GameDotNet.Core.Abstractions;

public interface IJobDependencyGraph
{
    IEnumerable<Type> GetDependencies(Type jobType);
}