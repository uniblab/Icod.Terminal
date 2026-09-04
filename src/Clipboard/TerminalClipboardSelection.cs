namespace Icod.Terminal;

/// <summary>
/// Identifies one terminal-managed clipboard or selection target.
/// </summary>
public enum TerminalClipboardSelection {
	/// <summary>The ordinary clipboard selection.</summary>
	Clipboard,

	/// <summary>The primary selection.</summary>
	Primary,

	/// <summary>The secondary selection.</summary>
	Secondary,

	/// <summary>The terminal's select selection.</summary>
	Select
}
