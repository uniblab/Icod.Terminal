namespace Icod.Terminal.Tests.Output;

using System.Text;
using Icod.Terminal;
using Xunit;

/// <summary>
/// Verifies the T30 internal OSC writer contract with byte-exact fixtures.
/// </summary>
public sealed class OscWriterTests {
	[Theory]
	[InlineData( 0, "", "1B5D303B1B5C" )]
	[InlineData( 0, "abc", "1B5D303B6162631B5C" )]
	[InlineData( 1, "icon", "1B5D313B69636F6E1B5C" )]
	[InlineData( 2, "title", "1B5D323B7469746C651B5C" )]
	public void EncodesExpectedFrames(
		int selectorValue,
		string value,
		string expectedHex
	) {
		OscTitleSelector selector = (OscTitleSelector)selectorValue;
		byte[] actual = OscWriter.EncodeTitleFrame(
			selector,
			value
		);

		Assert.Equal(
			Convert.FromHexString( expectedHex ),
			actual
		);
	}

	[Fact]
	public void EncodesMultilingualPayloadAsStrictUtf8() {
		const string value = "éλ猫";
		byte[] payload = Encoding.UTF8.GetBytes( value );
		byte[] expected = new byte[ payload.Length + 6 ];
		expected[ 0 ] = 0x1b;
		expected[ 1 ] = 0x5d;
		expected[ 2 ] = 0x30;
		expected[ 3 ] = 0x3b;
		payload.CopyTo( expected, 4 );
		expected[ ^2 ] = 0x1b;
		expected[ ^1 ] = 0x5c;

		Assert.Equal(
			expected,
			OscWriter.EncodeTitleFrame(
				OscTitleSelector.IconAndWindowTitle,
				value
			)
		);
	}

	[Fact]
	public void AcceptsMaximumEncodedPayload() {
		string value = new(
			'a',
			OscWriter.MaximumTitlePayloadByteCount
		);

		byte[] frame = OscWriter.EncodeTitleFrame(
			OscTitleSelector.WindowTitle,
			value
		);

		Assert.Equal(
			OscWriter.MaximumTitlePayloadByteCount + 6,
			frame.Length
		);
	}

	[Fact]
	public void RejectsPayloadBeyondMaximumEncodedSize() {
		string value = new(
			'a',
			OscWriter.MaximumTitlePayloadByteCount + 1
		);

		Assert.Throws<ArgumentException>(
			() => OscWriter.EncodeTitleFrame(
				OscTitleSelector.WindowTitle,
				value
			)
		);
	}

	[Theory]
	[InlineData( "\u0000" )]
	[InlineData( "\u0007" )]
	[InlineData( "\u0009" )]
	[InlineData( "\u000a" )]
	[InlineData( "\u000d" )]
	[InlineData( "\u001b" )]
	[InlineData( "\u007f" )]
	[InlineData( "\u0085" )]
	[InlineData( "\u009c" )]
	public void RejectsControlCharacters(
		string value
	) {
		Assert.Throws<ArgumentException>(
			() => OscWriter.EncodeTitleFrame(
				OscTitleSelector.IconAndWindowTitle,
				value
			)
		);
	}

	[Fact]
	public void RejectsUnpairedSurrogate() {
		Assert.Throws<ArgumentException>(
			() => OscWriter.EncodeTitleFrame(
				OscTitleSelector.IconAndWindowTitle,
				"\ud800"
			)
		);
	}

	[Fact]
	public void RejectsUnsupportedSelector() {
		Assert.Throws<ArgumentOutOfRangeException>(
			() => OscWriter.EncodeTitleFrame(
				(OscTitleSelector)3,
				"title"
			)
		);
	}

	[Fact]
	public async Task RejectedPayloadWritesNoBytes() {
		RecordingTerminalOutput output = new();

		await Assert.ThrowsAsync<ArgumentException>(
			() => OscWriter.WriteTitleAsync(
				output,
				OscTitleSelector.WindowTitle,
				"bad\u001btitle"
			).AsTask()
		);

		Assert.Empty( output.Bytes );
		Assert.Equal( 0, output.WriteCount );
	}

	[Fact]
	public async Task WritesCompleteFrameInOneCall() {
		RecordingTerminalOutput output = new();

		await OscWriter.WriteTitleAsync(
			output,
			OscTitleSelector.WindowTitle,
			"title"
		);

		Assert.Equal( 1, output.WriteCount );
		Assert.Equal(
			Convert.FromHexString( "1B5D323B7469746C651B5C" ),
			output.Bytes.ToArray()
		);
	}

	[Fact]
	public async Task CancellationBeforeTransmissionWritesNothing() {
		RecordingTerminalOutput output = new();
		using CancellationTokenSource cancellationSource = new();
		cancellationSource.Cancel();

		await Assert.ThrowsAnyAsync<OperationCanceledException>(
			() => OscWriter.WriteTitleAsync(
				output,
				OscTitleSelector.WindowTitle,
				"title",
				cancellationSource.Token
			).AsTask()
		);

		Assert.Empty( output.Bytes );
		Assert.Equal( 0, output.WriteCount );
	}

	private sealed class RecordingTerminalOutput : ITerminalOutput {
		internal List<byte> Bytes {
			get;
		} = [];

		internal int WriteCount {
			get;
			private set;
		}

		public ValueTask WriteAsync(
			ReadOnlyMemory<byte> buffer,
			CancellationToken cancellationToken = default
		) {
			cancellationToken.ThrowIfCancellationRequested();
			++this.WriteCount;
			this.Bytes.AddRange( buffer.ToArray() );
			return ValueTask.CompletedTask;
		}

		public ValueTask FlushAsync(
			CancellationToken cancellationToken = default
		) {
			cancellationToken.ThrowIfCancellationRequested();
			return ValueTask.CompletedTask;
		}
	}
}
