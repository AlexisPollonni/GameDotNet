namespace GameDotNet.Core.Tools.Containers;

public interface ICompositeDisposable : ICollection<IDisposable>, IDisposable
{ }