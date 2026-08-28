namespace Icod.Terminal;

/// <summary>
/// Implements the bounded CSI request/response forms introduced by T24.
/// </summary>
internal static class TerminalCsiQueryProtocol {
	internal const int MaximumParameterCount = 32;
	internal const int MaximumParameterValue = 1_000_000;

	internal static ReadOnlyMemory<byte> PrimaryDeviceAttributesRequest {
		get;
	} = new byte[] {
		0x1B,
		(byte)'[',
		(byte)'c'
	};

	internal static ReadOnlyMemory<byte> SecondaryDeviceAttributesRequest {
		get;
	} = new byte[] {
		0x1B,
		(byte)'[',
		(byte)'>',
		(byte)'c'
	};

	internal static ReadOnlyMemory<byte> DeviceStatusRequest {
		get;
	} = new byte[] {
		0x1B,
		(byte)'[',
		(byte)'5',
		(byte)'n'
	};

	internal static ReadOnlyMemory<byte> CursorPositionRequest {
		get;
	} = new byte[] {
		0x1B,
		(byte)'[',
		(byte)'6',
		(byte)'n'
	};

	internal static ITerminalResponseMatcher PrimaryDeviceAttributesMatcher {
		get;
	} = new TerminalCsiResponseMatcher(
		(byte)'c',
		(byte)'?'
	);

	internal static ITerminalResponseMatcher SecondaryDeviceAttributesMatcher {
		get;
	} = new TerminalCsiResponseMatcher(
		(byte)'c',
		(byte)'>'
	);

	internal static ITerminalResponseMatcher DeviceStatusMatcher {
		get;
	} = new TerminalCsiResponseMatcher(
		(byte)'n',
		privateMarker: null
	);

	internal static ITerminalResponseMatcher CursorPositionMatcher {
		get;
	} = new TerminalCsiResponseMatcher(
		(byte)'R',
		privateMarker: null
	);

	internal static TerminalPrimaryDeviceAttributes ParsePrimaryDeviceAttributes(
		TerminalResponseFrame frame
	) {
		ArgumentNullException.ThrowIfNull( frame );

		int[] parameters = ParseParameters(
			frame,
			(byte)'c',
			(byte)'?'
		);
		if ( 0 == parameters.Length ) {
			throw new FormatException(
				"A Primary Device Attributes response must contain a device code."
			);
		}

		return new TerminalPrimaryDeviceAttributes(
			parameters[ 0 ],
			parameters.Skip( 1 )
		);
	}

	internal static TerminalSecondaryDeviceAttributes ParseSecondaryDeviceAttributes(
		TerminalResponseFrame frame
	) {
		ArgumentNullException.ThrowIfNull( frame );

		int[] parameters = ParseParameters(
			frame,
			(byte)'c',
			(byte)'>'
		);
		if ( 3 != parameters.Length ) {
			throw new FormatException(
				"A Secondary Device Attributes response must contain exactly three parameters."
			);
		}

		return new TerminalSecondaryDeviceAttributes(
			parameters[ 0 ],
			parameters[ 1 ],
			parameters[ 2 ]
		);
	}

	internal static TerminalDeviceStatus ParseDeviceStatus(
		TerminalResponseFrame frame
	) {
		ArgumentNullException.ThrowIfNull( frame );

		int[] parameters = ParseParameters(
			frame,
			(byte)'n',
			privateMarker: null
		);
		if ( 1 != parameters.Length || parameters[ 0 ] is < 0 or > 4 ) {
			throw new FormatException(
				"A standard Device Status Report response must contain one status value from 0 through 4."
			);
		}

		return (TerminalDeviceStatus)parameters[ 0 ];
	}

	internal static TerminalCursorPosition ParseCursorPosition(
		TerminalResponseFrame frame
	) {
		ArgumentNullException.ThrowIfNull( frame );

		int[] parameters = ParseParameters(
			frame,
			(byte)'R',
			privateMarker: null
		);
		if ( 2 != parameters.Length ) {
			throw new FormatException(
				"A standard Cursor Position Report response must contain exactly row and column parameters."
			);
		}
		if ( 0 >= parameters[ 0 ] || 0 >= parameters[ 1 ] ) {
			throw new FormatException(
				"Cursor Position Report row and column values must be positive."
			);
		}

		return new TerminalCursorPosition(
			parameters[ 0 ],
			parameters[ 1 ]
		);
	}

	private static int[] ParseParameters(
		TerminalResponseFrame frame,
		byte finalByte,
		byte? privateMarker
	) {
		if ( TerminalResponseFrameKind.Csi != frame.Kind ) {
			throw new FormatException(
				"The terminal response is not a CSI frame."
			);
		}

		ReadOnlySpan<byte> bytes = frame.Bytes.Span;
		int start = GetCsiContentOffset( bytes );
		if ( 0 > start || start >= bytes.Length ) {
			throw new FormatException(
				"The terminal response does not contain a valid CSI introducer."
			);
		}
		if ( finalByte != bytes[ ^1 ] ) {
			throw new FormatException(
				"The terminal response has an unexpected CSI final byte."
			);
		}

		int end = bytes.Length - 1;
		if ( privateMarker.HasValue ) {
			if ( start >= end || privateMarker.Value != bytes[ start ] ) {
				throw new FormatException(
					"The terminal response has an unexpected CSI private marker."
				);
			}
			++start;
		} else if ( start < end && IsPrivateMarker( bytes[ start ] ) ) {
			throw new FormatException(
				"The terminal response unexpectedly uses a CSI private marker."
			);
		}

		return ParseNumericParameters(
			bytes.Slice(
				start,
				end - start
			)
		);
	}

	private static int[] ParseNumericParameters(
		ReadOnlySpan<byte> bytes
	) {
		if ( bytes.IsEmpty ) {
			return Array.Empty<int>();
		}

		List<int> parameters = [];
		int value = 0;
		bool hasDigit = false;

		for ( int index = 0; index < bytes.Length; index++ ) {
			byte current = bytes[ index ];
			if ( current >= (byte)'0' && current <= (byte)'9' ) {
				hasDigit = true;
				int digit = current - (byte)'0';
				if ( value > ( MaximumParameterValue - digit ) / 10 ) {
					throw new FormatException(
						$"A CSI numeric parameter exceeds the supported maximum of {MaximumParameterValue}."
					);
				}
				value = checked( value * 10 + digit );
				continue;
			}

			if ( (byte)';' != current ) {
				throw new FormatException(
					"A CSI query response contains a non-numeric parameter character."
				);
			}
			if ( !hasDigit ) {
				throw new FormatException(
					"A CSI query response contains an empty numeric parameter."
				);
			}

			AddParameter(
				parameters,
				value
			);
			value = 0;
			hasDigit = false;
		}

		if ( !hasDigit ) {
			throw new FormatException(
				"A CSI query response ends with an empty numeric parameter."
			);
		}
		AddParameter(
			parameters,
			value
		);

		return parameters.ToArray();
	}

	private static void AddParameter(
		ICollection<int> parameters,
		int value
	) {
		ArgumentNullException.ThrowIfNull( parameters );
		if ( MaximumParameterCount <= parameters.Count ) {
			throw new FormatException(
				$"A CSI query response cannot contain more than {MaximumParameterCount} numeric parameters."
			);
		}

		parameters.Add( value );
	}

	private static int GetCsiContentOffset(
		ReadOnlySpan<byte> bytes
	) {
		if ( 2 <= bytes.Length
			&& 0x1B == bytes[ 0 ]
			&& (byte)'[' == bytes[ 1 ] ) {
			return 2;
		}
		if ( 1 <= bytes.Length && 0x9B == bytes[ 0 ] ) {
			return 1;
		}

		return -1;
	}

	private static bool IsPrivateMarker(
		byte value
	) {
		return value is >= 0x3C and <= 0x3F;
	}

	private sealed class TerminalCsiResponseMatcher : ITerminalResponseMatcher {
		private readonly byte finalByte;
		private readonly byte? privateMarker;

		internal TerminalCsiResponseMatcher(
			byte finalByte,
			byte? privateMarker
		) {
			if ( finalByte is < 0x40 or > 0x7E ) {
				throw new ArgumentOutOfRangeException( nameof( finalByte ) );
			}
			if ( privateMarker.HasValue && !IsPrivateMarker( privateMarker.Value ) ) {
				throw new ArgumentOutOfRangeException( nameof( privateMarker ) );
			}

			this.finalByte = finalByte;
			this.privateMarker = privateMarker;
		}

		public TerminalResponseFrameKind FrameKind {
			get;
		} = TerminalResponseFrameKind.Csi;

		public bool IsMatch(
			TerminalResponseFrame frame
		) {
			ArgumentNullException.ThrowIfNull( frame );
			if ( TerminalResponseFrameKind.Csi != frame.Kind ) {
				return false;
			}

			ReadOnlySpan<byte> bytes = frame.Bytes.Span;
			int start = GetCsiContentOffset( bytes );
			if ( 0 > start || start >= bytes.Length || this.finalByte != bytes[ ^1 ] ) {
				return false;
			}

			int end = bytes.Length - 1;
			if ( this.privateMarker.HasValue ) {
				if ( start >= end || this.privateMarker.Value != bytes[ start ] ) {
					return false;
				}
				++start;
			} else if ( start < end && IsPrivateMarker( bytes[ start ] ) ) {
				return false;
			}

			return start <= end;
		}
	}
}
