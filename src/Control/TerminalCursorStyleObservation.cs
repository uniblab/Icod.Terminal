namespace Icod.Terminal;

/// <summary>
/// Represents one explicit live observation of terminal cursor-style support and state.
/// </summary>
public sealed class TerminalCursorStyleObservation {
	internal TerminalCursorStyleObservation(
		bool isSupported,
		TerminalCursorStyle? style
	) {
		if ( isSupported && !style.HasValue ) {
			throw new ArgumentNullException(
				nameof( style ),
				"A supported cursor-style observation must contain a semantic style."
			);
		}
		if ( !isSupported && style.HasValue ) {
			throw new ArgumentException(
				"An unsupported cursor-style observation cannot contain a semantic style.",
				nameof( style )
			);
		}
		if ( style.HasValue && !Enum.IsDefined( style.Value ) ) {
			throw new ArgumentOutOfRangeException(
				nameof( style ),
				style.Value,
				"The observed cursor style is not defined by the frozen 0.8 contract."
			);
		}

		this.IsSupported = isSupported;
		this.Style = style;
	}

	/// <summary>
	/// Gets whether the terminal explicitly reported DECSCUSR cursor-style state as supported.
	/// </summary>
	public bool IsSupported {
		get;
	}

	/// <summary>
	/// Gets the observed semantic cursor style when supported, or <see langword="null"/>
	/// when the terminal explicitly reports the DECRQSS request as unsupported.
	/// </summary>
	public TerminalCursorStyle? Style {
		get;
	}
}
