using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace GameDotNet.Core.Tooling;

/// <summary>
/// Debug-only thread ownership guard. In Release builds this is an empty struct
/// with all methods inlined to no-ops.
///
/// Supports three usage patterns:
/// <list type="bullet">
///   <item><b>Scoped ownership</b> — Acquire/Release around a recording session (command buffers)</item>
///   <item><b>Thread affinity</b> — Bind to one thread for lifetime (command pools)</item>
///   <item><b>Write guard</b> — Serialize writes, reads are free (descriptor sets, images)</item>
/// </list>
/// </summary>
[StructLayout(LayoutKind.Auto)]
public struct ThreadOwnershipGuard //TODO: Review code and API
{
#if DEBUG
    private int _ownerThreadId; // 0 = unowned
    private int _acquired; // 0 = free, 1 = acquired
    private bool _affinity; // true = permanent binding, cannot release
    private string? _context; // name of the owning wrapper for diagnostics
#endif

    /// <summary>
    /// Sets the diagnostic context name (wrapper type + handle) for error messages.
    /// </summary>
    [Conditional("DEBUG")]
    public void SetContext(string context)
    {
#if DEBUG
        _context = context;
#endif
    }

    /// <summary>
    /// Claims exclusive ownership for the calling thread. Throws if already owned by another thread.
    /// Call <see cref="Release"/> when done.
    /// </summary>
    [Conditional("DEBUG")]
    public void Acquire([CallerMemberName] string caller = "")
    {
#if DEBUG
        var tid = Environment.CurrentManagedThreadId;
        var prev = Interlocked.CompareExchange(ref _ownerThreadId, tid, 0);

        if (prev != 0 && prev != tid)
            ThrowWrongThread(caller, prev);

        if (Interlocked.Exchange(ref _acquired, 1) != 0)
            ThrowInvalidState(caller, "Already acquired. Missing Release()?");
#endif
    }

    /// <summary>
    /// Releases scoped ownership. Must be called from the owning thread.
    /// </summary>
    [Conditional("DEBUG")]
    public void Release([CallerMemberName] string caller = "")
    {
#if DEBUG
        AssertOwner(caller);

        if (_affinity)
            ThrowInvalidState(
                caller,
                "Cannot release affinity-bound guard. Use scoped ownership instead."
            );

        Interlocked.Exchange(ref _acquired, 0);
        Volatile.Write(ref _ownerThreadId, 0);
#endif
    }

    /// <summary>
    /// Permanently binds this guard to the calling thread. Subsequent calls from
    /// other threads will throw in Debug. Cannot be unbound.
    /// </summary>
    [Conditional("DEBUG")]
    public void BindToCurrentThread([CallerMemberName] string caller = "")
    {
#if DEBUG
        var tid = Environment.CurrentManagedThreadId;
        var prev = Interlocked.CompareExchange(ref _ownerThreadId, tid, 0);

        if (prev != 0 && prev != tid)
            ThrowWrongThread(caller, prev);

        _affinity = true;
        Volatile.Write(ref _acquired, 1);
#endif
    }

    /// <summary>
    /// Asserts that no other thread currently holds write access, then claims it.
    /// Use with <see cref="ReleaseWrite"/> in a try/finally.
    /// </summary>
    [Conditional("DEBUG")]
    public void AcquireWrite([CallerMemberName] string caller = "")
    {
#if DEBUG
        var tid = Environment.CurrentManagedThreadId;

        if (Interlocked.CompareExchange(ref _acquired, 1, 0) != 0)
        {
            var owner = Volatile.Read(ref _ownerThreadId);
            if (owner != tid)
                ThrowWrongThread(caller, owner, "Concurrent write detected.");
            // Re-entrant write from same thread is allowed.
            return;
        }

        Volatile.Write(ref _ownerThreadId, tid);
#endif
    }

    [Conditional("DEBUG")]
    public void ReleaseWrite([CallerMemberName] string caller = "")
    {
#if DEBUG
        AssertOwner(caller);
        Interlocked.Exchange(ref _acquired, 0);
        Volatile.Write(ref _ownerThreadId, 0);
#endif
    }

    /// <summary>
    /// Validates that the calling thread is the current owner. Use in any
    /// wrapper method that requires exclusive access.
    /// </summary>
    [Conditional("DEBUG")]
    public void AssertOwned([CallerMemberName] string caller = "")
    {
#if DEBUG
        if (Volatile.Read(ref _acquired) == 0)
            ThrowInvalidState(
                caller,
                "Not currently acquired. Missing Acquire()/BindToCurrentThread()?"
            );

        AssertOwner(caller);
#endif
    }

#if DEBUG
    private void AssertOwner(string caller)
    {
        var tid = Environment.CurrentManagedThreadId;
        var owner = Volatile.Read(ref _ownerThreadId);
        if (owner != tid)
            ThrowWrongThread(caller, owner);
    }

    [DoesNotReturn]
    private void ThrowWrongThread(string caller, int ownerTid, string? extra = null)
    {
        var ctx = _context ?? "unknown";
        var msg =
            $"Thread ownership violation in [{ctx}].{caller}: "
            + $"current thread {Environment.CurrentManagedThreadId}, owner thread {ownerTid}.";
        if (extra is not null)
            msg = $"{msg} {extra}";
        throw new InvalidOperationException(msg);
    }

    [DoesNotReturn]
    private void ThrowInvalidState(string caller, string detail)
    {
        var ctx = _context ?? "unknown";
        throw new InvalidOperationException($"State violation in [{ctx}].{caller}: {detail}");
    }
#endif
}
