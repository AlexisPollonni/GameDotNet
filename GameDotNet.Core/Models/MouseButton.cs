using System.Diagnostics.CodeAnalysis;
using Intellenum;

namespace GameDotNet.Core.Models;

/// <summary>
/// Represents the indices of the mouse buttons.
/// </summary>
/// <remarks>
/// <para>
/// The number of buttons provided depends on the input backend currently being used.
/// </para>
/// </remarks>
[Intellenum]
[SuppressMessage("ReSharper", "MemberCanBePrivate.Global")]
public partial class MouseButton
{
    /// <summary>
    /// Indicates the input backend was unable to determine a button name for the button in question, or it does not support it.
    /// </summary>
    public static readonly MouseButton Unknown = new(-1);

    /// <summary>
    /// The left mouse button.
    /// </summary>
    public static readonly MouseButton Left;

    /// <summary>
    /// The right mouse button.
    /// </summary>
    public static readonly MouseButton Right;

    /// <summary>
    /// The middle mouse button.
    /// </summary>
    public static readonly MouseButton Middle;

    /// <summary>
    /// The fourth mouse button.
    /// </summary>
    public static readonly MouseButton Button4;

    /// <summary>
    /// The fifth mouse button.
    /// </summary>
    public static readonly MouseButton Button5;

    /// <summary>
    /// The sixth mouse button.
    /// </summary>
    public static readonly MouseButton Button6;

    /// <summary>
    /// The seventh mouse button.
    /// </summary>
    public static readonly MouseButton Button7;

    /// <summary>
    /// The eighth mouse button.
    /// </summary>
    public static readonly MouseButton Button8;

    /// <summary>
    /// The ninth mouse button.
    /// </summary>
    public static readonly MouseButton Button9;

    /// <summary>
    /// The tenth mouse button.
    /// </summary>
    public static readonly MouseButton Button10;

    /// <summary>
    /// The eleventh mouse button.
    /// </summary>
    public static readonly MouseButton Button11;

    /// <summary>
    /// The twelfth mouse button.
    /// </summary>
    public static readonly MouseButton Button12;
}