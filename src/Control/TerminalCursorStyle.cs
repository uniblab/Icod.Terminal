namespace Icod.Terminal;

/// <summary>
/// Identifies one semantic terminal text-cursor style supported by the 0.8 contract.
/// </summary>
public enum TerminalCursorStyle {
	/// <summary>A blinking block cursor.</summary>
	BlinkingBlock,

	/// <summary>A steady block cursor.</summary>
	SteadyBlock,

	/// <summary>A blinking underline cursor.</summary>
	BlinkingUnderline,

	/// <summary>A steady underline cursor.</summary>
	SteadyUnderline,

	/// <summary>A blinking bar cursor, using the xterm DECSCUSR extension.</summary>
	BlinkingBar,

	/// <summary>A steady bar cursor, using the xterm DECSCUSR extension.</summary>
	SteadyBar
}

/// <summary>
/// Maps the frozen cursor-style semantic model to and from DECSCUSR status data.
/// </summary>
internal static class TerminalCursorStyleCodec {
	internal static int GetParameter(
		TerminalCursorStyle style
	) {
		if ( !Enum.IsDefined( style ) ) {
			throw new ArgumentOutOfRangeException(
				nameof( style ),
				style,
				"The cursor style is not defined by the frozen 0.8 contract."
			);
		}

		return style switch {
			TerminalCursorStyle.BlinkingBlock => 1,
			TerminalCursorStyle.SteadyBlock => 2,
			TerminalCursorStyle.BlinkingUnderline => 3,
			TerminalCursorStyle.SteadyUnderline => 4,
			TerminalCursorStyle.BlinkingBar => 5,
			TerminalCursorStyle.SteadyBar => 6,
			_ => throw new ArgumentOutOfRangeException(
				nameof( style ),
				style,
				"The cursor style is not defined by the frozen 0.8 contract."
			)
		};
	}

	internal static TerminalCursorStyle ParseStatusString(
		string statusString
	) {
		ArgumentNullException.ThrowIfNull( statusString );

		ReadOnlySpan<char> value = statusString.AsSpan();
		if ( 2 > value.Length
			|| ' ' != value[ ^2 ]
			|| 'q' != value[ ^1 ] ) {
			throw new FormatException(
				"A DECRQSS cursor-style status string must end with SP q."
			);
		}

		ReadOnlySpan<char> parameterText = value[ ..^2 ];
		if ( parameterText.IsEmpty ) {
			return TerminalCursorStyle.BlinkingBlock;
		}

		int parameter = 0;
		try {
			for ( int index = 0; index < parameterText.Length; index++ ) {
				char character = parameterText[ index ];
				if ( character is < '0' or > '9' ) {
					throw new FormatException(
						"A DECRQSS cursor-style status string may contain only one decimal parameter."
					);
				}

				parameter = checked(
					( parameter * 10 ) + ( character - '0' )
				);
			}
		} catch ( OverflowException exception ) {
			throw new FormatException(
				"The DECRQSS cursor-style parameter exceeds the supported numeric range.",
				exception
			);
		}

		return parameter switch {
			0 or 1 => TerminalCursorStyle.BlinkingBlock,
			2 => TerminalCursorStyle.SteadyBlock,
			3 => TerminalCursorStyle.BlinkingUnderline,
			4 => TerminalCursorStyle.SteadyUnderline,
			5 => TerminalCursorStyle.BlinkingBar,
			6 => TerminalCursorStyle.SteadyBar,
			_ => throw new FormatException(
				$"DECSCUSR parameter {parameter} is not a recognized semantic cursor style."
			)
		};
	}
}
