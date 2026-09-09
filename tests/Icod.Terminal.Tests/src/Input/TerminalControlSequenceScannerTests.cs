/*
	Icod.Terminal.Tests
	Automated test suite for the Icod.Terminal library.
	Copyright (C) 2026  Timothy J. Bruce <uniblab@hotmail.com>
*/

/*
	This program is free software: you can redistribute it and/or modify
	it under the terms of the GNU General Public License as published by
	the Free Software Foundation, either version 3 of the License, or
	(at your option) any later version.

	This program is distributed in the hope that it will be useful,
	but WITHOUT ANY WARRANTY; without even the implied warranty of
	MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
	GNU General Public License for more details.

	You should have received a copy of the GNU General Public License
	along with this program.  If not, see <https://www.gnu.org/licenses/>.
*/
namespace Icod.Terminal.Tests.Input;

using Icod.Terminal;
using Xunit;

/// <summary>
/// Verifies the N152 shared incremental control-language scanner.
/// </summary>
public sealed class TerminalControlSequenceScannerTests {
	[Fact]
	public void SevenBitFamiliesFrameIncrementally() {
		AssertIncrementalComplete(
			[ 0x1B, (byte)'[', (byte)'?', (byte)'1', (byte)';', (byte)'2', (byte)'c' ],
			TerminalControlFamily.Csi,
			usesEightBitIntroducer: false
		);
		AssertIncrementalComplete(
			[ 0x1B, (byte)'P', (byte)'1', (byte)'$', (byte)'r', (byte)'m', 0x1B, (byte)'\\' ],
			TerminalControlFamily.Dcs,
			usesEightBitIntroducer: false
		);
		AssertIncrementalComplete(
			[ 0x1B, (byte)']', (byte)'4', (byte)';', (byte)'1', (byte)';', (byte)'?', 0x1B, (byte)'\\' ],
			TerminalControlFamily.Osc,
			usesEightBitIntroducer: false
		);
		AssertIncrementalComplete(
			[ 0x1B, (byte)'_', (byte)'G', (byte)'a', (byte)'=', (byte)'q', 0x1B, (byte)'\\' ],
			TerminalControlFamily.Apc,
			usesEightBitIntroducer: false
		);
		AssertIncrementalComplete(
			[ 0x1B, (byte)'^', (byte)'p', (byte)'m', 0x1B, (byte)'\\' ],
			TerminalControlFamily.Pm,
			usesEightBitIntroducer: false
		);
		AssertIncrementalComplete(
			[ 0x1B, (byte)'X', (byte)'s', (byte)'o', (byte)'s', 0x1B, (byte)'\\' ],
			TerminalControlFamily.Sos,
			usesEightBitIntroducer: false
		);
	}

	[Fact]
	public void EightBitFamiliesFrameIncrementally() {
		AssertIncrementalComplete(
			[ 0x9B, (byte)'?', (byte)'1', (byte)'c' ],
			TerminalControlFamily.Csi,
			usesEightBitIntroducer: true
		);
		AssertIncrementalComplete(
			[ 0x90, (byte)'1', (byte)'$', (byte)'r', (byte)'m', 0x9C ],
			TerminalControlFamily.Dcs,
			usesEightBitIntroducer: true
		);
		AssertIncrementalComplete(
			[ 0x9D, (byte)'4', (byte)';', (byte)'1', (byte)';', (byte)'?', 0x9C ],
			TerminalControlFamily.Osc,
			usesEightBitIntroducer: true
		);
		AssertIncrementalComplete(
			[ 0x9F, (byte)'G', (byte)'a', (byte)'=', (byte)'q', 0x9C ],
			TerminalControlFamily.Apc,
			usesEightBitIntroducer: true
		);
		AssertIncrementalComplete(
			[ 0x9E, (byte)'p', (byte)'m', 0x9C ],
			TerminalControlFamily.Pm,
			usesEightBitIntroducer: true
		);
		AssertIncrementalComplete(
			[ 0x98, (byte)'s', (byte)'o', (byte)'s', 0x9C ],
			TerminalControlFamily.Sos,
			usesEightBitIntroducer: true
		);
	}

	[Fact]
	public void EscapePrefixRemainsUnclassifiedUntilSecondByte() {
		TerminalControlSequenceScanner scanner = new(
			TerminalResponseFramer.DefaultMaximumFrameBytes
		);

		TerminalResponseFrameParseStatus status = scanner.Feed( 0x1B );

		Assert.Equal( TerminalResponseFrameParseStatus.Incomplete, status );
		Assert.Equal( TerminalControlScanState.Escape, scanner.State );
		Assert.Null( scanner.Family );
		Assert.True( scanner.IntroducerIncomplete );
		Assert.Equal( 1, scanner.Length );
	}

	[Fact]
	public void UnknownEscapeSequenceBecomesNotCandidate() {
		TerminalControlSequenceScanner scanner = new(
			TerminalResponseFramer.DefaultMaximumFrameBytes
		);

		Assert.Equal(
			TerminalResponseFrameParseStatus.Incomplete,
			scanner.Feed( 0x1B )
		);
		Assert.Equal(
			TerminalResponseFrameParseStatus.NotCandidate,
			scanner.Feed( (byte)'7' )
		);
		Assert.Equal( TerminalControlScanState.NotCandidate, scanner.State );
		Assert.Null( scanner.Family );
	}

	[Fact]
	public void ScannerResetAllowsASecondFrame() {
		TerminalControlSequenceScanner scanner = new(
			TerminalResponseFramer.DefaultMaximumFrameBytes
		);

		FeedComplete(
			scanner,
			[ 0x1B, (byte)'[', (byte)'c' ]
		);
		Assert.Equal( TerminalControlFamily.Csi, scanner.Family );

		scanner.Reset();

		Assert.Equal( TerminalControlScanState.Ground, scanner.State );
		Assert.Equal( TerminalResponseFrameParseStatus.Incomplete, scanner.Status );
		Assert.Null( scanner.Family );
		Assert.Equal( 0, scanner.Length );
		Assert.False( scanner.UsesEightBitIntroducer );

		FeedComplete(
			scanner,
			[ 0x1B, (byte)'_', (byte)'G', 0x1B, (byte)'\\' ]
		);
		Assert.Equal( TerminalControlFamily.Apc, scanner.Family );
	}

	[Fact]
	public void ScannerRejectsFeedAfterTerminalStateUntilReset() {
		TerminalControlSequenceScanner scanner = new(
			TerminalResponseFramer.DefaultMaximumFrameBytes
		);
		FeedComplete(
			scanner,
			[ 0x1B, (byte)'[', (byte)'c' ]
		);

		Assert.Throws<InvalidOperationException>(
			() => scanner.Feed( (byte)'x' )
		);
	}

	[Fact]
	public void ScannerPreservesCsiIntermediateOrdering() {
		TerminalControlSequenceScanner valid = new(
			TerminalResponseFramer.DefaultMaximumFrameBytes
		);
		FeedComplete(
			valid,
			[ 0x1B, (byte)'[', (byte)'1', (byte)' ', (byte)'q' ]
		);
		Assert.Equal( TerminalControlFamily.Csi, valid.Family );

		TerminalControlSequenceScanner invalid = new(
			TerminalResponseFramer.DefaultMaximumFrameBytes
		);
		Assert.Equal( TerminalResponseFrameParseStatus.Incomplete, invalid.Feed( 0x1B ) );
		Assert.Equal( TerminalResponseFrameParseStatus.Incomplete, invalid.Feed( (byte)'[' ) );
		Assert.Equal( TerminalResponseFrameParseStatus.Incomplete, invalid.Feed( (byte)' ' ) );
		Assert.Equal(
			TerminalResponseFrameParseStatus.Invalid,
			invalid.Feed( (byte)'1' )
		);
	}

	[Fact]
	public void ScannerPreservesSplitDcsStringTerminator() {
		TerminalControlSequenceScanner scanner = new(
			TerminalResponseFramer.DefaultMaximumFrameBytes
		);
		byte[] prefix = [ 0x1B, (byte)'P', (byte)'q', (byte)'x', 0x1B ];
		foreach ( byte value in prefix ) {
			Assert.Equal(
				TerminalResponseFrameParseStatus.Incomplete,
				scanner.Feed( value )
			);
		}

		Assert.Equal( TerminalControlScanState.StringEscape, scanner.State );
		Assert.Equal(
			TerminalResponseFrameParseStatus.Complete,
			scanner.Feed( (byte)'\\' )
		);
	}

	[Fact]
	public void ScannerInvalidatesAtConfiguredBound() {
		TerminalControlSequenceScanner scanner = new( 4 );

		Assert.Equal( TerminalResponseFrameParseStatus.Incomplete, scanner.Feed( 0x1B ) );
		Assert.Equal( TerminalResponseFrameParseStatus.Incomplete, scanner.Feed( (byte)'_' ) );
		Assert.Equal( TerminalResponseFrameParseStatus.Incomplete, scanner.Feed( (byte)'G' ) );
		Assert.Equal(
			TerminalResponseFrameParseStatus.Invalid,
			scanner.Feed( (byte)'x' )
		);
	}

	[Fact]
	public void ScannerConstructorRejectsInvalidBounds() {
		Assert.Throws<ArgumentOutOfRangeException>(
			() => new TerminalControlSequenceScanner( 3 )
		);
		Assert.Throws<ArgumentOutOfRangeException>(
			() => new TerminalControlSequenceScanner(
				TerminalResponseFramer.HardMaximumFrameBytes + 1
			)
		);
	}

	private static void AssertIncrementalComplete(
		IReadOnlyList<byte> frame,
		TerminalControlFamily expectedFamily,
		bool usesEightBitIntroducer
	) {
		TerminalControlSequenceScanner scanner = new(
			TerminalResponseFramer.DefaultMaximumFrameBytes
		);

		for ( int index = 0; index < frame.Count; index++ ) {
			TerminalResponseFrameParseStatus status = scanner.Feed( frame[ index ] );
			if ( index + 1 == frame.Count ) {
				Assert.Equal( TerminalResponseFrameParseStatus.Complete, status );
			} else {
				Assert.Equal( TerminalResponseFrameParseStatus.Incomplete, status );
			}
		}

		Assert.Equal( expectedFamily, scanner.Family );
		Assert.Equal( usesEightBitIntroducer, scanner.UsesEightBitIntroducer );
		Assert.Equal( frame.Count, scanner.Length );
		Assert.Equal( TerminalControlScanState.Complete, scanner.State );
	}

	private static void FeedComplete(
		TerminalControlSequenceScanner scanner,
		IReadOnlyList<byte> frame
	) {
		ArgumentNullException.ThrowIfNull( scanner );
		ArgumentNullException.ThrowIfNull( frame );

		for ( int index = 0; index < frame.Count; index++ ) {
			TerminalResponseFrameParseStatus status = scanner.Feed( frame[ index ] );
			if ( index + 1 == frame.Count ) {
				Assert.Equal( TerminalResponseFrameParseStatus.Complete, status );
			} else {
				Assert.Equal( TerminalResponseFrameParseStatus.Incomplete, status );
			}
		}
	}
}
