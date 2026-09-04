namespace Icod.Terminal;

/// <summary>
/// Identifies the native filesystem path grammar used when publishing a terminal current location.
/// </summary>
public enum TerminalLocationPathStyle {
	/// <summary>
	/// A POSIX absolute path beginning with <c>/</c>.
	/// </summary>
	Posix = 0,

	/// <summary>
	/// A fully-qualified Windows drive path such as <c>C:\Development</c>.
	/// </summary>
	WindowsDrive = 1,

	/// <summary>
	/// A Windows UNC path such as <c>\\server\share\directory</c>.
	/// </summary>
	WindowsUnc = 2
}
