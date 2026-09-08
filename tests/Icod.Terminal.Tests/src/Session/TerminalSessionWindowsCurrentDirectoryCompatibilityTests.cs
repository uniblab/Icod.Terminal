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
namespace Icod.Terminal.Tests.Session;

using System.Text;
using Icod.Terminal;
using Icod.TermInfo;
using Xunit;

/// <summary>
/// Verifies T163 public OSC 9;9 Windows-current-directory compatibility integration.
/// </summary>
public sealed class TerminalSessionWindowsCurrentDirectoryCompatibilityTests {
	[Fact]
	public async Task EmitsExpectedCompatibilityFrameWithoutFlush() {
		RecordingTerminalOutput output = new();
		TerminalSession session = await OpenSessionAsync( output );

		await session.PublishWindowsCurrentDirectoryCompatibilityAsync(
			"C:\\work dir\\repo"
		);

		Assert.Single( output.Writes );
		Assert.Equal(
			Encoding.ASCII.GetBytes( "\u001b]9;9;C:\\work dir\\repo\u001b\\" ),
			output.Writes[ 0 ]
		);
		Assert.Single( output.WriteCancellationTokens );
		Assert.False( output.WriteCancellationTokens[ 0 ].CanBeCanceled );
		Assert.Equal( 0, output.FlushCount );

		await session.DisposeAsync();
		Assert.Equal( 1, output.FlushCount );
	}

	[Fact]
	public async Task PreservesPrintableUnicodePathText() {
		RecordingTerminalOutput output = new();
		await using TerminalSession session = await OpenSessionAsync( output );
		string path = "C:\\café & data\\😀";

		await session.PublishWindowsCurrentDirectoryCompatibilityAsync( path );

		Assert.Single( output.Writes );
		Assert.Equal(
			Encoding.UTF8.GetBytes( "\u001b]9;9;" + path + "\u001b\\" ),
			output.Writes[ 0 ]
		);
	}

	[Fact]
	public async Task DoesNotNormalizeOrTranslateCallerPath() {
		RecordingTerminalOutput output = new();
		await using TerminalSession session = await OpenSessionAsync( output );
		string path = "Z:/mixed\\segments/../literal";

		await session.PublishWindowsCurrentDirectoryCompatibilityAsync( path );

		Assert.Equal(
			Encoding.ASCII.GetBytes( "\u001b]9;9;" + path + "\u001b\\" ),
			Assert.Single( output.Writes )
		);
	}

	[Fact]
	public async Task NullAndEmptyPathsAreRejectedBeforeOutput() {
		RecordingTerminalOutput output = new();
		await using TerminalSession session = await OpenSessionAsync( output );

		await Assert.ThrowsAsync<ArgumentNullException>(
			() => session.PublishWindowsCurrentDirectoryCompatibilityAsync(
				null!
			).AsTask()
		);
		await Assert.ThrowsAsync<ArgumentException>(
			() => session.PublishWindowsCurrentDirectoryCompatibilityAsync(
				string.Empty
			).AsTask()
		);

		Assert.Empty( output.Writes );
	}

	[Theory]
	[InlineData( "C:\\bad\u0000path" )]
	[InlineData( "C:\\bad\u0007path" )]
	[InlineData( "C:\\bad\u001bpath" )]
	[InlineData( "C:\\bad\u007fpath" )]
	[InlineData( "C:\\bad\u009cpath" )]
	public async Task ControlCharactersAreRejectedBeforeOutput(
		string path
	) {
		RecordingTerminalOutput output = new();
		await using TerminalSession session = await OpenSessionAsync( output );

		await Assert.ThrowsAsync<ArgumentException>(
			() => session.PublishWindowsCurrentDirectoryCompatibilityAsync(
				path
			).AsTask()
		);

		Assert.Empty( output.Writes );
	}

	[Fact]
	public async Task MalformedUnicodeIsRejectedBeforeOutput() {
		RecordingTerminalOutput output = new();
		await using TerminalSession session = await OpenSessionAsync( output );
		string malformed = "C:\\bad\ud800path";

		await Assert.ThrowsAsync<ArgumentException>(
			() => session.PublishWindowsCurrentDirectoryCompatibilityAsync(
				malformed
			).AsTask()
		);

		Assert.Empty( output.Writes );
	}

	[Fact]
	public async Task ExactPayloadBoundaryIsAccepted() {
		RecordingTerminalOutput output = new();
		await using TerminalSession session = await OpenSessionAsync( output );
		string path = "C:\\" + new string(
			'a',
			TerminalOsc9SafeTextEncoder.MaximumWindowsCurrentDirectoryPayloadLength - 7
		);

		await session.PublishWindowsCurrentDirectoryCompatibilityAsync( path );

		Assert.Single( output.Writes );
		Assert.Equal(
			TerminalOsc9SafeTextEncoder.MaximumWindowsCurrentDirectoryPayloadLength + 4,
			output.Writes[ 0 ].Length
		);
	}

	[Fact]
	public async Task OversizePayloadIsRejectedBeforeOutput() {
		RecordingTerminalOutput output = new();
		await using TerminalSession session = await OpenSessionAsync( output );
		string path = "C:\\" + new string(
			'a',
			TerminalOsc9SafeTextEncoder.MaximumWindowsCurrentDirectoryPayloadLength - 6
		);

		await Assert.ThrowsAsync<ArgumentException>(
			() => session.PublishWindowsCurrentDirectoryCompatibilityAsync(
				path
			).AsTask()
		);

		Assert.Empty( output.Writes );
	}

	[Fact]
	public async Task InvalidCompatibilityPathFailsBeforeWaitingForOutputGate() {
		RecordingTerminalOutput output = new();
		await using TerminalSession session = await OpenSessionAsync( output );
		IDisposable controlOutput = await session.AcquireControlOutputAsync(
			CancellationToken.None
		);

		try {
			Assert.Throws<ArgumentException>(
				() => session.PublishWindowsCurrentDirectoryCompatibilityAsync(
					"C:\\bad\u001bpath"
				)
			);
			string oversize = "C:\\" + new string(
				'a',
				TerminalOsc9SafeTextEncoder.MaximumWindowsCurrentDirectoryPayloadLength - 6
			);
			Assert.Throws<ArgumentException>(
				() => session.PublishWindowsCurrentDirectoryCompatibilityAsync(
					oversize
				)
			);
			Assert.Empty( output.Writes );
		} finally {
			controlOutput.Dispose();
		}
	}

	[Fact]
	public async Task PreCancelledCompatibilityPublicationEmitsNothing() {
		RecordingTerminalOutput output = new();
		await using TerminalSession session = await OpenSessionAsync( output );
		using CancellationTokenSource cancellation = new();
		cancellation.Cancel();

		await Assert.ThrowsAnyAsync<OperationCanceledException>(
			() => session.PublishWindowsCurrentDirectoryCompatibilityAsync(
				"C:\\repo",
				cancellation.Token
			).AsTask()
		);

		Assert.Empty( output.Writes );
	}

	[Fact]
	public async Task CompatibilityPublicationWaitsBehindControlOutputLease() {
		RecordingTerminalOutput output = new();
		await using TerminalSession session = await OpenSessionAsync( output );
		IDisposable controlOutput = await session.AcquireControlOutputAsync(
			CancellationToken.None
		);

		Task publication = session.PublishWindowsCurrentDirectoryCompatibilityAsync(
			"C:\\repo"
		).AsTask();
		await Task.Delay( 50 );
		Assert.False( publication.IsCompleted );
		Assert.Empty( output.Writes );

		controlOutput.Dispose();
		await publication;

		Assert.Single( output.Writes );
	}

	[Fact]
	public async Task Osc7AndOsc99RemainExplicitIndependentOperations() {
		RecordingTerminalOutput output = new();
		await using TerminalSession session = await OpenSessionAsync( output );

		await session.PublishCurrentLocationAsync(
			"C:\\repo",
			TerminalLocationPathStyle.WindowsDrive
		);
		Assert.Single( output.Writes );
		Assert.Equal(
			OscWriter.EncodeLocationFrame(
				"C:\\repo",
				TerminalLocationPathKind.WindowsDrive
			),
			output.Writes[ 0 ]
		);

		await session.PublishWindowsCurrentDirectoryCompatibilityAsync(
			"C:\\repo"
		);
		Assert.Equal( 2, output.Writes.Count );
		Assert.Equal(
			Encoding.ASCII.GetBytes( "\u001b]9;9;C:\\repo\u001b\\" ),
			output.Writes[ 1 ]
		);
	}

	[Fact]
	public async Task NoninteractiveOutputIsRejected() {
		RecordingTerminalOutput output = new();
		TerminalSession session = await OpenSessionAsync(
			output,
			outputIsTerminal: false
		);

		try {
			await Assert.ThrowsAsync<InvalidOperationException>(
				() => session.PublishWindowsCurrentDirectoryCompatibilityAsync(
					"C:\\repo"
				).AsTask()
			);
			Assert.Empty( output.Writes );
		} finally {
			await session.DisposeAsync();
		}
	}

	private static ValueTask<TerminalSession> OpenSessionAsync(
		ITerminalOutput output,
		bool outputIsTerminal = true
	) {
		ArgumentNullException.ThrowIfNull( output );
		return TerminalSession.OpenAsync(
			new RecordingTerminalControlProvider( outputIsTerminal ),
			TerminalEndpoint.StandardInput,
			TerminalEndpoint.StandardOutput,
			new TestTerminalInput(),
			output,
			new TerminalSessionOptions {
				TerminalOverride = TerminalProfiles.Dumb,
				RequireInteractiveOutput = outputIsTerminal
			}
		);
	}

	private sealed class TestTerminalInput : ITerminalInput {
		public ValueTask<int> ReadAsync(
			Memory<byte> buffer,
			CancellationToken cancellationToken = default
		) {
			cancellationToken.ThrowIfCancellationRequested();
			return ValueTask.FromResult( 0 );
		}
	}

	private sealed class RecordingTerminalOutput : ITerminalOutput {
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

	private sealed class RecordingTerminalControlProvider : ITerminalControlProvider {
		private readonly TerminalModeSnapshot baseline = TerminalModeSnapshot.CreatePosix(
			0,
			0,
			0,
			0x0002UL,
			new byte[ 32 ],
			0,
			32,
			0,
			new TerminalSpeed( 13, 9600 ),
			new TerminalSpeed( 13, 9600 )
		);
		private readonly bool outputIsTerminal;

		internal RecordingTerminalControlProvider(
			bool outputIsTerminal
		) {
			this.outputIsTerminal = outputIsTerminal;
		}

		public TerminalControlResult<TerminalEndpointObservation> Observe(
			TerminalEndpoint endpoint
		) {
			ArgumentNullException.ThrowIfNull( endpoint );
			bool isTerminal = !ReferenceEquals(
				endpoint,
				TerminalEndpoint.StandardOutput
			) || this.outputIsTerminal;
			TerminalPlatformKind? platform = isTerminal
				? TerminalPlatformKind.PosixTermios
				: null;
			TerminalControlCapabilities capabilities = isTerminal
				? TerminalControlCapabilities.Attachment
					| TerminalControlCapabilities.ModeRead
					| TerminalControlCapabilities.ModeWrite
				: TerminalControlCapabilities.None;

			return TerminalControlResult<TerminalEndpointObservation>.Available(
				new TerminalEndpointObservation(
					isTerminal,
					null,
					platform,
					capabilities
				)
			);
		}

		public TerminalControlResult<TerminalSize> GetSize(
			TerminalEndpoint endpoint
		) {
			ArgumentNullException.ThrowIfNull( endpoint );
			return TerminalControlResult<TerminalSize>.Unsupported(
				"Size is not used by OSC 9;9 compatibility tests."
			);
		}

		public TerminalControlResult<TerminalModeSnapshot> GetMode(
			TerminalEndpoint endpoint
		) {
			ArgumentNullException.ThrowIfNull( endpoint );
			return TerminalControlResult<TerminalModeSnapshot>.Available(
				this.baseline
			);
		}

		public TerminalControlMutationResult SetMode(
			TerminalEndpoint endpoint,
			TerminalModeSnapshot mode,
			TerminalModeApplyTiming timing
		) {
			ArgumentNullException.ThrowIfNull( endpoint );
			ArgumentNullException.ThrowIfNull( mode );
			if ( !Enum.IsDefined( timing ) ) {
				throw new ArgumentOutOfRangeException( nameof( timing ) );
			}
			return TerminalControlMutationResult.Success();
		}
	}
}
