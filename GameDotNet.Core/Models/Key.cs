using System.Diagnostics.CodeAnalysis;
using Intellenum;

namespace GameDotNet.Core.Models;

/// <summary>
/// Represents keyboard keys.
/// </summary>
[Intellenum]
[SuppressMessage("ReSharper", "MemberCanBePrivate.Global")]
public partial class Key
{
    /// <summary>
    /// An unknown key.
    /// </summary>
    public static readonly Key Unknown = new(-1);

    /// <summary>
    /// The spacebar key.
    /// </summary>
    public static readonly Key Space = new(32);

    /// <summary>
    /// The apostrophe key.
    /// </summary>
    public static readonly Key Apostrophe = new(39 /* ' */);

    /// <summary>
    /// The comma key.
    /// </summary>
    public static readonly Key Comma = new(44 /* , */);

    /// <summary>
    /// The minus key.
    /// </summary>
    public static readonly Key Minus = new(45 /* - */);

    /// <summary>
    /// The period key.
    /// </summary>
    public static readonly Key Period = new(46 /* . */);

    /// <summary>
    /// The slash key.
    /// </summary>
    public static readonly Key Slash = new(47 /* / */);

    /// <summary>
    /// The 0 key.
    /// </summary>
    public static readonly Key Number0 = new(48);

    /// <summary>
    /// The 0 key; alias for <see cref="Number0"/>
    /// </summary>
    public static readonly Key D0 = Number0;

    /// <summary>
    /// The 1 key.
    /// </summary>
    public static readonly Key Number1 = new(49);

    /// <summary>
    /// The 2 key.
    /// </summary>
    public static readonly Key Number2 = new(50);

    /// <summary>
    /// The 3 key.
    /// </summary>
    public static readonly Key Number3 = new(51);

    /// <summary>
    /// The 4 key.
    /// </summary>
    public static readonly Key Number4 = new(52);

    /// <summary>
    /// The 5 key.
    /// </summary>
    public static readonly Key Number5 = new(53);

    /// <summary>
    /// The 6 key.
    /// </summary>
    public static readonly Key Number6 = new(54);

    /// <summary>
    /// The 7 key.
    /// </summary>
    public static readonly Key Number7 = new(55);

    /// <summary>
    /// The 8 key.
    /// </summary>
    public static readonly Key Number8 = new(56);

    /// <summary>
    /// The 9 key.
    /// </summary>
    public static readonly Key Number9 = new(57);

    /// <summary>
    /// The semicolon key.
    /// </summary>
    public static readonly Key Semicolon = new(59 /* ; */);

    /// <summary>
    /// The equal key.
    /// </summary>
    public static readonly Key Equal = new(61 /* = */);

    /// <summary>
    /// The A key.
    /// </summary>
    public static readonly Key A = new(65);

    /// <summary>
    /// The B key.
    /// </summary>
    public static readonly Key B = new(66);

    /// <summary>
    /// The C key.
    /// </summary>
    public static readonly Key C = new(67);

    /// <summary>
    /// The D key.
    /// </summary>
    public static readonly Key D = new(68);

    /// <summary>
    /// The E key.
    /// </summary>
    public static readonly Key E = new(69);

    /// <summary>
    /// The F key.
    /// </summary>
    public static readonly Key F = new(70);

    /// <summary>
    /// The G key.
    /// </summary>
    public static readonly Key G = new(71);

    /// <summary>
    /// The H key.
    /// </summary>
    public static readonly Key H = new(72);

    /// <summary>
    /// The I key.
    /// </summary>
    public static readonly Key I = new(73);

    /// <summary>
    /// The J key.
    /// </summary>
    public static readonly Key J = new(74);

    /// <summary>
    /// The K key.
    /// </summary>
    public static readonly Key K = new(75);

    /// <summary>
    /// The L key.
    /// </summary>
    public static readonly Key L = new(76);

    /// <summary>
    /// The M key.
    /// </summary>
    public static readonly Key M = new(77);

    /// <summary>
    /// The N key.
    /// </summary>
    public static readonly Key N = new(78);

    /// <summary>
    /// The O key.
    /// </summary>
    public static readonly Key O = new(79);

    /// <summary>
    /// The P key.
    /// </summary>
    public static readonly Key P = new(80);

    /// <summary>
    /// The Q key.
    /// </summary>
    public static readonly Key Q = new(81);

    /// <summary>
    /// The R key.
    /// </summary>
    public static readonly Key R = new(82);

    /// <summary>
    /// The S key.
    /// </summary>
    public static readonly Key S = new(83);

    /// <summary>
    /// The T key.
    /// </summary>
    public static readonly Key T = new(84);

    /// <summary>
    /// The U key.
    /// </summary>
    public static readonly Key U = new(85);

    /// <summary>
    /// The V key.
    /// </summary>
    public static readonly Key V = new(86);

    /// <summary>
    /// The W key.
    /// </summary>
    public static readonly Key W = new(87);

    /// <summary>
    /// The X key.
    /// </summary>
    public static readonly Key X = new(88);

    /// <summary>
    /// The Y key.
    /// </summary>
    public static readonly Key Y = new(89);

    /// <summary>
    /// The Z key.
    /// </summary>
    public static readonly Key Z = new(90);

    /// <summary>
    /// The left bracket(opening bracket) key.
    /// </summary>
    public static readonly Key LeftBracket = new(91 /* [ */);

    /// <summary>
    /// The backslash.
    /// </summary>
    public static readonly Key BackSlash = new(92 /* \ */);

    /// <summary>
    /// The right bracket(closing bracket) key.
    /// </summary>
    public static readonly Key RightBracket = new(93 /* ] */);

    /// <summary>
    /// The grave accent key.
    /// </summary>
    public static readonly Key GraveAccent = new(96 /* ` */);

    /// <summary>
    /// Non US keyboard layout key 1.
    /// </summary>
    public static readonly Key World1 = new(161 /* non-US #1 */);

    /// <summary>
    /// Non US keyboard layout key 2.
    /// </summary>
    public static readonly Key World2 = new(162 /* non-US #2 */);

    /// <summary>
    /// The escape key.
    /// </summary>
    public static readonly Key Escape = new(256);

    /// <summary>
    /// The enter key.
    /// </summary>
    public static readonly Key Enter = new(257);

    /// <summary>
    /// The tab key.
    /// </summary>
    public static readonly Key Tab = new(258);

    /// <summary>
    /// The backspace key.
    /// </summary>
    public static readonly Key Backspace = new(259);

    /// <summary>
    /// The insert key.
    /// </summary>
    public static readonly Key Insert = new(260);

    /// <summary>
    /// The delete key.
    /// </summary>
    public static readonly Key Delete = new(261);

    /// <summary>
    /// The right arrow key.
    /// </summary>
    public static readonly Key Right = new(262);

    /// <summary>
    /// The left arrow key.
    /// </summary>
    public static readonly Key Left = new(263);

    /// <summary>
    /// The down arrow key.
    /// </summary>
    public static readonly Key Down = new(264);

    /// <summary>
    /// The up arrow key.
    /// </summary>
    public static readonly Key Up = new(265);

    /// <summary>
    /// The page up key.
    /// </summary>
    public static readonly Key PageUp = new(266);

    /// <summary>
    /// The page down key.
    /// </summary>
    public static readonly Key PageDown = new(267);

    /// <summary>
    /// The home key.
    /// </summary>
    public static readonly Key Home = new(268);

    /// <summary>
    /// The end key.
    /// </summary>
    public static readonly Key End = new(269);

    /// <summary>
    /// The caps lock key.
    /// </summary>
    public static readonly Key CapsLock = new(280);

    /// <summary>
    /// The scroll lock key.
    /// </summary>
    public static readonly Key ScrollLock = new(281);

    /// <summary>
    /// The num lock key.
    /// </summary>
    public static readonly Key NumLock = new(282);

    /// <summary>
    /// The print screen key.
    /// </summary>
    public static readonly Key PrintScreen = new(283);

    /// <summary>
    /// The pause key.
    /// </summary>
    public static readonly Key Pause = new(284);

    /// <summary>
    /// The F1 key.
    /// </summary>
    public static readonly Key F1 = new(290);

    /// <summary>
    /// The F2 key.
    /// </summary>
    public static readonly Key F2 = new(291);

    /// <summary>
    /// The F3 key.
    /// </summary>
    public static readonly Key F3 = new(292);

    /// <summary>
    /// The F4 key.
    /// </summary>
    public static readonly Key F4 = new(293);

    /// <summary>
    /// The F5 key.
    /// </summary>
    public static readonly Key F5 = new(294);

    /// <summary>
    /// The F6 key.
    /// </summary>
    public static readonly Key F6 = new(295);

    /// <summary>
    /// The F7 key.
    /// </summary>
    public static readonly Key F7 = new(296);

    /// <summary>
    /// The F8 key.
    /// </summary>
    public static readonly Key F8 = new(297);

    /// <summary>
    /// The F9 key.
    /// </summary>
    public static readonly Key F9 = new(298);

    /// <summary>
    /// The F10 key.
    /// </summary>
    public static readonly Key F10 = new(299);

    /// <summary>
    /// The F11 key.
    /// </summary>
    public static readonly Key F11 = new(300);

    /// <summary>
    /// The F12 key.
    /// </summary>
    public static readonly Key F12 = new(301);

    /// <summary>
    /// The F13 key.
    /// </summary>
    public static readonly Key F13 = new(302);

    /// <summary>
    /// The F14 key.
    /// </summary>
    public static readonly Key F14 = new(303);

    /// <summary>
    /// The F15 key.
    /// </summary>
    public static readonly Key F15 = new(304);

    /// <summary>
    /// The F16 key.
    /// </summary>
    public static readonly Key F16 = new(305);

    /// <summary>
    /// The F17 key.
    /// </summary>
    public static readonly Key F17 = new(306);

    /// <summary>
    /// The F18 key.
    /// </summary>
    public static readonly Key F18 = new(307);

    /// <summary>
    /// The F19 key.
    /// </summary>
    public static readonly Key F19 = new(308);

    /// <summary>
    /// The F20 key.
    /// </summary>
    public static readonly Key F20 = new(309);

    /// <summary>
    /// The F21 key.
    /// </summary>
    public static readonly Key F21 = new(310);

    /// <summary>
    /// The F22 key.
    /// </summary>
    public static readonly Key F22 = new(311);

    /// <summary>
    /// The F23 key.
    /// </summary>
    public static readonly Key F23 = new(312);

    /// <summary>
    /// The F24 key.
    /// </summary>
    public static readonly Key F24 = new(313);

    /// <summary>
    /// The F25 key.
    /// </summary>
    public static readonly Key F25 = new(314);

    /// <summary>
    /// The 0 key on the key pad.
    /// </summary>
    public static readonly Key Keypad0 = new(320);

    /// <summary>
    /// The 1 key on the key pad.
    /// </summary>
    public static readonly Key Keypad1 = new(321);

    /// <summary>
    /// The 2 key on the key pad.
    /// </summary>
    public static readonly Key Keypad2 = new(322);

    /// <summary>
    /// The 3 key on the key pad.
    /// </summary>
    public static readonly Key Keypad3 = new(323);

    /// <summary>
    /// The 4 key on the key pad.
    /// </summary>
    public static readonly Key Keypad4 = new(324);

    /// <summary>
    /// The 5 key on the key pad.
    /// </summary>
    public static readonly Key Keypad5 = new(325);

    /// <summary>
    /// The 6 key on the key pad.
    /// </summary>
    public static readonly Key Keypad6 = new(326);

    /// <summary>
    /// The 7 key on the key pad.
    /// </summary>
    public static readonly Key Keypad7 = new(327);

    /// <summary>
    /// The 8 key on the key pad.
    /// </summary>
    public static readonly Key Keypad8 = new(328);

    /// <summary>
    /// The 9 key on the key pad.
    /// </summary>
    public static readonly Key Keypad9 = new(329);

    /// <summary>
    /// The decimal key on the key pad.
    /// </summary>
    public static readonly Key KeypadDecimal = new(330);

    /// <summary>
    /// The divide key on the key pad.
    /// </summary>
    public static readonly Key KeypadDivide = new(331);

    /// <summary>
    /// The multiply key on the key pad.
    /// </summary>
    public static readonly Key KeypadMultiply = new(332);

    /// <summary>
    /// The subtract key on the key pad.
    /// </summary>
    public static readonly Key KeypadSubtract = new(333);

    /// <summary>
    /// The add key on the key pad.
    /// </summary>
    public static readonly Key KeypadAdd = new(334);

    /// <summary>
    /// The enter key on the key pad.
    /// </summary>
    public static readonly Key KeypadEnter = new(335);

    /// <summary>
    /// The equal key on the key pad.
    /// </summary>
    public static readonly Key KeypadEqual = new(336);

    /// <summary>
    /// The left shift key.
    /// </summary>
    public static readonly Key ShiftLeft = new(340);

    /// <summary>
    /// The left control key.
    /// </summary>
    public static readonly Key ControlLeft = new(341);

    /// <summary>
    /// The left alt key.
    /// </summary>
    public static readonly Key AltLeft = new(342);

    /// <summary>
    /// The left super key.
    /// </summary>
    public static readonly Key SuperLeft = new(343);

    /// <summary>
    /// The right shift key.
    /// </summary>
    public static readonly Key ShiftRight = new(344);

    /// <summary>
    /// The right control key.
    /// </summary>
    public static readonly Key ControlRight = new(345);

    /// <summary>
    /// The right alt key.
    /// </summary>
    public static readonly Key AltRight = new(346);

    /// <summary>
    /// The right super key.
    /// </summary>
    public static readonly Key SuperRight = new(347);

    /// <summary>
    /// The menu key.
    /// </summary>
    public static readonly Key Menu = new(348);
}