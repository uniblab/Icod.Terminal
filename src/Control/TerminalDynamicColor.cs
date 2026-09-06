namespace Icod.Terminal;

/// <summary>
/// Identifies one semantic xterm dynamic-color slot supported by the 0.13 contract.
/// </summary>
public enum TerminalDynamicColor {
	/// <summary>The terminal's default text foreground color.</summary>
	DefaultForeground,
	/// <summary>The terminal's default text background color.</summary>
	DefaultBackground,
	/// <summary>The terminal's text-cursor color.</summary>
	TextCursor,
	/// <summary>The terminal's mouse-pointer foreground color.</summary>
	MouseForeground,
	/// <summary>The terminal's mouse-pointer background color.</summary>
	MouseBackground,
	/// <summary>The terminal's highlight background color.</summary>
	HighlightBackground,
	/// <summary>The terminal's highlight foreground color.</summary>
	HighlightForeground
}
