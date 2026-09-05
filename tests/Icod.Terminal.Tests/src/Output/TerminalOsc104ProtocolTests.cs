namespace Icod.Terminal.Tests.Output;

using System.Text;
using Icod.Terminal;
using Xunit;

/// <summary>
/// Verifies canonical OSC 104 indexed-palette reset framing and validation.
/// </summary>
public sealed class TerminalOsc104ProtocolTests {
	[Fact]
	public void ResetAllUsesBareOsc104WithCanonicalSt() {
		Assert.Equal(
			Encoding.ASCII.GetBytes( "\u001b]104\u001b\\" ),
			TerminalOsc104Protocol.CreateResetAllFrame()
		);
	}

	[Theory]
	[InlineData( (byte)0, "\u001b]104;0\u001b\\" )]
	[InlineData( (byte)9, "\u001b]104;9\u001b\\" )]
	[InlineData( (byte)10, "\u001b]104;10\u001b\\" )]
	[InlineData( (byte)99, "\u001b]104;99\u001b\\" )]
	[InlineData( (byte)100, "\u001b]104;100\u001b\\" )]
	[InlineData( byte.MaxValue, "\u001b]104;255\u001b\\" )]
	public void SingleResetUsesMinimalDecimalIndex(
		byte index,
		string expected
	) {
		Assert.Equal(
			Encoding.ASCII.GetBytes( expected ),
			TerminalOsc104Protocol.CreateResetFrame( index )
		);
	}

	[Fact]
	public void MultipleResetPreservesCallerOrder() {
		Assert.Equal(
			Encoding.ASCII.GetBytes( "\u001b]104;255;0;17;128\u001b\\" ),
			TerminalOsc104Protocol.CreateResetFrame(
				new byte[] { 255, 0, 17, 128 }
			)
		);
	}

	[Fact]
	public void EmptyIndexedResetIsRejectedInsteadOfBecomingResetAll() {
		Assert.Throws<ArgumentException>(
			() => TerminalOsc104Protocol.CreateResetFrame( Array.Empty<byte>() )
		);
	}

	[Fact]
	public void DuplicateIndexIsRejectedBeforeFrameConstruction() {
		Assert.Throws<ArgumentException>(
			() => TerminalOsc104Protocol.CreateResetFrame(
				new byte[] { 7, 9, 7 }
			)
		);
	}

	[Fact]
	public void MoreThanCompletePaletteDomainIsRejected() {
		byte[] indices = new byte[ 257 ];
		Assert.Throws<ArgumentException>(
			() => TerminalOsc104Protocol.CreateResetFrame( indices )
		);
	}

	[Fact]
	public void CompletePaletteDomainCanBeExplicitlyResetByIndex() {
		byte[] indices = Enumerable.Range( 0, 256 )
			.Select( value => (byte)value )
			.ToArray();

		byte[] frame = TerminalOsc104Protocol.CreateResetFrame( indices );
		string text = Encoding.ASCII.GetString( frame );
		Assert.StartsWith( "\u001b]104;0;1;2;3", text );
		Assert.EndsWith( ";253;254;255\u001b\\", text );
	}
}
