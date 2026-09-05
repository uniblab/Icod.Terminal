namespace Icod.Terminal.Tests.Control;

using Icod.Terminal;
using Xunit;

/// <summary>
/// Verifies the T82 cursor-style semantic codec without terminal I/O.
/// </summary>
public sealed class TerminalCursorStyleCodecTests {
	[Theory]
	[InlineData( TerminalCursorStyle.BlinkingBlock, 1 )]
	[InlineData( TerminalCursorStyle.SteadyBlock, 2 )]
	[InlineData( TerminalCursorStyle.BlinkingUnderline, 3 )]
	[InlineData( TerminalCursorStyle.SteadyUnderline, 4 )]
	[InlineData( TerminalCursorStyle.BlinkingBar, 5 )]
	[InlineData( TerminalCursorStyle.SteadyBar, 6 )]
	public void SemanticStylesMapToFrozenParameters(
		TerminalCursorStyle style,
		int expected
	) {
		Assert.Equal(
			expected,
			TerminalCursorStyleCodec.GetParameter( style )
		);
	}

	[Fact]
	public void UndefinedSemanticStyleIsRejected() {
		Assert.Throws<ArgumentOutOfRangeException>(
			() => TerminalCursorStyleCodec.GetParameter(
				(TerminalCursorStyle)int.MaxValue
			)
		);
	}

	[Theory]
	[InlineData( " q", TerminalCursorStyle.BlinkingBlock )]
	[InlineData( "0 q", TerminalCursorStyle.BlinkingBlock )]
	[InlineData( "1 q", TerminalCursorStyle.BlinkingBlock )]
	[InlineData( "0001 q", TerminalCursorStyle.BlinkingBlock )]
	[InlineData( "2 q", TerminalCursorStyle.SteadyBlock )]
	[InlineData( "03 q", TerminalCursorStyle.BlinkingUnderline )]
	[InlineData( "4 q", TerminalCursorStyle.SteadyUnderline )]
	[InlineData( "5 q", TerminalCursorStyle.BlinkingBar )]
	[InlineData( "006 q", TerminalCursorStyle.SteadyBar )]
	public void RecognizedStatusStringsMapToSemanticStyles(
		string statusString,
		TerminalCursorStyle expected
	) {
		Assert.Equal(
			expected,
			TerminalCursorStyleCodec.ParseStatusString( statusString )
		);
	}

	[Theory]
	[InlineData( "" )]
	[InlineData( "q" )]
	[InlineData( "1q" )]
	[InlineData( "1 m" )]
	[InlineData( "1;2 q" )]
	[InlineData( "?1 q" )]
	[InlineData( "-1 q" )]
	[InlineData( "1  q" )]
	[InlineData( "7 q" )]
	[InlineData( "8 q" )]
	[InlineData( "999 q" )]
	[InlineData( "1 $q" )]
	public void MalformedOrUnrecognizedStatusStringsFailDeterministically(
		string statusString
	) {
		Assert.Throws<FormatException>(
			() => TerminalCursorStyleCodec.ParseStatusString( statusString )
		);
	}

	[Fact]
	public void NumericOverflowFailsAsFormatError() {
		string statusString = new( '9', 64 );
		statusString += " q";

		Assert.Throws<FormatException>(
			() => TerminalCursorStyleCodec.ParseStatusString( statusString )
		);
	}

	[Fact]
	public void NullStatusStringIsRejected() {
		Assert.Throws<ArgumentNullException>(
			() => TerminalCursorStyleCodec.ParseStatusString( null! )
		);
	}
}
