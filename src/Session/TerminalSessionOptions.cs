namespace Icod.Terminal;

/// <summary>
/// Configures the input-state policy applied when opening a <see cref="TerminalSession"/>.
/// </summary>
public sealed class TerminalSessionOptions {
	/// <summary>
	/// Gets or initializes the semantic input discipline entered by the session.
	/// </summary>
	public TerminalInputMode InputMode {
		get;
		init;
	} = TerminalInputMode.CBreak;

	/// <summary>
	/// Gets or initializes whether host terminal input echo remains enabled.
	/// </summary>
	public bool EchoInput {
		get;
		init;
	}

	/// <summary>
	/// Gets or initializes whether the output endpoint must be an interactive terminal.
	/// </summary>
	/// <remarks>
	/// Input is always required to be interactive because a session captures and owns
	/// an input-mode transition. Set this property to <see langword="false"/> only for
	/// callers which intentionally combine terminal input with redirected output.
	/// </remarks>
	public bool RequireInteractiveOutput {
		get;
		init;
	} = true;

	internal void Validate() {
		if ( !Enum.IsDefined( this.InputMode ) ) {
			throw new ArgumentOutOfRangeException(
				nameof( this.InputMode ),
				this.InputMode,
				"The terminal input mode is not recognized."
			);
		}
	}
}
