namespace Icod.Terminal;

using System.Globalization;
using System.Text;

internal sealed partial class TerminalInputDecoder {
	private const int MaximumModernKeyboardFrameBytes = 4_096;
	private const int MaximumModernKeyboardParameters = 64;
	private const int MaximumAssociatedTextScalars = 32;

	private readonly Queue<TerminalInputEvent> pendingModernKeyboardEvents = new();

	private async ValueTask<TerminalInputEvent?> TryReadModernKeyboardEventAsync(
		CancellationToken cancellationToken
	) {
		if ( 2 > this.bufferedBytes.Count
			|| EscapeByte != this.bufferedBytes[ 0 ]
			|| (byte)'[' != this.bufferedBytes[ 1 ] ) {
			return null;
		}

		while ( true ) {
			int finalIndex = FindCsiFinalIndex( this.bufferedBytes );
			if ( 0 <= finalIndex ) {
				byte finalByte = this.bufferedBytes[ finalIndex ];
				if ( finalByte is not (byte)'u' and not (byte)'~' ) {
					return null;
				}

				byte[] frame = this.bufferedBytes.GetRange(
					0,
					finalIndex + 1
				).ToArray();

				TerminalInputEvent? inputEvent;
				IReadOnlyList<TerminalInputEvent>? additionalEvents = null;
				bool decoded = (byte)'u' == finalByte
					? TryDecodeKittyCsiUFrame(
						frame,
						out inputEvent,
						out additionalEvents
					)
					: TryDecodeXtermModifyOtherKeysFrame(
						frame,
						out inputEvent
					)
				;
				if ( !decoded ) {
					return null;
				}

				this.Consume( frame.Length );
				if ( additionalEvents is not null ) {
					List<byte> pendingTextBytes = [];
					foreach ( TerminalInputEvent additional in additionalEvents ) {
						Rune character = additional.Character
							?? throw new InvalidOperationException(
								"A pending Kitty pure-text event must contain one Unicode scalar."
							);
						pendingTextBytes.AddRange(
							Encoding.UTF8.GetBytes( character.ToString() )
						);
					}
					if ( 0 < pendingTextBytes.Count ) {
						this.bufferedBytes.InsertRange( 0, pendingTextBytes );
					}
				}
				return inputEvent;
			}

			if ( this.bufferedBytes.Count >= MaximumModernKeyboardFrameBytes ) {
				this.Consume( Math.Min( this.bufferedBytes.Count, MaximumModernKeyboardFrameBytes ) );
				return null;
			}

			if ( this.endOfInput ) {
				return null;
			}

			if ( !await this.ReadMoreWithinEscapeWindowAsync(
				cancellationToken
			).ConfigureAwait( false ) ) {
				return null;
			}
		}
	}

	private static bool TryDecodeXtermModifyOtherKeysFrame(
		ReadOnlySpan<byte> frame,
		out TerminalInputEvent? inputEvent
	) {
		inputEvent = null;
		if ( 8 > frame.Length
			|| EscapeByte != frame[ 0 ]
			|| (byte)'[' != frame[ 1 ]
			|| (byte)'~' != frame[ ^1 ] ) {
			return false;
		}

		string body = Encoding.ASCII.GetString( frame[ 2..^1 ] );
		string[] parameters = body.Split( ';' );
		if ( 3 != parameters.Length
			|| !string.Equals( parameters[ 0 ], "27", StringComparison.Ordinal )
			|| !TryParsePositiveInteger( parameters[ 1 ], out int encodedModifiers )
			|| !TryMapXtermModifiers( encodedModifiers, out TerminalKeyModifiers modifiers )
			|| !TryParseRune( parameters[ 2 ], out Rune character ) ) {
			return false;
		}

		TerminalKey key = MapUnicodeKeyIdentity( character );
		inputEvent = TerminalKey.Character == key
			? TerminalInputEvent.FromKey(
				TerminalKey.Character,
				modifiers,
				character,
				keyPhase: TerminalKeyEventPhase.Press
			)
			: TerminalInputEvent.FromKey(
				key,
				modifiers,
				keyPhase: TerminalKeyEventPhase.Press
			)
		;
		return true;
	}

	private static bool TryDecodeKittyCsiUFrame(
		ReadOnlySpan<byte> frame,
		out TerminalInputEvent? inputEvent,
		out IReadOnlyList<TerminalInputEvent>? additionalEvents
	) {
		inputEvent = null;
		additionalEvents = null;
		if ( 4 > frame.Length
			|| EscapeByte != frame[ 0 ]
			|| (byte)'[' != frame[ 1 ]
			|| (byte)'u' != frame[ ^1 ] ) {
			return false;
		}

		string body = Encoding.ASCII.GetString( frame[ 2..^1 ] );
		string[] parameters = body.Split( ';' );
		if ( 0 == parameters.Length || MaximumModernKeyboardParameters < parameters.Length ) {
			return false;
		}

		string[] keyParts = parameters[ 0 ].Split( ':' );
		if ( 0 == keyParts.Length || 3 < keyParts.Length ) {
			return false;
		}
		if ( !TryParseNonNegativeInteger( keyParts[ 0 ], out int keyCode ) ) {
			return false;
		}

		Rune? shiftedCharacter = null;
		Rune? baseLayoutCharacter = null;
		if ( 2 <= keyParts.Length && 0 < keyParts[ 1 ].Length ) {
			if ( !TryParseRune( keyParts[ 1 ], out Rune shifted ) ) {
				return false;
			}
			shiftedCharacter = shifted;
		}
		if ( 3 == keyParts.Length && 0 < keyParts[ 2 ].Length ) {
			if ( !TryParseRune( keyParts[ 2 ], out Rune baseLayout ) ) {
				return false;
			}
			baseLayoutCharacter = baseLayout;
		}

		TerminalKeyModifiers modifiers = TerminalKeyModifiers.None;
		TerminalKeyEventPhase phase = TerminalKeyEventPhase.Press;
		if ( 2 <= parameters.Length && 0 < parameters[ 1 ].Length ) {
			string[] modifierParts = parameters[ 1 ].Split( ':' );
			if ( 2 < modifierParts.Length
				|| !TryParsePositiveInteger( modifierParts[ 0 ], out int encodedModifiers )
				|| !TryMapKittyModifiers( encodedModifiers, out modifiers ) ) {
				return false;
			}
			if ( 2 == modifierParts.Length ) {
				if ( !TryParsePositiveInteger( modifierParts[ 1 ], out int eventType )
					|| !TryMapKittyEventPhase( eventType, out phase ) ) {
					return false;
				}
			}
		}

		string? associatedText = null;
		List<Rune>? associatedRunes = null;
		if ( 3 <= parameters.Length && 0 < parameters[ 2 ].Length ) {
			string[] textParts = parameters[ 2 ].Split( ':' );
			if ( MaximumAssociatedTextScalars < textParts.Length ) {
				return false;
			}
			associatedRunes = new List<Rune>( textParts.Length );
			StringBuilder text = new();
			foreach ( string textPart in textParts ) {
				if ( !TryParseRune( textPart, out Rune rune ) || IsForbiddenAssociatedTextControl( rune ) ) {
					return false;
				}
				associatedRunes.Add( rune );
				text.Append( rune.ToString() );
			}
			associatedText = text.ToString();
		}

		if ( 3 < parameters.Length ) {
			for ( int index = 3; index < parameters.Length; ++index ) {
				if ( 0 < parameters[ index ].Length ) {
					return false;
				}
			}
		}

		if ( 0 == keyCode ) {
			if ( associatedRunes is null || 0 == associatedRunes.Count ) {
				return false;
			}
			inputEvent = TerminalInputEvent.FromText( associatedRunes[ 0 ] );
			if ( 1 < associatedRunes.Count ) {
				List<TerminalInputEvent> remaining = new( associatedRunes.Count - 1 );
				for ( int index = 1; index < associatedRunes.Count; ++index ) {
					remaining.Add( TerminalInputEvent.FromText( associatedRunes[ index ] ) );
				}
				additionalEvents = remaining;
			}
			return true;
		}

		if ( TryMapKittyFunctionalKey(
			keyCode,
			out TerminalKey key,
			out int? functionKeyNumber
		) ) {
			inputEvent = TerminalInputEvent.FromKey(
				key,
				modifiers,
				functionKeyNumber: functionKeyNumber,
				keyPhase: phase,
				associatedText: associatedText
			);
			return true;
		}

		if ( IsKittyPrivateUseCode( keyCode ) ) {
			inputEvent = TerminalInputEvent.FromKey(
				TerminalKey.Unrecognized,
				modifiers,
				keyPhase: phase,
				associatedText: associatedText
			);
			return true;
		}

		if ( !TryCreateRune( keyCode, out Rune character ) ) {
			return false;
		}

		TerminalKey semanticKey = MapUnicodeKeyIdentity( character );
		if ( TerminalKey.Character == semanticKey ) {
			inputEvent = TerminalInputEvent.FromKey(
				TerminalKey.Character,
				modifiers,
				character,
				keyPhase: phase,
				shiftedCharacter: shiftedCharacter,
				baseLayoutCharacter: baseLayoutCharacter,
				associatedText: associatedText
			);
		} else {
			if ( shiftedCharacter.HasValue || baseLayoutCharacter.HasValue ) {
				return false;
			}
			inputEvent = TerminalInputEvent.FromKey(
				semanticKey,
				modifiers,
				keyPhase: phase,
				associatedText: associatedText
			);
		}
		return true;
	}

	private static int FindCsiFinalIndex(
		IReadOnlyList<byte> bytes
	) {
		for ( int index = 2; index < bytes.Count; ++index ) {
			byte value = bytes[ index ];
			if ( value is >= 0x40 and <= 0x7e ) {
				return index;
			}
		}
		return -1;
	}

	private static bool TryParseNonNegativeInteger(
		string value,
		out int parsed
	) {
		return int.TryParse(
			value,
			NumberStyles.None,
			CultureInfo.InvariantCulture,
			out parsed
		) && 0 <= parsed;
	}

	private static bool TryParsePositiveInteger(
		string value,
		out int parsed
	) {
		return TryParseNonNegativeInteger( value, out parsed ) && 0 < parsed;
	}

	private static bool TryParseRune(
		string value,
		out Rune rune
	) {
		rune = default;
		return TryParseNonNegativeInteger( value, out int codePoint )
			&& TryCreateRune( codePoint, out rune );
	}

	private static bool TryCreateRune(
		int codePoint,
		out Rune rune
	) {
		rune = default;
		if ( 0 > codePoint || 0x10ffff < codePoint || codePoint is >= 0xd800 and <= 0xdfff ) {
			return false;
		}
		rune = new Rune( codePoint );
		return true;
	}

	private static bool TryMapXtermModifiers(
		int encodedModifiers,
		out TerminalKeyModifiers modifiers
	) {
		modifiers = TerminalKeyModifiers.None;
		int bits = encodedModifiers - 1;
		if ( 0 > bits || 0 != ( bits & ~0x07 ) ) {
			return false;
		}
		if ( 0 != ( bits & 1 ) ) {
			modifiers |= TerminalKeyModifiers.Shift;
		}
		if ( 0 != ( bits & 2 ) ) {
			modifiers |= TerminalKeyModifiers.Alt;
		}
		if ( 0 != ( bits & 4 ) ) {
			modifiers |= TerminalKeyModifiers.Control;
		}
		return true;
	}

	private static bool TryMapKittyModifiers(
		int encodedModifiers,
		out TerminalKeyModifiers modifiers
	) {
		modifiers = TerminalKeyModifiers.None;
		int bits = encodedModifiers - 1;
		if ( 0 > bits || 0 != ( bits & ~0xff ) ) {
			return false;
		}
		if ( 0 != ( bits & 1 ) ) {
			modifiers |= TerminalKeyModifiers.Shift;
		}
		if ( 0 != ( bits & 2 ) ) {
			modifiers |= TerminalKeyModifiers.Alt;
		}
		if ( 0 != ( bits & 4 ) ) {
			modifiers |= TerminalKeyModifiers.Control;
		}
		if ( 0 != ( bits & 8 ) ) {
			modifiers |= TerminalKeyModifiers.Super;
		}
		if ( 0 != ( bits & 16 ) ) {
			modifiers |= TerminalKeyModifiers.Hyper;
		}
		if ( 0 != ( bits & 32 ) ) {
			modifiers |= TerminalKeyModifiers.Meta;
		}
		if ( 0 != ( bits & 64 ) ) {
			modifiers |= TerminalKeyModifiers.CapsLock;
		}
		if ( 0 != ( bits & 128 ) ) {
			modifiers |= TerminalKeyModifiers.NumLock;
		}
		return true;
	}

	private static bool TryMapKittyEventPhase(
		int eventType,
		out TerminalKeyEventPhase phase
	) {
		phase = eventType switch {
			1 => TerminalKeyEventPhase.Press,
			2 => TerminalKeyEventPhase.Repeat,
			3 => TerminalKeyEventPhase.Release,
			_ => default
		};
		return eventType is >= 1 and <= 3;
	}

	private static TerminalKey MapUnicodeKeyIdentity(
		Rune character
	) {
		return character.Value switch {
			13 => TerminalKey.Enter,
			27 => TerminalKey.Escape,
			9 => TerminalKey.Tab,
			32 => TerminalKey.Space,
			127 => TerminalKey.Backspace,
			_ => TerminalKey.Character
		};
	}

	private static bool IsForbiddenAssociatedTextControl(
		Rune rune
	) {
		return rune.Value <= 0x1f
			|| 0x7f == rune.Value
			|| rune.Value is >= 0x80 and <= 0x9f;
	}

	private static bool IsKittyPrivateUseCode(
		int codePoint
	) {
		return codePoint is >= 0xe000 and <= 0xf8ff;
	}

	private static bool TryMapKittyFunctionalKey(
		int codePoint,
		out TerminalKey key,
		out int? functionKeyNumber
	) {
		functionKeyNumber = null;
		key = codePoint switch {
			57358 => TerminalKey.CapsLock,
			57359 => TerminalKey.ScrollLock,
			57360 => TerminalKey.NumLock,
			57361 => TerminalKey.PrintScreen,
			57362 => TerminalKey.Pause,
			57363 => TerminalKey.Menu,
			>= 57376 and <= 57398 => TerminalKey.Function,
			57399 => TerminalKey.Keypad0,
			57400 => TerminalKey.Keypad1,
			57401 => TerminalKey.Keypad2,
			57402 => TerminalKey.Keypad3,
			57403 => TerminalKey.Keypad4,
			57404 => TerminalKey.Keypad5,
			57405 => TerminalKey.Keypad6,
			57406 => TerminalKey.Keypad7,
			57407 => TerminalKey.Keypad8,
			57408 => TerminalKey.Keypad9,
			57409 => TerminalKey.KeypadDecimal,
			57410 => TerminalKey.KeypadDivide,
			57411 => TerminalKey.KeypadMultiply,
			57412 => TerminalKey.KeypadSubtract,
			57413 => TerminalKey.KeypadAdd,
			57414 => TerminalKey.KeypadEnter,
			57415 => TerminalKey.KeypadEqual,
			57416 => TerminalKey.KeypadSeparator,
			57417 => TerminalKey.KeypadLeft,
			57418 => TerminalKey.KeypadRight,
			57419 => TerminalKey.KeypadUp,
			57420 => TerminalKey.KeypadDown,
			57421 => TerminalKey.KeypadPageUp,
			57422 => TerminalKey.KeypadPageDown,
			57423 => TerminalKey.KeypadHome,
			57424 => TerminalKey.KeypadEnd,
			57425 => TerminalKey.KeypadInsert,
			57426 => TerminalKey.KeypadDelete,
			57427 => TerminalKey.KeypadBegin,
			57428 => TerminalKey.MediaPlay,
			57429 => TerminalKey.MediaPause,
			57430 => TerminalKey.MediaPlayPause,
			57431 => TerminalKey.MediaReverse,
			57432 => TerminalKey.MediaStop,
			57433 => TerminalKey.MediaFastForward,
			57434 => TerminalKey.MediaRewind,
			57435 => TerminalKey.MediaTrackNext,
			57436 => TerminalKey.MediaTrackPrevious,
			57437 => TerminalKey.MediaRecord,
			57438 => TerminalKey.VolumeDown,
			57439 => TerminalKey.VolumeUp,
			57440 => TerminalKey.VolumeMute,
			57441 => TerminalKey.LeftShift,
			57442 => TerminalKey.LeftControl,
			57443 => TerminalKey.LeftAlt,
			57444 => TerminalKey.LeftSuper,
			57445 => TerminalKey.LeftHyper,
			57446 => TerminalKey.LeftMeta,
			57447 => TerminalKey.RightShift,
			57448 => TerminalKey.RightControl,
			57449 => TerminalKey.RightAlt,
			57450 => TerminalKey.RightSuper,
			57451 => TerminalKey.RightHyper,
			57452 => TerminalKey.RightMeta,
			57453 => TerminalKey.IsoLevel3Shift,
			57454 => TerminalKey.IsoLevel5Shift,
			_ => TerminalKey.None
		};

		if ( TerminalKey.Function == key ) {
			functionKeyNumber = codePoint - 57376 + 13;
		}
		return TerminalKey.None != key;
	}
}
