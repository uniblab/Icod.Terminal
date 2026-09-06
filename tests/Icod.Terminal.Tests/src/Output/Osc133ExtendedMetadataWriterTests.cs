namespace Icod.Terminal.Tests.Output;

using System.Text;
using Icod.Terminal;
using Xunit;

/// <summary>
/// Verifies T151 byte-exact extended OSC 133 metadata encoding and bounds.
/// </summary>
public sealed class Osc133ExtendedMetadataWriterTests {
	[Fact]
	public void DefaultPromptMetadataMatchesBarePromptFrame() {
		Assert.Equal(
			OscWriter.EncodeOsc133PromptStartFrame(),
			OscWriter.EncodeOsc133PromptStartFrame(
				shellDoesNotRedrawPrompt: false,
				useSpecialCursorKey: false,
				secondaryPrompt: false,
				clickEvents: 0
			)
		);
	}

	[Fact]
	public void PromptMetadataUsesCanonicalParameterOrder() {
		Assert.Equal(
			Encoding.ASCII.GetBytes(
				"\u001b]133;A;redraw=0;special_key=1;k=s;click_events=2\u001b\\"
			),
			OscWriter.EncodeOsc133PromptStartFrame(
				shellDoesNotRedrawPrompt: true,
				useSpecialCursorKey: true,
				secondaryPrompt: true,
				clickEvents: 2
			)
		);
	}

	[Theory]
	[InlineData( false, false, false, 1, "\u001b]133;A;click_events=1\u001b\\" )]
	[InlineData( false, false, true, 0, "\u001b]133;A;k=s\u001b\\" )]
	[InlineData( true, false, false, 0, "\u001b]133;A;redraw=0\u001b\\" )]
	[InlineData( false, true, false, 0, "\u001b]133;A;special_key=1\u001b\\" )]
	public void PromptMetadataEncodesEachSupportedField(
		bool shellDoesNotRedrawPrompt,
		bool useSpecialCursorKey,
		bool secondaryPrompt,
		byte clickEvents,
		string expected
	) {
		Assert.Equal(
			Encoding.ASCII.GetBytes( expected ),
			OscWriter.EncodeOsc133PromptStartFrame(
				shellDoesNotRedrawPrompt,
				useSpecialCursorKey,
				secondaryPrompt,
				clickEvents
			)
		);
	}

	[Fact]
	public void UndefinedPromptClickModeIsRejected() {
		Assert.Throws<ArgumentOutOfRangeException>(
			() => OscWriter.EncodeOsc133PromptStartFrame(
				shellDoesNotRedrawPrompt: false,
				useSpecialCursorKey: false,
				secondaryPrompt: false,
				clickEvents: 3
			)
		);
	}

	[Fact]
	public void EmptyCommandLineIsDistinctFromBareCommandOutput() {
		Assert.Equal(
			Encoding.ASCII.GetBytes( "\u001b]133;C;cmdline_url=\u001b\\" ),
			OscWriter.EncodeOsc133CommandOutputStartFrame( string.Empty )
		);
		Assert.NotEqual(
			OscWriter.EncodeOsc133CommandOutputStartFrame(),
			OscWriter.EncodeOsc133CommandOutputStartFrame( string.Empty )
		);
	}

	[Fact]
	public void CommandLineUsesStrictUtf8BytePercentEncoding() {
		Assert.Equal(
			Encoding.ASCII.GetBytes(
				"\u001b]133;C;cmdline_url=echo%20caf%C3%A9%20%F0%9F%98%80\u001b\\"
			),
			OscWriter.EncodeOsc133CommandOutputStartFrame(
				"echo café 😀"
			)
		);
	}

	[Fact]
	public void CommandLineEncodesProtocolAndShellMetacharacters() {
		Assert.Equal(
			Encoding.ASCII.GetBytes(
				"\u001b]133;C;cmdline_url=%25%3B%3D%07%1B%0D%0A%00%09%22%5C%24%26%7C%3C%3E%60\u001b\\"
			),
			OscWriter.EncodeOsc133CommandOutputStartFrame(
				"%;=\a\u001b\r\n\0\t\"\\$&|<>`"
			)
		);
	}

	[Fact]
	public void CommandLineLeavesOnlyRfc3986UnreservedAsciiLiteral() {
		Assert.Equal(
			Encoding.ASCII.GetBytes(
				"\u001b]133;C;cmdline_url=AZaz09-._~\u001b\\"
			),
			OscWriter.EncodeOsc133CommandOutputStartFrame(
				"AZaz09-._~"
			)
		);
	}

	[Fact]
	public void IllFormedUtf16IsRejectedBeforeFrameCreation() {
		Assert.Throws<ArgumentException>(
			() => OscWriter.EncodeOsc133CommandOutputStartFrame(
				"\ud800"
			)
		);
	}

	[Fact]
	public void ExactMaximumPayloadIsAccepted() {
		const int prefixLength = 18;
		string commandLine = new(
			'a',
			TerminalOsc133ExtendedMetadataEncoder.MaximumPayloadLength - prefixLength
		);

		byte[] frame = OscWriter.EncodeOsc133CommandOutputStartFrame(
			commandLine
		);

		Assert.Equal(
			TerminalOsc133ExtendedMetadataEncoder.MaximumPayloadLength + 4,
			frame.Length
		);
		Assert.Equal( 0x1b, frame[ 0 ] );
		Assert.Equal( (byte)']', frame[ 1 ] );
		Assert.Equal( 0x1b, frame[ ^2 ] );
		Assert.Equal( (byte)'\\', frame[ ^1 ] );
	}

	[Fact]
	public void OneByteOverMaximumPayloadIsRejected() {
		const int prefixLength = 18;
		string commandLine = new(
			'a',
			TerminalOsc133ExtendedMetadataEncoder.MaximumPayloadLength - prefixLength + 1
		);

		Assert.Throws<ArgumentException>(
			() => OscWriter.EncodeOsc133CommandOutputStartFrame(
				commandLine
			)
		);
	}

	[Fact]
	public async Task ExtendedWriterCommitsOneNoncancellableWriteWithoutFlush() {
		RecordingOutput output = new();

		await OscWriter.WriteOsc133CommandOutputStartAsync(
			output,
			"echo hello",
			CancellationToken.None
		);

		Assert.Single( output.Writes );
		Assert.Equal(
			Encoding.ASCII.GetBytes(
				"\u001b]133;C;cmdline_url=echo%20hello\u001b\\"
			),
			output.Writes[ 0 ]
		);
		Assert.False( output.WriteCancellationTokens[ 0 ].CanBeCanceled );
		Assert.Equal( 0, output.FlushCount );
	}

	[Fact]
	public async Task PreCancelledExtendedWriterEmitsNothing() {
		RecordingOutput output = new();
		using CancellationTokenSource cancellation = new();
		cancellation.Cancel();

		await Assert.ThrowsAnyAsync<OperationCanceledException>(
			() => OscWriter.WriteOsc133PromptStartAsync(
				output,
				shellDoesNotRedrawPrompt: true,
				useSpecialCursorKey: true,
				secondaryPrompt: true,
				clickEvents: 2,
				cancellationToken: cancellation.Token
			).AsTask()
		);

		Assert.Empty( output.Writes );
		Assert.Equal( 0, output.FlushCount );
	}

	private sealed class RecordingOutput : ITerminalOutput {
		internal List<byte[]> Writes {
			get;
		} = [];

		internal List<CancellationToken> WriteCancellationTokens {
			get;
		} = [];

		internal int FlushCount {
			get;
			private set;
		}

		public ValueTask WriteAsync(
			ReadOnlyMemory<byte> buffer,
			CancellationToken cancellationToken = default
		) {
			this.Writes.Add( buffer.ToArray() );
			this.WriteCancellationTokens.Add( cancellationToken );
			return ValueTask.CompletedTask;
		}

		public ValueTask FlushAsync(
			CancellationToken cancellationToken = default
		) {
			cancellationToken.ThrowIfCancellationRequested();
			++this.FlushCount;
			return ValueTask.CompletedTask;
		}
	}
}
