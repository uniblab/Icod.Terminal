namespace Icod.Terminal;

using System.Globalization;
using System.Text;

internal sealed partial class TerminalInputDecoder {
	private readonly object kittyKeyboardProbeGate = new();
	private KittyKeyboardFlagsProbe? kittyKeyboardFlagsProbe;

	internal KittyKeyboardFlagsProbe RegisterKittyKeyboardFlagsProbe() {
		lock ( this.kittyKeyboardProbeGate ) {
			if ( this.kittyKeyboardFlagsProbe is not null ) {
				throw new InvalidOperationException(
					"The terminal input decoder already has an active Kitty keyboard-flags probe."
				);
			}

			KittyKeyboardFlagsProbe probe = new();
			this.kittyKeyboardFlagsProbe = probe;
			return probe;
		}
	}

	internal void RemoveKittyKeyboardFlagsProbe(
		KittyKeyboardFlagsProbe probe
	) {
		ArgumentNullException.ThrowIfNull( probe );
		lock ( this.kittyKeyboardProbeGate ) {
			if ( ReferenceEquals( this.kittyKeyboardFlagsProbe, probe ) ) {
				this.kittyKeyboardFlagsProbe = null;
			}
		}
	}

	private async ValueTask<bool> TryConsumeKittyKeyboardFlagsProbeAsync(
		CancellationToken cancellationToken
	) {
		KittyKeyboardFlagsProbe? probe;
		lock ( this.kittyKeyboardProbeGate ) {
			probe = this.kittyKeyboardFlagsProbe;
		}
		if ( probe is null
			|| 3 > this.bufferedBytes.Count
			|| EscapeByte != this.bufferedBytes[ 0 ]
			|| (byte)'[' != this.bufferedBytes[ 1 ]
			|| (byte)'?' != this.bufferedBytes[ 2 ] ) {
			return false;
		}

		while ( true ) {
			int finalIndex = FindCsiFinalIndex( this.bufferedBytes );
			if ( 0 <= finalIndex ) {
				if ( (byte)'u' != this.bufferedBytes[ finalIndex ] ) {
					return false;
				}

				byte[] frame = this.bufferedBytes.GetRange(
					0,
					finalIndex + 1
				).ToArray();
				if ( !TryParseKittyKeyboardFlagsReport(
					frame,
					out int flags
				) ) {
					return false;
				}

				this.Consume( frame.Length );
				probe.Record( flags );
				if ( 0 == this.bufferedBytes.Count && !this.endOfInput ) {
					_ = await this.ReadMoreAsync(
						cancellationToken
					).ConfigureAwait( false );
				}
				return true;
			}

			if ( this.bufferedBytes.Count >= MaximumModernKeyboardFrameBytes ) {
				return false;
			}
			if ( this.endOfInput ) {
				return false;
			}
			if ( !await this.ReadMoreWithinEscapeWindowAsync(
				cancellationToken
			).ConfigureAwait( false ) ) {
				return false;
			}
		}
	}

	private static bool TryParseKittyKeyboardFlagsReport(
		ReadOnlySpan<byte> frame,
		out int flags
	) {
		flags = 0;
		if ( 5 > frame.Length
			|| EscapeByte != frame[ 0 ]
			|| (byte)'[' != frame[ 1 ]
			|| (byte)'?' != frame[ 2 ]
			|| (byte)'u' != frame[ ^1 ] ) {
			return false;
		}

		string value = Encoding.ASCII.GetString( frame[ 3..^1 ] );
		return int.TryParse(
			value,
			NumberStyles.None,
			CultureInfo.InvariantCulture,
			out flags
		) && 0 <= flags && 31 >= flags;
	}
}

internal sealed class KittyKeyboardFlagsProbe {
	private int flags = -1;

	internal int? Flags {
		get {
			int value = Volatile.Read( ref this.flags );
			return 0 > value
				? null
				: value
			;
		}
	}

	internal void Record(
		int value
	) {
		if ( value is < 0 or > 31 ) {
			throw new ArgumentOutOfRangeException( nameof( value ) );
		}
		Volatile.Write( ref this.flags, value );
	}
}
