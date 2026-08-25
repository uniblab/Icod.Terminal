namespace Icod.Terminal;

using System.Text;
using Icod.TermInfo;

/// <summary>
/// Configures terminal identity, input state, output setup, and application text
/// policy applied when opening a <see cref="TerminalSession"/>.
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

	/// <summary>
	/// Gets or initializes an explicit terminal-description override.
	/// </summary>
	/// <remarks>
	/// When supplied, no terminal-name lookup is performed. This option is mutually
	/// exclusive with <see cref="TerminalName"/>.
	/// </remarks>
	public TerminalDescription? TerminalOverride {
		get;
		init;
	}

	/// <summary>
	/// Gets or initializes an explicit terminal name to resolve instead of the
	/// process <c>TERM</c> value.
	/// </summary>
	public string? TerminalName {
		get;
		init;
	}

	/// <summary>
	/// Gets or initializes the terminal database used for named resolution.
	/// </summary>
	/// <remarks>
	/// When omitted, each session composes system terminfo discovery followed by
	/// <see cref="Icod.TermInfo.TerminalDatabase.BuiltIn"/>.
	/// </remarks>
	public TerminalDatabase? TerminalDatabase {
		get;
		init;
	}

	/// <summary>
	/// Gets or initializes whether host output mode should be configured for the
	/// selected terminal protocol when required.
	/// </summary>
	/// <remarks>
	/// For system-backed Windows sessions this acquires the reversible
	/// <see cref="WindowsVirtualTerminal"/> output lease for process standard output
	/// or standard error. Custom providers retain responsibility for any equivalent
	/// transport setup. Set this to
	/// <see langword="false"/> only when a caller-owned output endpoint is already
	/// configured.
	/// </remarks>
	public bool ConfigureOutput {
		get;
		init;
	} = true;

	/// <summary>
	/// Gets or initializes the encoding used for application text written through
	/// <see cref="TerminalSession.WriteTextAsync"/>.
	/// </summary>
	/// <remarks>
	/// The default is strict UTF-8 without a byte-order mark. Terminfo capability
	/// strings do not use this encoding; their one-byte protocol representation is
	/// preserved independently.
	/// </remarks>
	public Encoding ApplicationEncoding {
		get;
		init;
	} = new UTF8Encoding(
		encoderShouldEmitUTF8Identifier: false,
		throwOnInvalidBytes: true
	);

	/// <summary>
	/// Gets or initializes how terminfo padding directives are honored by session
	/// capability output.
	/// </summary>
	public PaddingMode CapabilityPaddingMode {
		get;
		init;
	} = PaddingMode.Delay;

	/// <summary>
	/// Gets or initializes an optional terminfo delay provider for capability output.
	/// </summary>
	public ITermInfoDelayProvider? CapabilityDelayProvider {
		get;
		init;
	}

	internal void Validate() {
		if ( !Enum.IsDefined( this.InputMode ) ) {
			throw new ArgumentOutOfRangeException(
				nameof( this.InputMode ),
				this.InputMode,
				"The terminal input mode is not recognized."
			);
		}
		if ( !Enum.IsDefined( this.CapabilityPaddingMode ) ) {
			throw new ArgumentOutOfRangeException(
				nameof( this.CapabilityPaddingMode ),
				this.CapabilityPaddingMode,
				"The terminal capability padding mode is not recognized."
			);
		}
		if ( this.ApplicationEncoding is null ) {
			throw new ArgumentNullException( nameof( this.ApplicationEncoding ) );
		}
		if ( this.TerminalName is not null
			&& string.IsNullOrWhiteSpace( this.TerminalName ) ) {
			throw new ArgumentException(
				"An explicit terminal name cannot be empty or whitespace.",
				nameof( this.TerminalName )
			);
		}
		if ( this.TerminalOverride is not null
			&& this.TerminalName is not null ) {
			throw new ArgumentException(
				"TerminalOverride and TerminalName cannot both be supplied."
			);
		}
	}
}
