namespace Icod.Terminal;

using System.Globalization;
using System.Text;

/// <summary>
/// Serializes and restores complete terminal-mode snapshots without applying
/// application policy or touching a terminal endpoint.
/// </summary>
public static class TerminalModeCodec {
	private const string WindowsInputPrefix = "win32-v1-input:";
	private const string WindowsOutputPrefix = "win32-v1-output:";

	/// <summary>
	/// Serializes a mode into a stable machine-readable representation.
	/// </summary>
	/// <param name="mode">The complete mode snapshot.</param>
	/// <returns>The serialized representation.</returns>
	public static string Serialize(
		TerminalModeSnapshot mode
	) {
		ArgumentNullException.ThrowIfNull( mode );

		return mode.Platform switch {
			TerminalPlatformKind.PosixTermios => SerializePosix( mode ),
			TerminalPlatformKind.WindowsConsole => SerializeWindows( mode ),
			_ => throw new ArgumentOutOfRangeException(
				nameof( mode ),
				mode.Platform,
				"The terminal platform is not recognized."
			)
		};
	}

	/// <summary>
	/// Restores serialized state against a live baseline snapshot.
	/// </summary>
	/// <param name="serialized">The serialized state.</param>
	/// <param name="baseline">A current snapshot for the destination endpoint.</param>
	/// <param name="restored">The restored complete snapshot when successful.</param>
	/// <param name="error">A controlled validation error when unsuccessful.</param>
	/// <returns><see langword="true"/> when the value was restored.</returns>
	public static bool TryRestore(
		string serialized,
		TerminalModeSnapshot baseline,
		out TerminalModeSnapshot? restored,
		out string? error
	) {
		ArgumentNullException.ThrowIfNull( serialized );
		ArgumentNullException.ThrowIfNull( baseline );

		if ( TerminalPlatformKind.PosixTermios == baseline.Platform ) {
			return TryRestorePosix(
				serialized,
				baseline,
				out restored,
				out error
			);
		}

		return TryRestoreWindows(
			serialized,
			baseline,
			out restored,
			out error
		);
	}

	private static string SerializePosix(
		TerminalModeSnapshot mode
	) {
		var builder = new StringBuilder();

		AppendHex( builder, mode.InputFlags );
		AppendSeparatorAndHex( builder, mode.OutputFlags );
		AppendSeparatorAndHex( builder, mode.ControlFlags );
		AppendSeparatorAndHex( builder, mode.LocalFlags );

		foreach ( byte value in mode.ControlCharacters ) {
			AppendSeparatorAndHex( builder, value );
		}

		return builder.ToString();
	}

	private static string SerializeWindows(
		TerminalModeSnapshot mode
	) {
		string prefix = TerminalConsoleDirection.Input == mode.ConsoleDirection
			? WindowsInputPrefix
			: WindowsOutputPrefix;

		return string.Concat(
			prefix,
			mode.ConsoleMode!.Value.ToString(
				"x8",
				CultureInfo.InvariantCulture
			)
		);
	}

	private static bool TryRestorePosix(
		string serialized,
		TerminalModeSnapshot baseline,
		out TerminalModeSnapshot? restored,
		out string? error
	) {
		string[] fields = serialized.Split(
			':',
			StringSplitOptions.None
		);
		int expectedCount = 4 + baseline.ControlCharacters.Count;

		if ( expectedCount != fields.Length ) {
			restored = null;
			error = string.Concat(
				"The POSIX terminal state contains ",
				fields.Length.ToString( CultureInfo.InvariantCulture ),
				" fields; this host requires ",
				expectedCount.ToString( CultureInfo.InvariantCulture ),
				"."
			);

			return false;
		}

		var flags = new ulong[ 4 ];

		for ( int index = 0; index < flags.Length; ++index ) {
			if ( !TryParseHex( fields[ index ], out flags[ index ] ) ) {
				restored = null;
				error = string.Concat(
					"Terminal flag field ",
					( index + 1 ).ToString( CultureInfo.InvariantCulture ),
					" is not a hexadecimal value."
				);

				return false;
			}

			if ( ( 32 == baseline.NativeFlagWidth )
				&& ( uint.MaxValue < flags[ index ] ) ) {
				restored = null;
				error = string.Concat(
					"Terminal flag field ",
					( index + 1 ).ToString( CultureInfo.InvariantCulture ),
					" exceeds this host's 32-bit terminal flag width."
				);

				return false;
			}
		}

		var characters = new byte[ baseline.ControlCharacters.Count ];

		for ( int index = 0; index < characters.Length; ++index ) {
			if ( !TryParseHex( fields[ 4 + index ], out ulong parsed )
				|| ( byte.MaxValue < parsed ) ) {
				restored = null;
				error = string.Concat(
					"Control-character field ",
					( index + 1 ).ToString( CultureInfo.InvariantCulture ),
					" is not a byte-sized hexadecimal value."
				);

				return false;
			}

			characters[ index ] = (byte)parsed;
		}

		restored = baseline.WithPosixSerializedState(
			flags[ 0 ],
			flags[ 1 ],
			flags[ 2 ],
			flags[ 3 ],
			characters
		);
		error = null;

		return true;
	}

	private static bool TryRestoreWindows(
		string serialized,
		TerminalModeSnapshot baseline,
		out TerminalModeSnapshot? restored,
		out string? error
	) {
		string expectedPrefix = TerminalConsoleDirection.Input == baseline.ConsoleDirection
			? WindowsInputPrefix
			: WindowsOutputPrefix;

		if ( !serialized.StartsWith(
			expectedPrefix,
			StringComparison.Ordinal
		) ) {
			restored = null;
			error = string.Concat(
				"The serialized console mode does not describe a ",
				TerminalConsoleDirection.Input == baseline.ConsoleDirection
					? "console input"
					: "console output",
				" handle."
			);

			return false;
		}

		string field = serialized[ expectedPrefix.Length.. ];

		if ( ( 8 != field.Length )
			|| !TryParseHex( field, out ulong parsed )
			|| ( uint.MaxValue < parsed ) ) {
			restored = null;
			error = "The serialized Windows console mode is not a 32-bit hexadecimal value.";

			return false;
		}

		restored = TerminalModeSnapshot.CreateWindowsConsole(
			baseline.ConsoleDirection!.Value,
			(uint)parsed
		);
		error = null;

		return true;
	}

	private static bool TryParseHex(
		string value,
		out ulong parsed
	) {
		bool tryParsed = ulong.TryParse(
			value,
			NumberStyles.AllowHexSpecifier,
			CultureInfo.InvariantCulture,
			out parsed
		);

		return !string.IsNullOrEmpty( value ) && tryParsed;
	}

	private static void AppendHex(
		StringBuilder builder,
		ulong value
	) {
		builder.Append(
			value.ToString(
				"x",
				CultureInfo.InvariantCulture
			)
		);
	}

	private static void AppendSeparatorAndHex(
		StringBuilder builder,
		ulong value
	) {
		builder.Append( ':' );
		AppendHex( builder, value );
	}
}