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
/// Verifies byte-exact OSC 777 notification framing, validation, bounds, and write semantics.
/// </summary>
public sealed class Osc777NotificationWriterTests {
	[Theory]
	[InlineData( "", "", "\u001b]777;notify;;\u001b\\" )]
	[InlineData( "Build", "Complete", "\u001b]777;notify;Build;Complete\u001b\\" )]
	public void NotificationEncodesCanonicalStFrame(
		string title,
		string message,
		string expected
	) {
		Assert.Equal(
			Encoding.ASCII.GetBytes( expected ),
			OscWriter.EncodeOsc777NotificationFrame(
				title,
				message
			)
		);
	}

	[Fact]
	public void NotificationEncodesUnicodeAsStrictUtf8() {
		string title = "café";
		string message = "done 😀";
		byte[] frame = OscWriter.EncodeOsc777NotificationFrame(
			title,
			message
		);
		byte[] expectedPayload = Encoding.UTF8.GetBytes(
			"777;notify;" + title + ";" + message
		);

		Assert.Equal( 0x1b, frame[ 0 ] );
		Assert.Equal( (byte)']', frame[ 1 ] );
		Assert.Equal(
			expectedPayload,
			frame.AsSpan( 2, expectedPayload.Length ).ToArray()
		);
		Assert.Equal( 0x1b, frame[ ^2 ] );
		Assert.Equal( (byte)'\\', frame[ ^1 ] );
	}

	[Theory]
	[InlineData( "bad;title", "body" )]
	[InlineData( "title", "bad;body" )]
	public void NotificationRejectsAmbiguousSemicolonFields(
		string title,
		string message
	) {
		Assert.Throws<ArgumentException>(
			() => OscWriter.EncodeOsc777NotificationFrame(
				title,
				message
			)
		);
	}

	[Fact]
	public void NotificationRejectsControlsInEitherField() {
		foreach ( char control in EnumerateForbiddenControls() ) {
			Assert.Throws<ArgumentException>(
				() => OscWriter.EncodeOsc777NotificationFrame(
					"before" + control + "after",
					"body"
				)
			);
			Assert.Throws<ArgumentException>(
				() => OscWriter.EncodeOsc777NotificationFrame(
					"title",
					"before" + control + "after"
				)
			);
		}
	}

	[Fact]
	public void NotificationRejectsMalformedUnicode() {
		Assert.Throws<ArgumentException>(
			() => OscWriter.EncodeOsc777NotificationFrame(
				"bad\ud800title",
				"body"
			)
		);
		Assert.Throws<ArgumentException>(
			() => OscWriter.EncodeOsc777NotificationFrame(
				"title",
				"bad\udc00body"
			)
		);
	}

	[Fact]
	public void NotificationEnforcesCompletePayloadByteLimit() {
		const int protocolOverhead = 12;
		string exact = new(
			'a',
			TerminalOsc777NotificationEncoder.MaximumPayloadLength - protocolOverhead
		);
		byte[] frame = OscWriter.EncodeOsc777NotificationFrame(
			string.Empty,
			exact
		);

		Assert.Equal(
			TerminalOsc777NotificationEncoder.MaximumPayloadLength + 4,
			frame.Length
		);
		Assert.Throws<ArgumentException>(
			() => OscWriter.EncodeOsc777NotificationFrame(
				string.Empty,
				exact + "a"
			)
		);
	}

	[Fact]
	public async Task WriterCommitsOneCompleteNoncancellableFrameWithoutFlush() {
		RecordingTerminalOutput output = new();

		await OscWriter.WriteOsc777NotificationAsync(
			output,
			"Build",
			"Complete"
		);

		Assert.Single( output.Writes );
		Assert.Equal(
			Encoding.ASCII.GetBytes( "\u001b]777;notify;Build;Complete\u001b\\" ),
			output.Writes[ 0 ]
		);
		Assert.False( output.LastWriteCancellationToken.CanBeCanceled );
		Assert.Equal( 0, output.FlushCount );
	}

	[Fact]
	public async Task PreCancelledWriterEmitsNothing() {
		RecordingTerminalOutput output = new();
		using CancellationTokenSource cancellation = new();
		cancellation.Cancel();

		await Assert.ThrowsAnyAsync<OperationCanceledException>(
			() => OscWriter.WriteOsc777NotificationAsync(
				output,
				"Build",
				"Complete",
				cancellation.Token
			).AsTask()
		);

		Assert.Empty( output.Writes );
	}

	private static IEnumerable<char> EnumerateForbiddenControls() {
		for ( int value = 0x0000; value <= 0x001f; ++value ) {
			yield return (char)value;
		}
		yield return '\u007f';
		for ( int value = 0x0080; value <= 0x009f; ++value ) {
			yield return (char)value;
		}
	}

	private sealed class RecordingTerminalOutput : ITerminalOutput {
		internal List<byte[]> Writes {
			get;
		} = [];

		internal int FlushCount {
			get;
			private set;
		}

		internal CancellationToken LastWriteCancellationToken {
			get;
			private set;
		}

		public ValueTask WriteAsync(
			ReadOnlyMemory<byte> buffer,
			CancellationToken cancellationToken = default
		) {
			this.LastWriteCancellationToken = cancellationToken;
			cancellationToken.ThrowIfCancellationRequested();
			this.Writes.Add( buffer.ToArray() );
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
