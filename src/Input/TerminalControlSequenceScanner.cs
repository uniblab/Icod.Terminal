/*
	Icod.Terminal
	Managed, cross-platform live-terminal session and terminal-control library for .NET.
	Copyright (C) 2026  Timothy J. Bruce <uniblab@hotmail.com>
*/

/*
	This program is free software: you can redistribute it and/or modify
	it under the terms of the GNU Lesser General Public License as published by
	the Free Software Foundation, either version 3 of the License, or
	(at your option) any later version.

	This program is distributed in the hope that it will be useful,
	but WITHOUT ANY WARRANTY; without even the implied warranty of
	MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
	GNU Lesser General Public License for more details.

	You should have received a copy of the GNU Lesser General Public License
	along with this program.  If not, see <https://www.gnu.org/licenses/>.
*/
namespace Icod.Terminal;

/// <summary>
/// Identifies one internal scanner state without assigning dialect semantics.
/// </summary>
internal enum TerminalControlScanState {
	Ground,
	Escape,
	CsiParameter,
	CsiIntermediate,
	DcsParameter,
	DcsIntermediate,
	DcsPayload,
	OscPayload,
	ApcPayload,
	PmPayload,
	SosPayload,
	StringEscape,
	Complete,
	Invalid,
	NotCandidate
}

/// <summary>
/// Incrementally frames one bounded ECMA-style terminal control sequence or string.
/// </summary>
internal sealed class TerminalControlSequenceScanner {
	private const byte BellByte = 0x07;
	private const byte EscapeByte = 0x1B;
	private const byte SosByte = 0x98;
	private const byte CsiByte = 0x9B;
	private const byte DcsByte = 0x90;
	private const byte OscByte = 0x9D;
	private const byte PmByte = 0x9E;
	private const byte ApcByte = 0x9F;
	private const byte StringTerminatorByte = 0x9C;

	private readonly int maximumFrameBytes;

	private TerminalControlScanState state;
	private TerminalControlFamily? family;
	private bool usesEightBitIntroducer;
	private int length;

	internal TerminalControlSequenceScanner(
		int maximumFrameBytes
	) {
		if ( 4 > maximumFrameBytes
			|| TerminalResponseFramer.HardMaximumFrameBytes < maximumFrameBytes ) {
			throw new ArgumentOutOfRangeException( nameof( maximumFrameBytes ) );
		}

		this.maximumFrameBytes = maximumFrameBytes;
		this.Reset();
	}

	internal TerminalControlScanState State {
		get {
			return this.state;
		}
	}

	internal TerminalResponseFrameParseStatus Status {
		get {
			return this.state switch {
				TerminalControlScanState.Complete => TerminalResponseFrameParseStatus.Complete,
				TerminalControlScanState.Invalid => TerminalResponseFrameParseStatus.Invalid,
				TerminalControlScanState.NotCandidate => TerminalResponseFrameParseStatus.NotCandidate,
				_ => TerminalResponseFrameParseStatus.Incomplete
			};
		}
	}

	internal TerminalControlFamily? Family {
		get {
			return this.family;
		}
	}

	internal bool UsesEightBitIntroducer {
		get {
			return this.usesEightBitIntroducer;
		}
	}

	internal int Length {
		get {
			return this.length;
		}
	}

	internal bool IntroducerIncomplete {
		get {
			return TerminalControlScanState.Escape == this.state
				&& 1 == this.length;
		}
	}

	internal void Reset() {
		this.state = TerminalControlScanState.Ground;
		this.family = null;
		this.usesEightBitIntroducer = false;
		this.length = 0;
	}

	internal TerminalResponseFrameParseStatus Feed(
		byte value
	) {
		if ( this.IsTerminalState() ) {
			throw new InvalidOperationException(
				"The terminal control scanner must be reset before feeding bytes after a terminal state."
			);
		}
		if ( this.length >= this.maximumFrameBytes ) {
			this.state = TerminalControlScanState.Invalid;
			return this.Status;
		}

		checked {
			++this.length;
		}

		switch ( this.state ) {
			case TerminalControlScanState.Ground:
				this.FeedGround( value );
				break;

			case TerminalControlScanState.Escape:
				this.FeedEscape( value );
				break;

			case TerminalControlScanState.CsiParameter:
				this.FeedCsiParameter( value );
				break;

			case TerminalControlScanState.CsiIntermediate:
				this.FeedCsiIntermediate( value );
				break;

			case TerminalControlScanState.DcsParameter:
				this.FeedDcsParameter( value );
				break;

			case TerminalControlScanState.DcsIntermediate:
				this.FeedDcsIntermediate( value );
				break;

			case TerminalControlScanState.DcsPayload:
				this.FeedDcsPayload( value );
				break;

			case TerminalControlScanState.OscPayload:
				this.FeedOscPayload( value );
				break;

			case TerminalControlScanState.ApcPayload:
			case TerminalControlScanState.PmPayload:
			case TerminalControlScanState.SosPayload:
				this.FeedStrictStringPayload( value );
				break;

			case TerminalControlScanState.StringEscape:
				this.state = (byte)'\\' == value
					? TerminalControlScanState.Complete
					: TerminalControlScanState.Invalid
				;
				break;

			default:
				throw new InvalidOperationException(
					$"Unexpected terminal control scanner state '{this.state}'."
				);
		}

		if ( !this.IsTerminalState()
			&& this.length >= this.maximumFrameBytes ) {
			this.state = TerminalControlScanState.Invalid;
		}

		return this.Status;
	}

	private void FeedGround(
		byte value
	) {
		if ( EscapeByte == value ) {
			this.state = TerminalControlScanState.Escape;
			return;
		}

		this.usesEightBitIntroducer = true;
		switch ( value ) {
			case CsiByte:
				this.SetFamily(
					TerminalControlFamily.Csi,
					TerminalControlScanState.CsiParameter
				);
				return;

			case DcsByte:
				this.SetFamily(
					TerminalControlFamily.Dcs,
					TerminalControlScanState.DcsParameter
				);
				return;

			case OscByte:
				this.SetFamily(
					TerminalControlFamily.Osc,
					TerminalControlScanState.OscPayload
				);
				return;

			case ApcByte:
				this.SetFamily(
					TerminalControlFamily.Apc,
					TerminalControlScanState.ApcPayload
				);
				return;

			case PmByte:
				this.SetFamily(
					TerminalControlFamily.Pm,
					TerminalControlScanState.PmPayload
				);
				return;

			case SosByte:
				this.SetFamily(
					TerminalControlFamily.Sos,
					TerminalControlScanState.SosPayload
				);
				return;

			default:
				this.usesEightBitIntroducer = false;
				this.state = TerminalControlScanState.NotCandidate;
				return;
		}
	}

	private void FeedEscape(
		byte value
	) {
		this.usesEightBitIntroducer = false;
		switch ( value ) {
			case (byte)'[':
				this.SetFamily(
					TerminalControlFamily.Csi,
					TerminalControlScanState.CsiParameter
				);
				return;

			case (byte)'P':
				this.SetFamily(
					TerminalControlFamily.Dcs,
					TerminalControlScanState.DcsParameter
				);
				return;

			case (byte)']':
				this.SetFamily(
					TerminalControlFamily.Osc,
					TerminalControlScanState.OscPayload
				);
				return;

			case (byte)'_':
				this.SetFamily(
					TerminalControlFamily.Apc,
					TerminalControlScanState.ApcPayload
				);
				return;

			case (byte)'^':
				this.SetFamily(
					TerminalControlFamily.Pm,
					TerminalControlScanState.PmPayload
				);
				return;

			case (byte)'X':
				this.SetFamily(
					TerminalControlFamily.Sos,
					TerminalControlScanState.SosPayload
				);
				return;

			default:
				this.state = TerminalControlScanState.NotCandidate;
				return;
		}
	}

	private void FeedCsiParameter(
		byte value
	) {
		if ( IsFinalByte( value ) ) {
			this.state = TerminalControlScanState.Complete;
			return;
		}
		if ( IsParameterByte( value ) ) {
			return;
		}
		if ( IsIntermediateByte( value ) ) {
			this.state = TerminalControlScanState.CsiIntermediate;
			return;
		}

		this.state = TerminalControlScanState.Invalid;
	}

	private void FeedCsiIntermediate(
		byte value
	) {
		if ( IsFinalByte( value ) ) {
			this.state = TerminalControlScanState.Complete;
			return;
		}
		if ( IsIntermediateByte( value ) ) {
			return;
		}

		this.state = TerminalControlScanState.Invalid;
	}

	private void FeedDcsParameter(
		byte value
	) {
		if ( IsFinalByte( value ) ) {
			this.state = TerminalControlScanState.DcsPayload;
			return;
		}
		if ( IsParameterByte( value ) ) {
			return;
		}
		if ( IsIntermediateByte( value ) ) {
			this.state = TerminalControlScanState.DcsIntermediate;
			return;
		}

		this.state = TerminalControlScanState.Invalid;
	}

	private void FeedDcsIntermediate(
		byte value
	) {
		if ( IsFinalByte( value ) ) {
			this.state = TerminalControlScanState.DcsPayload;
			return;
		}
		if ( IsIntermediateByte( value ) ) {
			return;
		}

		this.state = TerminalControlScanState.Invalid;
	}

	private void FeedDcsPayload(
		byte value
	) {
		if ( StringTerminatorByte == value ) {
			this.state = TerminalControlScanState.Complete;
			return;
		}
		if ( EscapeByte == value ) {
			this.state = TerminalControlScanState.StringEscape;
			return;
		}
		if ( IsCancellationByte( value ) ) {
			this.state = TerminalControlScanState.Invalid;
		}
	}

	private void FeedOscPayload(
		byte value
	) {
		if ( StringTerminatorByte == value ) {
			this.state = this.usesEightBitIntroducer
				? TerminalControlScanState.Complete
				: TerminalControlScanState.Invalid
			;
			return;
		}
		if ( BellByte == value ) {
			this.state = this.usesEightBitIntroducer
				? TerminalControlScanState.Invalid
				: TerminalControlScanState.Complete
			;
			return;
		}
		if ( EscapeByte == value ) {
			this.state = this.usesEightBitIntroducer
				? TerminalControlScanState.Invalid
				: TerminalControlScanState.StringEscape
			;
			return;
		}
		if ( IsCancellationByte( value ) ) {
			this.state = TerminalControlScanState.Invalid;
		}
	}

	private void FeedStrictStringPayload(
		byte value
	) {
		if ( StringTerminatorByte == value ) {
			this.state = this.usesEightBitIntroducer
				? TerminalControlScanState.Complete
				: TerminalControlScanState.Invalid
			;
			return;
		}
		if ( EscapeByte == value ) {
			this.state = this.usesEightBitIntroducer
				? TerminalControlScanState.Invalid
				: TerminalControlScanState.StringEscape
			;
			return;
		}
		if ( IsCancellationByte( value ) ) {
			this.state = TerminalControlScanState.Invalid;
		}
	}

	private void SetFamily(
		TerminalControlFamily family,
		TerminalControlScanState state
	) {
		if ( !Enum.IsDefined( family ) ) {
			throw new ArgumentOutOfRangeException( nameof( family ) );
		}
		if ( !Enum.IsDefined( state ) ) {
			throw new ArgumentOutOfRangeException( nameof( state ) );
		}

		this.family = family;
		this.state = state;
	}

	private bool IsTerminalState() {
		return this.state is TerminalControlScanState.Complete
			or TerminalControlScanState.Invalid
			or TerminalControlScanState.NotCandidate;
	}

	private static bool IsParameterByte(
		byte value
	) {
		return value is >= 0x30 and <= 0x3F;
	}

	private static bool IsIntermediateByte(
		byte value
	) {
		return value is >= 0x20 and <= 0x2F;
	}

	private static bool IsFinalByte(
		byte value
	) {
		return value is >= 0x40 and <= 0x7E;
	}

	private static bool IsCancellationByte(
		byte value
	) {
		return value is 0x18 or 0x1A;
	}
}
