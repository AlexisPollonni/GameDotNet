namespace GameDotNet.Core.Tooling.Collections;

public interface ICompositeDisposable : ICollection<IDisposable>, IDisposable
{ }