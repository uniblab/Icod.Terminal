namespace Icod.Terminal;

using Icod.TermInfo;

/// <summary>
/// T18 traditional modified-key decoding for <see cref="TerminalInputDecoder"/>.
/// </summary>
internal sealed partial class TerminalInputDecoder {
	private void AddTraditionalModifiedKeyCapabilities(
		TerminalDescription terminal
	) {
		ArgumentNullException.ThrowIfNull( terminal );

		this.AddCapability(
			terminal,
			StringCapability.KeyShiftDeleteCharacter,
			TerminalInputEvent.FromKey(
				TerminalKey.Delete,
				TerminalKeyModifiers.Shift
			)
		);
		this.AddCapability(
			terminal,
			StringCapability.KeyShiftEnd,
			TerminalInputEvent.FromKey(
				TerminalKey.End,
				TerminalKeyModifiers.Shift
			)
		);
		this.AddCapability(
			terminal,
			StringCapability.KeyShiftHome,
			TerminalInputEvent.FromKey(
				TerminalKey.Home,
				TerminalKeyModifiers.Shift
			)
		);
		this.AddCapability(
			terminal,
			StringCapability.KeyShiftInsertCharacter,
			TerminalInputEvent.FromKey(
				TerminalKey.Insert,
				TerminalKeyModifiers.Shift
			)
		);
		this.AddCapability(
			terminal,
			StringCapability.KeyShiftLeft,
			TerminalInputEvent.FromKey(
				TerminalKey.Left,
				TerminalKeyModifiers.Shift
			)
		);
		this.AddCapability(
			terminal,
			StringCapability.KeyShiftNextPage,
			TerminalInputEvent.FromKey(
				TerminalKey.PageDown,
				TerminalKeyModifiers.Shift
			)
		);
		this.AddCapability(
			terminal,
			StringCapability.KeyShiftPreviousPage,
			TerminalInputEvent.FromKey(
				TerminalKey.PageUp,
				TerminalKeyModifiers.Shift
			)
		);
		this.AddCapability(
			terminal,
			StringCapability.KeyShiftRight,
			TerminalInputEvent.FromKey(
				TerminalKey.Right,
				TerminalKeyModifiers.Shift
			)
		);

		this.AddExtendedModifiedKeyCapability(
			terminal,
			"kUP",
			TerminalKey.Up,
			TerminalKeyModifiers.Shift
		);
		this.AddExtendedModifiedKeyCapability(
			terminal,
			"kDN",
			TerminalKey.Down,
			TerminalKeyModifiers.Shift
		);

		for ( int modifierParameter = 3; modifierParameter <= 8; modifierParameter++ ) {
			if ( !TryGetTraditionalModifiers(
				modifierParameter,
				out TerminalKeyModifiers modifiers
			) ) {
				continue;
			}

			char suffix = (char)( '0' + modifierParameter );
			this.AddExtendedModifiedKeyCapability(
				terminal,
				$"kUP{suffix}",
				TerminalKey.Up,
				modifiers
			);
			this.AddExtendedModifiedKeyCapability(
				terminal,
				$"kDN{suffix}",
				TerminalKey.Down,
				modifiers
			);
			this.AddExtendedModifiedKeyCapability(
				terminal,
				$"kLFT{suffix}",
				TerminalKey.Left,
				modifiers
			);
			this.AddExtendedModifiedKeyCapability(
				terminal,
				$"kRIT{suffix}",
				TerminalKey.Right,
				modifiers
			);
			this.AddExtendedModifiedKeyCapability(
				terminal,
				$"kHOM{suffix}",
				TerminalKey.Home,
				modifiers
			);
			this.AddExtendedModifiedKeyCapability(
				terminal,
				$"kEND{suffix}",
				TerminalKey.End,
				modifiers
			);
			this.AddExtendedModifiedKeyCapability(
				terminal,
				$"kPRV{suffix}",
				TerminalKey.PageUp,
				modifiers
			);
			this.AddExtendedModifiedKeyCapability(
				terminal,
				$"kNXT{suffix}",
				TerminalKey.PageDown,
				modifiers
			);
			this.AddExtendedModifiedKeyCapability(
				terminal,
				$"kIC{suffix}",
				TerminalKey.Insert,
				modifiers
			);
			this.AddExtendedModifiedKeyCapability(
				terminal,
				$"kDC{suffix}",
				TerminalKey.Delete,
				modifiers
			);
		}
	}

	private void AddTraditionalModifiedFunctionCapability(
		TerminalDescription terminal,
		StringCapability capability
	) {
		ArgumentNullException.ThrowIfNull( terminal );

		string? value = terminal.GetString( capability );
		if ( string.IsNullOrEmpty( value ) ) {
			return;
		}
		if ( !TryDecodeTraditionalModifiedFunctionSequence(
			value,
			out int functionKeyNumber,
			out TerminalKeyModifiers modifiers
		) ) {
			return;
		}

		this.AddSequence(
			EncodeCapability(
				value,
				capability
			),
			TerminalInputEvent.FromKey(
				TerminalKey.Function,
				modifiers,
				functionKeyNumber: functionKeyNumber
			),
			$"Terminal modified function-key capability '{capability}'"
		);
	}

	private void AddExtendedModifiedKeyCapability(
		TerminalDescription terminal,
		string name,
		TerminalKey key,
		TerminalKeyModifiers modifiers
	) {
		ArgumentNullException.ThrowIfNull( terminal );
		ArgumentException.ThrowIfNullOrWhiteSpace( name );
		if ( !Enum.IsDefined( key ) || TerminalKey.None == key ) {
			throw new ArgumentOutOfRangeException( nameof( key ) );
		}

		if ( !terminal.TryGetExtendedString(
			name,
			out string? value
		) || string.IsNullOrEmpty( value ) ) {
			return;
		}

		this.AddExtendedCapability(
			name,
			value,
			TerminalInputEvent.FromKey(
				key,
				modifiers
			)
		);
	}

	private static bool TryDecodeTraditionalModifiedFunctionSequence(
		string value,
		out int functionKeyNumber,
		out TerminalKeyModifiers modifiers
	) {
		ArgumentNullException.ThrowIfNull( value );

		functionKeyNumber = 0;
		modifiers = TerminalKeyModifiers.None;

		if ( 6 > value.Length
			|| '\u001b' != value[ 0 ]
			|| '[' != value[ 1 ] ) {
			return false;
		}

		int finalIndex = value.Length - 1;
		int separatorIndex = value.IndexOf(
			';',
			2
		);
		if ( 2 >= separatorIndex || separatorIndex >= finalIndex - 1 ) {
			return false;
		}
		if ( 0 <= value.IndexOf(
			';',
			separatorIndex + 1
		) ) {
			return false;
		}

		ReadOnlySpan<char> keyParameter = value.AsSpan(
			2,
			separatorIndex - 2
		);
		ReadOnlySpan<char> modifierParameter = value.AsSpan(
			separatorIndex + 1,
			finalIndex - separatorIndex - 1
		);
		if ( 1 != modifierParameter.Length
			|| !TryGetTraditionalModifiers(
				modifierParameter[ 0 ] - '0',
				out modifiers
			) ) {
			modifiers = TerminalKeyModifiers.None;
			return false;
		}

		char final = value[ finalIndex ];
		if ( 1 == keyParameter.Length && '1' == keyParameter[ 0 ] ) {
			functionKeyNumber = final switch {
				'P' => 1,
				'Q' => 2,
				'R' => 3,
				'S' => 4,
				_ => 0
			};
			if ( 0 != functionKeyNumber ) {
				return true;
			}
		}

		if ( '~' != final || !TryParseTwoDigitDecimal(
			keyParameter,
			out int tildeCode
		) ) {
			functionKeyNumber = 0;
			modifiers = TerminalKeyModifiers.None;
			return false;
		}

		functionKeyNumber = tildeCode switch {
			15 => 5,
			17 => 6,
			18 => 7,
			19 => 8,
			20 => 9,
			21 => 10,
			23 => 11,
			24 => 12,
			_ => 0
		};
		if ( 0 == functionKeyNumber ) {
			modifiers = TerminalKeyModifiers.None;
			return false;
		}

		return true;
	}

	private static bool TryParseTwoDigitDecimal(
		ReadOnlySpan<char> value,
		out int result
	) {
		result = 0;
		if ( 2 != value.Length
			|| value[ 0 ] is < '0' or > '9'
			|| value[ 1 ] is < '0' or > '9' ) {
			return false;
		}

		result = ( ( value[ 0 ] - '0' ) * 10 )
			+ ( value[ 1 ] - '0' );
		return true;
	}

	private static bool TryGetTraditionalModifiers(
		int modifierParameter,
		out TerminalKeyModifiers modifiers
	) {
		modifiers = modifierParameter switch {
			2 => TerminalKeyModifiers.Shift,
			3 => TerminalKeyModifiers.Alt,
			4 => TerminalKeyModifiers.Shift | TerminalKeyModifiers.Alt,
			5 => TerminalKeyModifiers.Control,
			6 => TerminalKeyModifiers.Shift | TerminalKeyModifiers.Control,
			7 => TerminalKeyModifiers.Alt | TerminalKeyModifiers.Control,
			8 => TerminalKeyModifiers.Shift
				| TerminalKeyModifiers.Alt
				| TerminalKeyModifiers.Control,
			_ => TerminalKeyModifiers.None
		};
		return TerminalKeyModifiers.None != modifiers;
	}
}
