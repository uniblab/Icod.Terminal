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
/// Verifies bounded byte-exact iTerm2 OSC 1337 shell-integration framing.
/// </summary>
public sealed class Osc1337ShellIntegrationWriterTests {
	[Fact]
	public void SetMarkEncodesCanonicalFrame() {
		Assert.Equal(
			Encoding.ASCII.GetBytes( "\u001b]1337;SetMark\u001b\\" ),
			OscWriter.EncodeOsc1337SetMarkFrame()
		);
	}

	[Fact]
	public void CurrentDirectoryPreservesPrintableTextAndUtf8() {
		string currentDirectory = "/srv/café/a;b=c";
		Assert.Equal(
			Encoding.UTF8.GetBytes(
				"\u001b]1337;CurrentDir=/srv/café/a;b=c\u001b\\"
			),
			OscWriter.EncodeOsc1337CurrentDirectoryFrame( currentDirectory )
		);
	}

	[Fact]
	public void RemoteHostEncodesCanonicalFrame() {
		Assert.Equal(
			Encoding.ASCII.GetBytes(
				"\u001b]1337;RemoteHost=alice@host.example.test\u001b\\"
			),
			OscWriter.EncodeOsc1337RemoteHostFrame(
				"alice",
				"host.example.test"
			)
		);
	}

	[Fact]
	public void UserVariableUsesStrictUtf8ThenBase64() {
		Assert.Equal(
			Encoding.ASCII.GetBytes(
				"\u001b]1337;SetUserVar=branch=Y2Fmw6kg8J+YgA==\u001b\\"
			),
			OscWriter.EncodeOsc1337SetUserVariableFrame(
				"branch",
				"café 😀"
			)
		);
	}

	[Fact]
	public void ShellIntegrationVersionUsesCurrentNondeprecatedForm() {
		Assert.Equal(
			Encoding.ASCII.GetBytes(
				"\u001b]1337;ShellIntegrationVersion=20;shell=bash\u001b\\"
			),
			OscWriter.EncodeOsc1337ShellIntegrationVersionFrame(
				20,
				"bash"
			)
		);
	}

	[Fact]
	public void ClearCapturedOutputEncodesCanonicalFrame() {
		Assert.Equal(
			Encoding.ASCII.GetBytes(
				"\u001b]1337;ClearCapturedOutput\u001b\\"
			),
			OscWriter.EncodeOsc1337ClearCapturedOutputFrame()
		);
	}

	[Fact]
	public void CurrentDirectoryRejectsControlsAndMalformedUnicode() {
		Assert.Throws<ArgumentException>(
			() => OscWriter.EncodeOsc1337CurrentDirectoryFrame(
				"/tmp/bad\u001bpath"
			)
		);
		Assert.Throws<ArgumentException>(
			() => OscWriter.EncodeOsc1337CurrentDirectoryFrame(
				"/tmp/bad\ud800path"
			)
		);
	}

	[Theory]
	[InlineData( "bad@user", "host.example" )]
	[InlineData( "user", "bad;host" )]
	[InlineData( "user=name", "host.example" )]
	public void RemoteHostRejectsProtocolDelimiters(
		string userName,
		string hostName
	) {
		Assert.Throws<ArgumentException>(
			() => OscWriter.EncodeOsc1337RemoteHostFrame(
				userName,
				hostName
			)
		);
	}

	[Theory]
	[InlineData( "bad=name" )]
	[InlineData( "bad;name" )]
	[InlineData( "bad\u001bname" )]
	public void UserVariableRejectsUnsafeName(
		string name
	) {
		Assert.Throws<ArgumentException>(
			() => OscWriter.EncodeOsc1337SetUserVariableFrame(
				name,
				"value"
			)
		);
	}

	[Fact]
	public void UserVariableRejectsMalformedUnicodeValue() {
		Assert.Throws<ArgumentException>(
			() => OscWriter.EncodeOsc1337SetUserVariableFrame(
				"name",
				"bad\ud800value"
			)
		);
	}

	[Theory]
	[InlineData( -1, "bash" )]
	[InlineData( 1, "bad;shell" )]
	[InlineData( 1, "bad shell" )]
	public void ShellIntegrationVersionRejectsInvalidInput(
		int version,
		string shellName
	) {
		Assert.ThrowsAny<ArgumentException>(
			() => OscWriter.EncodeOsc1337ShellIntegrationVersionFrame(
				version,
				shellName
			)
		);
	}

	[Fact]
	public void CurrentDirectoryAcceptsExactPayloadLimit() {
		const int prefixLength = 16;
		string currentDirectory = new(
			'a',
			TerminalOsc1337ShellIntegrationEncoder.MaximumPayloadLength
				- prefixLength
		);
		byte[] frame = OscWriter.EncodeOsc1337CurrentDirectoryFrame(
			currentDirectory
		);
		Assert.Equal(
			TerminalOsc1337ShellIntegrationEncoder.MaximumPayloadLength + 4,
			frame.Length
		);
	}

	[Fact]
	public void CurrentDirectoryRejectsOneByteOverPayloadLimit() {
		const int prefixLength = 16;
		string currentDirectory = new(
			'a',
			TerminalOsc1337ShellIntegrationEncoder.MaximumPayloadLength
				- prefixLength
				+ 1
		);
		Assert.Throws<ArgumentException>(
			() => OscWriter.EncodeOsc1337CurrentDirectoryFrame(
				currentDirectory
			)
		);
	}

	[Fact]
	public async Task WriterCommitsOneCompleteFrameWithoutFlush() {
		RecordingTerminalOutput output = new();
		byte[] frame = OscWriter.EncodeOsc1337SetMarkFrame();

		await OscWriter.WriteOsc1337FrameAsync(
			output,
			frame
		);

		Assert.Single( output.Writes );
		Assert.Equal( frame, output.Writes[ 0 ] );
		Assert.Equal( 0, output.FlushCount );
		Assert.False( output.LastWriteCancellationToken.CanBeCanceled );
	}

	[Fact]
	public async Task PreCancelledWriterEmitsNothing() {
		RecordingTerminalOutput output = new();
		using CancellationTokenSource cancellation = new();
		cancellation.Cancel();

		await Assert.ThrowsAnyAsync<OperationCanceledException>(
			() => OscWriter.WriteOsc1337FrameAsync(
				output,
				OscWriter.EncodeOsc1337SetMarkFrame(),
				cancellation.Token
			).AsTask()
		);

		Assert.Empty( output.Writes );
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
