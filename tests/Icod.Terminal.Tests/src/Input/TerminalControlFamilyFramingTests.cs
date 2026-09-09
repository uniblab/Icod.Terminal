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
/// Verifies N151 normalized control-family framing without dialect interpretation.
/// </summary>
public sealed class TerminalControlFamilyFramingTests {
	[Fact]
	public void NormalizedFramerPreservesExistingResponseFamilies() {
		AssertSameResult(
			[ 0x1B, (byte)'[', (byte)'?', (byte)'1', (byte)'c' ],
			TerminalResponseFrameKind.Csi,
			TerminalControlFamily.Csi
		);
		AssertSameResult(
			[ 0x1B, (byte)'P', (byte)'1', (byte)'$', (byte)'r', (byte)'m', 0x1B, (byte)'\\' ],
			TerminalResponseFrameKind.Dcs,
			TerminalControlFamily.Dcs
		);
		AssertSameResult(
			[ 0x1B, (byte)']', (byte)'5', (byte)'2', (byte)';', (byte)'c', (byte)';', 0x07 ],
			TerminalResponseFrameKind.Osc,
			TerminalControlFamily.Osc
		);
	}

	[Fact]
	public void ApcSevenBitFrameRequiresSevenBitStringTerminator() {
		byte[] frame = [
			0x1B,
			(byte)'_',
			(byte)'G',
			(byte)'a',
			(byte)'=',
			(byte)'T',
			(byte)',',
			(byte)'f',
			(byte)'=',
			(byte)'3',
			(byte)'2',
			(byte)';',
			(byte)'Q',
			(byte)'Q',
			(byte)'=',
			(byte)'=',
			0x1B,
			(byte)'\\'
		];

		TerminalResponseFrameParseResult result = TerminalResponseFramer.Parse(
			frame,
			TerminalControlFamily.Apc,
			TerminalResponseFramer.DefaultMaximumFrameBytes
		);

		Assert.Equal( TerminalResponseFrameParseStatus.Complete, result.Status );
		Assert.Equal( frame.Length, result.Length );

		TerminalResponseFrameParseResult c1Terminator = TerminalResponseFramer.Parse(
			[ 0x1B, (byte)'_', (byte)'G', 0x9C ],
			TerminalControlFamily.Apc,
			TerminalResponseFramer.DefaultMaximumFrameBytes
		);
		Assert.Equal( TerminalResponseFrameParseStatus.Invalid, c1Terminator.Status );
	}

	[Fact]
	public void ApcEightBitFrameRequiresEightBitStringTerminator() {
		byte[] frame = [ 0x9F, (byte)'G', (byte)'a', (byte)'=', (byte)'q', 0x9C ];

		TerminalResponseFrameParseResult result = TerminalResponseFramer.Parse(
			frame,
			TerminalControlFamily.Apc,
			TerminalResponseFramer.DefaultMaximumFrameBytes
		);

		Assert.Equal( TerminalResponseFrameParseStatus.Complete, result.Status );
		Assert.Equal( frame.Length, result.Length );

		TerminalResponseFrameParseResult sevenBitTerminator = TerminalResponseFramer.Parse(
			[ 0x9F, (byte)'G', 0x1B, (byte)'\\' ],
			TerminalControlFamily.Apc,
			TerminalResponseFramer.DefaultMaximumFrameBytes
		);
		Assert.Equal( TerminalResponseFrameParseStatus.Invalid, sevenBitTerminator.Status );
	}

	[Fact]
	public void PmAndSosUseTheSameStrictStringFramingRules() {
		AssertComplete(
			[ 0x1B, (byte)'^', (byte)'p', (byte)'m', 0x1B, (byte)'\\' ],
			TerminalControlFamily.Pm
		);
		AssertComplete(
			[ 0x9E, (byte)'p', (byte)'m', 0x9C ],
			TerminalControlFamily.Pm
		);
		AssertComplete(
			[ 0x1B, (byte)'X', (byte)'s', (byte)'o', (byte)'s', 0x1B, (byte)'\\' ],
			TerminalControlFamily.Sos
		);
		AssertComplete(
			[ 0x98, (byte)'s', (byte)'o', (byte)'s', 0x9C ],
			TerminalControlFamily.Sos
		);
	}

	[Fact]
	public void StrictStringFamiliesRejectBellAndCancellationBytes() {
		TerminalResponseFrameParseResult bell = TerminalResponseFramer.Parse(
			[ 0x1B, (byte)'_', (byte)'G', 0x07 ],
			TerminalControlFamily.Apc,
			TerminalResponseFramer.DefaultMaximumFrameBytes
		);
		Assert.Equal( TerminalResponseFrameParseStatus.Incomplete, bell.Status );

		TerminalResponseFrameParseResult cancel = TerminalResponseFramer.Parse(
			[ 0x1B, (byte)'_', (byte)'G', 0x18 ],
			TerminalControlFamily.Apc,
			TerminalResponseFramer.DefaultMaximumFrameBytes
		);
		Assert.Equal( TerminalResponseFrameParseStatus.Invalid, cancel.Status );

		TerminalResponseFrameParseResult substitute = TerminalResponseFramer.Parse(
			[ 0x1B, (byte)'_', (byte)'G', 0x1A ],
			TerminalControlFamily.Apc,
			TerminalResponseFramer.DefaultMaximumFrameBytes
		);
		Assert.Equal( TerminalResponseFrameParseStatus.Invalid, substitute.Status );
	}

	[Fact]
	public void StrictStringFamilyIncompleteEscapeRemainsIncomplete() {
		TerminalResponseFrameParseResult result = TerminalResponseFramer.Parse(
			[ 0x1B, (byte)'_', (byte)'G', 0x1B ],
			TerminalControlFamily.Apc,
			TerminalResponseFramer.DefaultMaximumFrameBytes
		);

		Assert.Equal( TerminalResponseFrameParseStatus.Incomplete, result.Status );
	}

	[Fact]
	public void StrictStringFamilyHonorsMaximumFrameBytes() {
		TerminalResponseFrameParseResult result = TerminalResponseFramer.Parse(
			[ 0x1B, (byte)'_', (byte)'G', (byte)'x' ],
			TerminalControlFamily.Apc,
			4
		);

		Assert.Equal( TerminalResponseFrameParseStatus.Invalid, result.Status );
	}

	[Fact]
	public void FramerRejectsUnknownNormalizedFamily() {
		Assert.Throws<ArgumentOutOfRangeException>(
			() => TerminalResponseFramer.Parse(
				[ 0x1B, (byte)'[', (byte)'c' ],
				(TerminalControlFamily)int.MaxValue,
				TerminalResponseFramer.DefaultMaximumFrameBytes
			)
		);
	}

	private static void AssertSameResult(
		IReadOnlyList<byte> bytes,
		TerminalResponseFrameKind legacyKind,
		TerminalControlFamily family
	) {
		TerminalResponseFrameParseResult legacy = TerminalResponseFramer.Parse(
			bytes,
			legacyKind,
			TerminalResponseFramer.DefaultMaximumFrameBytes
		);
		TerminalResponseFrameParseResult normalized = TerminalResponseFramer.Parse(
			bytes,
			family,
			TerminalResponseFramer.DefaultMaximumFrameBytes
		);

		Assert.Equal( legacy.Status, normalized.Status );
		Assert.Equal( legacy.Length, normalized.Length );
		Assert.Equal( legacy.IntroducerIncomplete, normalized.IntroducerIncomplete );
	}

	private static void AssertComplete(
		IReadOnlyList<byte> bytes,
		TerminalControlFamily family
	) {
		TerminalResponseFrameParseResult result = TerminalResponseFramer.Parse(
			bytes,
			family,
			TerminalResponseFramer.DefaultMaximumFrameBytes
		);

		Assert.Equal( TerminalResponseFrameParseStatus.Complete, result.Status );
		Assert.Equal( bytes.Count, result.Length );
	}
}
