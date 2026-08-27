namespace Icod.Terminal;

using Icod.TermInfo;

/// <summary>
/// T17 mouse-protocol decoding for <see cref="TerminalInputDecoder"/>.
/// </summary>
internal sealed partial class TerminalInputDecoder {
	private TerminalMouseProtocolParser? mouseProtocolParser;

	private void InitializeMouseProtocolParser(
		TerminalDescription terminal
	) {
		ArgumentNullException.ThrowIfNull( terminal );
		this.mouseProtocolParser = TerminalMouseProtocolParser.TryCreate( terminal );
	}

	private async ValueTask<TerminalInputEvent?> TryReadMouseEventAsync(
		CancellationToken cancellationToken
	) {
		TerminalMouseProtocolParser? parser = this.mouseProtocolParser;
		if ( parser is null ) {
			return null;
		}

		while ( true ) {
			MouseFrameParseStatus status = parser.TryParse(
				this.bufferedBytes,
				out TerminalInputEvent? inputEvent,
				out int consumed
			);
			switch ( status ) {
				case MouseFrameParseStatus.NoMatch:
				case MouseFrameParseStatus.Invalid:
					return null;

				case MouseFrameParseStatus.Success:
					if ( inputEvent is null || 0 >= consumed ) {
						throw new InvalidOperationException(
							"The mouse protocol parser reported success without a complete event."
						);
					}

					this.Consume( consumed );
					return inputEvent;

				case MouseFrameParseStatus.NeedMoreData:
					bool appended = this.bufferedBytes.Count < parser.PrefixLength
						? await this.ReadMoreWithinEscapeWindowAsync(
							cancellationToken
						).ConfigureAwait( false )
						: await this.ReadMoreAsync(
							cancellationToken
						).ConfigureAwait( false )
					;
					if ( !appended ) {
						return null;
					}
					break;

				default:
					throw new InvalidOperationException(
						$"The mouse protocol parser returned unknown status '{status}'."
					);
			}
		}
	}

	private enum MouseFrameParseStatus {
		NoMatch,
		NeedMoreData,
		Success,
		Invalid
	}

	private enum MouseWireProtocol {
		Sgr,
		Legacy
	}

	private sealed class TerminalMouseProtocolParser {
		private const string SgrPrefixText = "\u001b[<";
		private const string LegacyPrefixText = "\u001b[M";
		private const int ShiftMask = 4;
		private const int AltMask = 8;
		private const int ControlMask = 16;
		private const int MotionMask = 32;

		private static readonly byte[] SgrPrefix = [ 0x1B, 0x5B, 0x3C ];
		private static readonly byte[] LegacyPrefix = [ 0x1B, 0x5B, 0x4D ];

		private readonly MouseWireProtocol protocol;
		private readonly byte[] prefix;
		private TerminalMouseButton lastPressedButton;

		private TerminalMouseProtocolParser(
			MouseWireProtocol protocol,
			byte[] prefix
		) {
			if ( !Enum.IsDefined( protocol ) ) {
				throw new ArgumentOutOfRangeException( nameof( protocol ) );
			}
			ArgumentNullException.ThrowIfNull( prefix );
			if ( 0 == prefix.Length ) {
				throw new ArgumentException(
					"The mouse protocol prefix cannot be empty.",
					nameof( prefix )
				);
			}

			this.protocol = protocol;
			this.prefix = prefix;
		}

		internal int PrefixLength {
			get {
				return this.prefix.Length;
			}
		}

		internal static TerminalMouseProtocolParser? TryCreate(
			TerminalDescription terminal
		) {
			ArgumentNullException.ThrowIfNull( terminal );

			string? keyMouse = terminal.GetString( StringCapability.KeyMouse );
			if ( string.IsNullOrEmpty( keyMouse ) ) {
				return null;
			}
			if ( keyMouse.StartsWith(
				SgrPrefixText,
				StringComparison.Ordinal
			) ) {
				return new TerminalMouseProtocolParser(
					MouseWireProtocol.Sgr,
					SgrPrefix
				);
			}
			if ( keyMouse.StartsWith(
				LegacyPrefixText,
				StringComparison.Ordinal
			) ) {
				return new TerminalMouseProtocolParser(
					MouseWireProtocol.Legacy,
					LegacyPrefix
				);
			}

			return null;
		}

		internal MouseFrameParseStatus TryParse(
			IReadOnlyList<byte> bytes,
			out TerminalInputEvent? inputEvent,
			out int consumed
		) {
			ArgumentNullException.ThrowIfNull( bytes );

			inputEvent = null;
			consumed = 0;

			int prefixCount = Math.Min( bytes.Count, this.prefix.Length );
			for ( int index = 0; index < prefixCount; index++ ) {
				if ( bytes[ index ] != this.prefix[ index ] ) {
					return MouseFrameParseStatus.NoMatch;
				}
			}
			if ( bytes.Count < this.prefix.Length ) {
				return MouseFrameParseStatus.NeedMoreData;
			}

			return MouseWireProtocol.Sgr == this.protocol
				? this.TryParseSgr(
					bytes,
					out inputEvent,
					out consumed
				)
				: this.TryParseLegacy(
					bytes,
					out inputEvent,
					out consumed
				)
			;
		}

		private MouseFrameParseStatus TryParseSgr(
			IReadOnlyList<byte> bytes,
			out TerminalInputEvent? inputEvent,
			out int consumed
		) {
			inputEvent = null;
			consumed = 0;

			Span<int> fields = stackalloc int[ 3 ];
			int fieldIndex = 0;
			int value = 0;
			bool hasDigit = false;

			for ( int index = this.prefix.Length; index < bytes.Count; index++ ) {
				byte current = bytes[ index ];
				if ( current is >= (byte)'0' and <= (byte)'9' ) {
					int digit = current - (byte)'0';
					if ( value > ( int.MaxValue - digit ) / 10 ) {
						return MouseFrameParseStatus.Invalid;
					}

					value = ( value * 10 ) + digit;
					hasDigit = true;
					continue;
				}

				if ( (byte)';' == current ) {
					if ( !hasDigit || 2 <= fieldIndex ) {
						return MouseFrameParseStatus.Invalid;
					}

					fields[ fieldIndex++ ] = value;
					value = 0;
					hasDigit = false;
					continue;
				}

				if ( (byte)'M' == current || (byte)'m' == current ) {
					if ( !hasDigit || 2 != fieldIndex ) {
						return MouseFrameParseStatus.Invalid;
					}

					fields[ 2 ] = value;
					if ( !this.TryCreateMouseEvent(
						fields[ 0 ],
						fields[ 1 ],
						fields[ 2 ],
						(byte)'m' == current,
						out inputEvent
					) ) {
						inputEvent = null;
						return MouseFrameParseStatus.Invalid;
					}

					consumed = index + 1;
					return MouseFrameParseStatus.Success;
				}

				return MouseFrameParseStatus.Invalid;
			}

			return MouseFrameParseStatus.NeedMoreData;
		}

		private MouseFrameParseStatus TryParseLegacy(
			IReadOnlyList<byte> bytes,
			out TerminalInputEvent? inputEvent,
			out int consumed
		) {
			inputEvent = null;
			consumed = 0;

			int required = this.prefix.Length + 3;
			if ( bytes.Count < required ) {
				return MouseFrameParseStatus.NeedMoreData;
			}

			int codeByte = bytes[ this.prefix.Length ];
			int columnByte = bytes[ this.prefix.Length + 1 ];
			int rowByte = bytes[ this.prefix.Length + 2 ];
			if ( 32 > codeByte || 33 > columnByte || 33 > rowByte ) {
				return MouseFrameParseStatus.Invalid;
			}

			if ( !this.TryCreateMouseEvent(
				codeByte - 32,
				columnByte - 32,
				rowByte - 32,
				releaseMarker: false,
				out inputEvent
			) ) {
				inputEvent = null;
				return MouseFrameParseStatus.Invalid;
			}

			consumed = required;
			return MouseFrameParseStatus.Success;
		}

		private bool TryCreateMouseEvent(
			int code,
			int column,
			int row,
			bool releaseMarker,
			out TerminalInputEvent? inputEvent
		) {
			inputEvent = null;
			if ( 0 > code || 1 > column || 1 > row ) {
				return false;
			}

			TerminalKeyModifiers modifiers = TerminalKeyModifiers.None;
			if ( 0 != ( code & ShiftMask ) ) {
				modifiers |= TerminalKeyModifiers.Shift;
			}
			if ( 0 != ( code & AltMask ) ) {
				modifiers |= TerminalKeyModifiers.Alt;
			}
			if ( 0 != ( code & ControlMask ) ) {
				modifiers |= TerminalKeyModifiers.Control;
			}

			bool motion = 0 != ( code & MotionMask );
			int buttonCode = code
				& ~( ShiftMask | AltMask | ControlMask | MotionMask );

			TerminalMouseAction action;
			TerminalMouseButton button;

			if ( buttonCode is >= 64 and <= 67 ) {
				if ( motion || releaseMarker ) {
					return false;
				}

				action = buttonCode switch {
					64 => TerminalMouseAction.WheelUp,
					65 => TerminalMouseAction.WheelDown,
					66 => TerminalMouseAction.WheelLeft,
					67 => TerminalMouseAction.WheelRight,
					_ => throw new InvalidOperationException()
				};
				button = TerminalMouseButton.None;
			} else if ( 3 == buttonCode ) {
				if ( motion ) {
					if ( releaseMarker ) {
						return false;
					}

					action = TerminalMouseAction.Move;
					button = TerminalMouseButton.None;
				} else {
					if ( TerminalMouseButton.None == this.lastPressedButton ) {
						return false;
					}

					action = TerminalMouseAction.Release;
					button = this.lastPressedButton;
				}
			} else {
				if ( !TryMapButton(
					buttonCode,
					out button
				) ) {
					return false;
				}

				if ( motion ) {
					if ( releaseMarker ) {
						return false;
					}
					action = TerminalMouseAction.Move;
				} else {
					action = releaseMarker
						? TerminalMouseAction.Release
						: TerminalMouseAction.Press
					;
				}
			}

			if ( TerminalMouseAction.Press == action
				|| ( TerminalMouseAction.Move == action
					&& TerminalMouseButton.None != button ) ) {
				this.lastPressedButton = button;
			} else if ( TerminalMouseAction.Release == action ) {
				this.lastPressedButton = TerminalMouseButton.None;
			}

			inputEvent = TerminalInputEvent.FromMouse(
				new TerminalMouseEvent(
					action,
					button,
					column - 1,
					row - 1,
					modifiers
				)
			);
			return true;
		}

		private static bool TryMapButton(
			int buttonCode,
			out TerminalMouseButton button
		) {
			button = buttonCode switch {
				0 => TerminalMouseButton.Primary,
				1 => TerminalMouseButton.Middle,
				2 => TerminalMouseButton.Secondary,
				128 => TerminalMouseButton.Button4,
				129 => TerminalMouseButton.Button5,
				130 => TerminalMouseButton.Button6,
				131 => TerminalMouseButton.Button7,
				_ => TerminalMouseButton.None
			};
			return TerminalMouseButton.None != button;
		}
	}
}
