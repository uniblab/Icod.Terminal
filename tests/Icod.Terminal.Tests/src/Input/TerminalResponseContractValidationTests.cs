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
/// Verifies validation invariants on the internal T22 response-routing contracts.
/// </summary>
public sealed class TerminalResponseContractValidationTests {
	[Fact]
	public void ResponseFrameRejectsUnknownFrameKind() {
		Assert.Throws<ArgumentOutOfRangeException>(
			() => new TerminalResponseFrame(
				(TerminalResponseFrameKind)int.MaxValue,
				[ 0x9B, (byte)'R' ]
			)
		);
	}

	[Fact]
	public void ResponseFramerRejectsUnknownFrameKind() {
		Assert.Throws<ArgumentOutOfRangeException>(
			() => TerminalResponseFramer.Parse(
				[ 0x9B, (byte)'R' ],
				(TerminalResponseFrameKind)int.MaxValue,
				TerminalResponseFramer.DefaultMaximumFrameBytes
			)
		);
	}

	[Fact]
	public void ParseResultRejectsUnknownStatus() {
		Assert.Throws<ArgumentOutOfRangeException>(
			() => new TerminalResponseFrameParseResult(
				(TerminalResponseFrameParseStatus)int.MaxValue
			)
		);
	}

	[Fact]
	public void CompleteParseResultRequiresPositiveLength() {
		Assert.Throws<ArgumentOutOfRangeException>(
			() => new TerminalResponseFrameParseResult(
				TerminalResponseFrameParseStatus.Complete
			)
		);
	}

	[Fact]
	public void NonCompleteParseResultRejectsPositiveLength() {
		Assert.Throws<ArgumentException>(
			() => new TerminalResponseFrameParseResult(
				TerminalResponseFrameParseStatus.NotCandidate,
				length: 1
			)
		);
	}

	[Fact]
	public void IncompleteIntroducerRequiresIncompleteStatus() {
		Assert.Throws<ArgumentException>(
			() => new TerminalResponseFrameParseResult(
				TerminalResponseFrameParseStatus.NotCandidate,
				introducerIncomplete: true
			)
		);
	}
}
