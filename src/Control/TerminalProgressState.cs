namespace Icod.Terminal;

/// <summary>
/// Identifies one semantic determinate terminal-progress rendering state.
/// </summary>
public enum TerminalProgressState {
	/// <summary>Normal/default determinate progress.</summary>
	Normal,

	/// <summary>Determinate progress indicating an error condition.</summary>
	Error,

	/// <summary>
	/// Determinate progress requesting vendor-defined attention rendering.
	/// </summary>
	/// <remarks>
	/// Windows Terminal describes OSC 9;4 wire state 4 as warning while ConEmu
	/// describes it as paused. <see cref="Attention"/> is the neutral semantic name.
	/// </remarks>
	Attention
}
