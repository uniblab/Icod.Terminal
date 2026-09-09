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
namespace Icod.Terminal.Tests.Output;

using System.Text;
using Icod.Terminal;
using Xunit;

/// <summary>
/// Verifies byte-exact OSC 633 framing, VS Code serialization, bounds, and commit behavior.
/// </summary>
public sealed class Osc633VsCodeShellIntegrationWriterTests {
	[Theory]
	[InlineData( "A", "\u001b]633;A\u001b\\" )]
	[InlineData( "B", "\u001b]633;B\u001b\\" )]
	[InlineData( "C", "\u001b]633;C\u001b\\" )]
	[InlineData( "D", "\u001b]633;D\u001b\\" )]
	public void MarkersUseCanonicalStFrames(
		string marker,
		string expected
	) {
		byte[] actual = marker switch {
			"A" => OscWriter.EncodeOsc633PromptStartFrame(),
			"B" => OscWriter.EncodeOsc633CommandInputStartFrame(),
			"C" => OscWriter.EncodeOsc633CommandOutputStartFrame(),
			"D" => OscWriter.EncodeOsc633CommandAbortedFrame(),
			_ => throw new InvalidOperationException()
		};

		Assert.Equal(
			Encoding.ASCII.GetBytes( expected ),
			actual
		);
	}

	[Theory]
	[InlineData( 0, "\u001b]633;D;0\u001b\\" )]
	[InlineData( 1, "\u001b]633;D;1\u001b\\" )]
	[InlineData( -1, "\u001b]633;D;-1\u001b\\" )]
	[InlineData( 255, "\u001b]633;D;255\u001b\\" )]
	[InlineData( int.MaxValue, "\u001b]633;D;2147483647\u001b\\" )]
	[InlineData( int.MinValue, "\u001b]633;D;-2147483648\u001b\\" )]
	public void CompletionEncodesSignedDecimalExitCodeExactly(
		int exitCode,
		string expected
	) {
		Assert.Equal(
			Encoding.ASCII.GetBytes( expected ),
			OscWriter.EncodeOsc633CommandFinishedFrame( exitCode )
		);
	}

	[Fact]
	public void CommandLineUsesVsCodeEscapingAndStrictUtf8() {
		Assert.Equal(
			Encoding.UTF8.GetBytes(
				"\u001b]633;E;printf\\x20café\\x3b\\x0a\\\\done;01234567-89ab-cdef-0123-456789abcdef\u001b\\"
			),
			OscWriter.EncodeOsc633CommandLineFrame(
				"printf café;\n\\done",
				"01234567-89ab-cdef-0123-456789abcdef"
			)
		);
	}

	[Fact]
	public void CurrentDirectoryPropertyUsesVsCodeEscapingAndOptionalNonce() {
		Assert.Equal(
			Encoding.UTF8.GetBytes(
				"\u001b]633;P;Cwd=C:\\\\work\\x3bset;nonce-1\u001b\\"
			),
			OscWriter.EncodeOsc633CurrentDirectoryFrame(
				"C:\\work;set",
				"nonce-1"
			)
		);
	}

	[Theory]
	[InlineData( true, "\u001b]633;P;IsWindows=True\u001b\\" )]
	[InlineData( false, "\u001b]633;P;IsWindows=False\u001b\\" )]
	public void IsWindowsPropertyUsesDocumentedBooleanSpelling(
		bool value,
		string expected
	) {
		Assert.Equal(
			Encoding.ASCII.GetBytes( expected ),
			OscWriter.EncodeOsc633IsWindowsFrame( value )
		);
	}

	[Theory]
	[InlineData( true, "\u001b]633;P;HasRichCommandDetection=True\u001b\\" )]
	[InlineData( false, "\u001b]633;P;HasRichCommandDetection=False\u001b\\" )]
	public void RichCommandDetectionPropertyUsesDocumentedBooleanSpelling(
		bool value,
		string expected
	) {
		Assert.Equal(
			Encoding.ASCII.GetBytes( expected ),
			OscWriter.EncodeOsc633RichCommandDetectionFrame( value )
		);
	}

	[Fact]
	public void SerializerMatchesDocumentedVsCodeExamples() {
		Assert.Equal( "\\\\", TerminalOsc633Encoder.SerializeMessage( "\\" ) );
		Assert.Equal( "\\x0a", TerminalOsc633Encoder.SerializeMessage( "\n" ) );
		Assert.Equal( "\\x3b", TerminalOsc633Encoder.SerializeMessage( ";" ) );
		Assert.Equal( "a\\x20b", TerminalOsc633Encoder.SerializeMessage( "a b" ) );
	}

	[Fact]
	public void UnsafeNonceIsRejected() {
		Assert.Throws<ArgumentException>(
			() => OscWriter.EncodeOsc633CommandLineFrame(
				"echo ok",
				"bad;nonce"
			)
		);
	}

	[Fact]
	public void MalformedUnicodeIsRejected() {
		Assert.Throws<ArgumentException>(
			() => OscWriter.EncodeOsc633CommandLineFrame(
				"\ud800"
			)
		);
	}

	[Fact]
	public void OversizedPayloadIsRejected() {
		string commandLine = new(
			'a',
			TerminalOsc633Encoder.MaximumPayloadLength
		);

		Assert.Throws<ArgumentException>(
			() => OscWriter.EncodeOsc633CommandLineFrame( commandLine )
		);
	}

	[Fact]
	public async Task WriterCommitsExactlyOneNoncancellableWriteWithoutFlush() {
		RecordingOutput output = new();

		await OscWriter.WriteOsc633CommandLineAsync(
			output,
			"echo hello",
			"nonce"
		);

		Assert.Single( output.Writes );
		Assert.Equal(
			Encoding.ASCII.GetBytes( "\u001b]633;E;echo\\x20hello;nonce\u001b\\" ),
			output.Writes[ 0 ]
		);
		Assert.False( output.WriteCancellationTokens[ 0 ].CanBeCanceled );
		Assert.Equal( 0, output.FlushCount );
	}

	[Fact]
	public async Task PreCancelledWriterEmitsNothing() {
		RecordingOutput output = new();
		using CancellationTokenSource cancellation = new();
		cancellation.Cancel();

		await Assert.ThrowsAnyAsync<OperationCanceledException>(
			() => OscWriter.WriteOsc633PromptStartAsync(
				output,
				cancellation.Token
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
