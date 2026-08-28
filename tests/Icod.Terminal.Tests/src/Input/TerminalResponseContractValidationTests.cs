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
