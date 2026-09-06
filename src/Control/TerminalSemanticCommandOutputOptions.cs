namespace Icod.Terminal;

/// <summary>
/// Represents optional extended metadata for one OSC 133 command-output marker.
/// </summary>
/// <remarks>
/// The default value is valid and emits the portable bare OSC 133 <c>C</c> marker.
/// A non-null command line is published through <c>cmdline_url</c> using strict UTF-8
/// byte percent encoding. Publishing command-line metadata is explicit caller intent;
/// the library does not inspect shell history, capture process command lines, or redact secrets.
/// </remarks>
public readonly struct TerminalSemanticCommandOutputOptions {
	/// <summary>
	/// Initializes OSC 133 command-output metadata.
	/// </summary>
	/// <param name="commandLine">
	/// The command line to publish, or <see langword="null"/> to publish no command-line metadata.
	/// An empty string is distinct from <see langword="null"/> and publishes an explicitly empty
	/// <c>cmdline_url</c> value.
	/// </param>
	public TerminalSemanticCommandOutputOptions(
		string? commandLine = null
	) {
		this.CommandLine = commandLine;
	}

	/// <summary>
	/// Gets the exact caller-supplied command line to publish, or <see langword="null"/> when
	/// no command-line metadata should be emitted.
	/// </summary>
	/// <remarks>
	/// Command lines can contain credentials, tokens, private paths, host names, or other
	/// sensitive material. Percent encoding protects OSC framing; it does not provide confidentiality.
	/// </remarks>
	public string? CommandLine {
		get;
	}
}
