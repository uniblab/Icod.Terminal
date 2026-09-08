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
/// Verifies the T161 bounded OSC 9 notification and 9;9 compatibility writers.
/// </summary>
public sealed class Osc9SafeTextWriterTests {
	[Theory]
	[InlineData( "", "\u001b]9;\u001b\\" )]
	[InlineData( "Build complete", "\u001b]9;Build complete\u001b\\" )]
	[InlineData( "100%; done", "\u001b]9;100%; done\u001b\\" )]
	public void NotificationEncodesExpectedAsciiFrame(
		string message,
		string expected
	) {
		Assert.Equal(
			Encoding.ASCII.GetBytes( expected ),
			OscWriter.EncodeOsc9NotificationFrame( message )
		);
	}

	[Fact]
	public void NotificationEncodesUnicodeAsUtf8() {
		string message = "café 😀";
		byte[] expected = [
			0x1b, 0x5d, 0x39, 0x3b,
			0x63, 0x61, 0x66, 0xc3, 0xa9, 0x20,
			0xf0, 0x9f, 0x98, 0x80,
			0x1b, 0x5c
		];

		Assert.Equal(
			expected,
			OscWriter.EncodeOsc9NotificationFrame( message )
		);
	}

	[Fact]
	public void NotificationRejectsEveryC0DelAndC1Control() {
		foreach ( char control in EnumerateForbiddenControls() ) {
			Assert.Throws<ArgumentException>(
				() => OscWriter.EncodeOsc9NotificationFrame(
					"before" + control + "after"
				)
			);
		}
	}

	[Fact]
	public void NotificationRejectsMalformedUnicode() {
		Assert.Throws<ArgumentException>(
			() => OscWriter.EncodeOsc9NotificationFrame( "bad\ud800text" )
		);
	}

	[Fact]
	public void NotificationAcceptsExactPayloadLimit() {
		string message = new(
			'a',
			TerminalOsc9SafeTextEncoder.MaximumNotificationPayloadLength - 2
		);
		byte[] frame = OscWriter.EncodeOsc9NotificationFrame( message );

		Assert.Equal(
			TerminalOsc9SafeTextEncoder.MaximumNotificationPayloadLength + 4,
			frame.Length
		);
	}

	[Fact]
	public void NotificationRejectsOneByteOverPayloadLimit() {
		string message = new(
			'a',
			TerminalOsc9SafeTextEncoder.MaximumNotificationPayloadLength - 1
		);

		Assert.Throws<ArgumentException>(
			() => OscWriter.EncodeOsc9NotificationFrame( message )
		);
	}

	[Fact]
	public void NotificationPayloadLimitCountsUtf8Bytes() {
		string exact = new(
			'é',
			( TerminalOsc9SafeTextEncoder.MaximumNotificationPayloadLength - 2 ) / 2
		);
		byte[] frame = OscWriter.EncodeOsc9NotificationFrame( exact );

		Assert.Equal(
			TerminalOsc9SafeTextEncoder.MaximumNotificationPayloadLength + 4,
			frame.Length
		);
		Assert.Throws<ArgumentException>(
			() => OscWriter.EncodeOsc9NotificationFrame( exact + "a" )
		);
	}

	[Theory]
	[InlineData( "C:\\work dir\\repo", "\u001b]9;9;C:\\work dir\\repo\u001b\\" )]
	[InlineData( "C:\\A&B;C=D", "\u001b]9;9;C:\\A&B;C=D\u001b\\" )]
	public void WindowsCurrentDirectoryPreservesPrintablePath(
		string path,
		string expected
	) {
		Assert.Equal(
			Encoding.ASCII.GetBytes( expected ),
			OscWriter.EncodeOsc9WindowsCurrentDirectoryFrame( path )
		);
	}

	[Fact]
	public void WindowsCurrentDirectoryEncodesUnicodeAsUtf8() {
		string path = "C:\\café\\😀";
		byte[] frame = OscWriter.EncodeOsc9WindowsCurrentDirectoryFrame( path );
		byte[] expectedPayload = Encoding.UTF8.GetBytes( "9;9;" + path );

		Assert.Equal( 0x1b, frame[ 0 ] );
		Assert.Equal( (byte)']', frame[ 1 ] );
		Assert.Equal(
			expectedPayload,
			frame.AsSpan( 2, expectedPayload.Length ).ToArray()
		);
		Assert.Equal( 0x1b, frame[ ^2 ] );
		Assert.Equal( (byte)'\\', frame[ ^1 ] );
	}

	[Fact]
	public void WindowsCurrentDirectoryRejectsEmptyPath() {
		Assert.Throws<ArgumentException>(
			() => OscWriter.EncodeOsc9WindowsCurrentDirectoryFrame( string.Empty )
		);
	}

	[Fact]
	public void WindowsCurrentDirectoryRejectsEveryC0DelAndC1Control() {
		foreach ( char control in EnumerateForbiddenControls() ) {
			Assert.Throws<ArgumentException>(
				() => OscWriter.EncodeOsc9WindowsCurrentDirectoryFrame(
					"C:\\before" + control + "after"
				)
			);
		}
	}

	[Fact]
	public void WindowsCurrentDirectoryRejectsMalformedUnicode() {
		Assert.Throws<ArgumentException>(
			() => OscWriter.EncodeOsc9WindowsCurrentDirectoryFrame(
				"C:\\bad\udc00path"
			)
		);
	}

	[Fact]
	public void WindowsCurrentDirectoryAcceptsExactPayloadLimit() {
		string path = "C:\\" + new string(
			'a',
			TerminalOsc9SafeTextEncoder.MaximumWindowsCurrentDirectoryPayloadLength - 7
		);
		byte[] frame = OscWriter.EncodeOsc9WindowsCurrentDirectoryFrame( path );

		Assert.Equal(
			TerminalOsc9SafeTextEncoder.MaximumWindowsCurrentDirectoryPayloadLength + 4,
			frame.Length
		);
	}

	[Fact]
	public void WindowsCurrentDirectoryRejectsOneByteOverPayloadLimit() {
		string path = "C:\\" + new string(
			'a',
			TerminalOsc9SafeTextEncoder.MaximumWindowsCurrentDirectoryPayloadLength - 6
		);

		Assert.Throws<ArgumentException>(
			() => OscWriter.EncodeOsc9WindowsCurrentDirectoryFrame( path )
		);
	}

	[Fact]
	public void WindowsCurrentDirectoryPayloadLimitCountsUtf8Bytes() {
		const string prefix = "C:\\";
		int valueByteBudget = TerminalOsc9SafeTextEncoder.MaximumWindowsCurrentDirectoryPayloadLength
			- 4
			- prefix.Length;
		string exact = string.Concat(
			prefix,
			new string( 'é', valueByteBudget / 2 ),
			0 == valueByteBudget % 2
				? string.Empty
				: "a"
		);
		byte[] frame = OscWriter.EncodeOsc9WindowsCurrentDirectoryFrame( exact );

		Assert.Equal(
			TerminalOsc9SafeTextEncoder.MaximumWindowsCurrentDirectoryPayloadLength + 4,
			frame.Length
		);
		Assert.Throws<ArgumentException>(
			() => OscWriter.EncodeOsc9WindowsCurrentDirectoryFrame( exact + "a" )
		);
	}

	[Fact]
	public async Task NotificationWritesOneCompleteFrameWithoutFlush() {
		RecordingTerminalOutput output = new();

		await OscWriter.WriteOsc9NotificationAsync(
			output,
			"done"
		);

		Assert.Single( output.Writes );
		Assert.Equal(
			Encoding.ASCII.GetBytes( "\u001b]9;done\u001b\\" ),
			output.Writes[ 0 ]
		);
		Assert.Equal( 0, output.FlushCount );
		Assert.False( output.LastWriteCancellationToken.CanBeCanceled );
	}

	[Fact]
	public async Task WindowsCurrentDirectoryWritesOneCompleteFrameWithoutFlush() {
		RecordingTerminalOutput output = new();

		await OscWriter.WriteOsc9WindowsCurrentDirectoryAsync(
			output,
			"C:\\repo"
		);

		Assert.Single( output.Writes );
		Assert.Equal(
			Encoding.ASCII.GetBytes( "\u001b]9;9;C:\\repo\u001b\\" ),
			output.Writes[ 0 ]
		);
		Assert.Equal( 0, output.FlushCount );
		Assert.False( output.LastWriteCancellationToken.CanBeCanceled );
	}

	[Fact]
	public async Task PreCancelledNotificationEmitsNothing() {
		RecordingTerminalOutput output = new();
		using CancellationTokenSource cancellation = new();
		cancellation.Cancel();

		await Assert.ThrowsAnyAsync<OperationCanceledException>(
			() => OscWriter.WriteOsc9NotificationAsync(
				output,
				"done",
				cancellation.Token
			).AsTask()
		);

		Assert.Empty( output.Writes );
		Assert.Equal( 0, output.FlushCount );
	}

	[Fact]
	public async Task InvalidNotificationEmitsNothing() {
		RecordingTerminalOutput output = new();

		await Assert.ThrowsAsync<ArgumentException>(
			() => OscWriter.WriteOsc9NotificationAsync(
				output,
				"bad\u001btext"
			).AsTask()
		);

		Assert.Empty( output.Writes );
		Assert.Equal( 0, output.FlushCount );
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
