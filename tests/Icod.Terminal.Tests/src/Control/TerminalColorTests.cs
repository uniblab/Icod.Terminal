namespace Icod.Terminal.Tests.Control;

using Icod.Terminal;
using Xunit;

/// <summary>
/// Verifies the T131 public terminal-color value contract.
/// </summary>
public sealed class TerminalColorTests {
	[Fact]
	public void ConstructorPreservesSixteenBitChannels() {
		TerminalColor color = new(
			0x1234,
			0xabcd,
			0xffff
		);

		Assert.Equal( 0x1234, color.Red );
		Assert.Equal( 0xabcd, color.Green );
		Assert.Equal( 0xffff, color.Blue );
	}

	[Theory]
	[InlineData( 0, 0x0000 )]
	[InlineData( 1, 0x0101 )]
	[InlineData( 127, 0x7f7f )]
	[InlineData( 128, 0x8080 )]
	[InlineData( 254, 0xfefe )]
	[InlineData( 255, 0xffff )]
	public void FromRgb8ReplicatesByteAcrossSixteenBits(
		int source,
		int expected
	) {
		TerminalColor color = TerminalColor.FromRgb8(
			(byte)source,
			(byte)source,
			(byte)source
		);

		Assert.Equal( expected, color.Red );
		Assert.Equal( expected, color.Green );
		Assert.Equal( expected, color.Blue );
	}

	[Fact]
	public void EqualityUsesNormalizedChannels() {
		TerminalColor left = new( 1, 2, 3 );
		TerminalColor same = new( 1, 2, 3 );
		TerminalColor different = new( 1, 2, 4 );

		Assert.True( left.Equals( same ) );
		Assert.True( left.Equals( (object)same ) );
		Assert.True( left == same );
		Assert.False( left != same );
		Assert.Equal( left.GetHashCode(), same.GetHashCode() );

		Assert.False( left.Equals( different ) );
		Assert.False( left == different );
		Assert.True( left != different );
		Assert.False( left.Equals( null ) );
	}

	[Fact]
	public void DefaultValueIsBlack() {
		TerminalColor color = default;

		Assert.Equal( 0, color.Red );
		Assert.Equal( 0, color.Green );
		Assert.Equal( 0, color.Blue );
		Assert.Equal( new TerminalColor( 0, 0, 0 ), color );
	}
}
