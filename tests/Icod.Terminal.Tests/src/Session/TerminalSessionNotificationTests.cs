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
/// Verifies T162 public OSC 9 notification integration.
/// </summary>
public sealed class TerminalSessionNotificationTests {
	[Fact]
	public async Task EmitsExpectedNotificationFrameWithoutFlush() {
		RecordingTerminalOutput output = new();
		TerminalSession session = await OpenSessionAsync( output );

		await session.SendNotificationAsync( "build complete" );

		Assert.Single( output.Writes );
		Assert.Equal(
			Encoding.ASCII.GetBytes( "\u001b]9;build complete\u001b\\" ),
			output.Writes[ 0 ]
		);
		Assert.Single( output.WriteCancellationTokens );
		Assert.False( output.WriteCancellationTokens[ 0 ].CanBeCanceled );
		Assert.Equal( 0, output.FlushCount );

		await session.DisposeAsync();
		Assert.Equal( 1, output.FlushCount );
	}

	[Fact]
	public async Task EmptyNotificationIsExplicitlyAllowed() {
		RecordingTerminalOutput output = new();
		await using TerminalSession session = await OpenSessionAsync( output );

		await session.SendNotificationAsync( string.Empty );

		Assert.Single( output.Writes );
		Assert.Equal(
			Encoding.ASCII.GetBytes( "\u001b]9;\u001b\\" ),
			output.Writes[ 0 ]
		);
	}

	[Fact]
	public async Task PreservesUnicodeTextAsStrictUtf8() {
		RecordingTerminalOutput output = new();
		await using TerminalSession session = await OpenSessionAsync( output );

		await session.SendNotificationAsync( "café 😀" );

		Assert.Single( output.Writes );
		Assert.Equal(
			Encoding.UTF8.GetBytes( "\u001b]9;café 😀\u001b\\" ),
			output.Writes[ 0 ]
		);
	}

	[Fact]
	public async Task NullMessageIsRejectedBeforeOutput() {
		RecordingTerminalOutput output = new();
		await using TerminalSession session = await OpenSessionAsync( output );

		await Assert.ThrowsAsync<ArgumentNullException>(
			() => session.SendNotificationAsync( null! ).AsTask()
		);

		Assert.Empty( output.Writes );
	}

	[Theory]
	[InlineData( "line\nfeed" )]
	[InlineData( "tab\tvalue" )]
	[InlineData( "bell\u0007value" )]
	[InlineData( "escape\u001bvalue" )]
	[InlineData( "delete\u007fvalue" )]
	[InlineData( "c1\u009bvalue" )]
	public async Task ControlCharactersAreRejectedBeforeOutput(
		string message
	) {
		RecordingTerminalOutput output = new();
		await using TerminalSession session = await OpenSessionAsync( output );

		await Assert.ThrowsAsync<ArgumentException>(
			() => session.SendNotificationAsync( message ).AsTask()
		);

		Assert.Empty( output.Writes );
	}

	[Fact]
	public async Task MalformedUnicodeIsRejectedBeforeOutput() {
		RecordingTerminalOutput output = new();
		await using TerminalSession session = await OpenSessionAsync( output );
		string malformed = new( [ '\ud800' ] );

		await Assert.ThrowsAsync<ArgumentException>(
			() => session.SendNotificationAsync( malformed ).AsTask()
		);

		Assert.Empty( output.Writes );
	}

	[Fact]
	public async Task ExactPayloadBoundaryIsAccepted() {
		RecordingTerminalOutput output = new();
		await using TerminalSession session = await OpenSessionAsync( output );
		string message = new(
			'a',
			TerminalOsc9SafeTextEncoder.MaximumNotificationPayloadLength - 2
		);

		await session.SendNotificationAsync( message );

		Assert.Single( output.Writes );
		Assert.Equal(
			TerminalOsc9SafeTextEncoder.MaximumNotificationPayloadLength + 4,
			output.Writes[ 0 ].Length
		);
	}

	[Fact]
	public async Task OversizePayloadIsRejectedBeforeOutput() {
		RecordingTerminalOutput output = new();
		await using TerminalSession session = await OpenSessionAsync( output );
		string message = new(
			'a',
			TerminalOsc9SafeTextEncoder.MaximumNotificationPayloadLength - 1
		);

		await Assert.ThrowsAsync<ArgumentException>(
			() => session.SendNotificationAsync( message ).AsTask()
		);

		Assert.Empty( output.Writes );
	}

	[Fact]
	public async Task InvalidNotificationFailsBeforeWaitingForOutputGate() {
		RecordingTerminalOutput output = new();
		await using TerminalSession session = await OpenSessionAsync( output );
		IDisposable controlOutput = await session.AcquireControlOutputAsync(
			CancellationToken.None
		);

		try {
			Assert.Throws<ArgumentException>(
				() => session.SendNotificationAsync( "bad\u001bmessage" )
			);
			string oversize = new(
				'a',
				TerminalOsc9SafeTextEncoder.MaximumNotificationPayloadLength - 1
			);
			Assert.Throws<ArgumentException>(
				() => session.SendNotificationAsync( oversize )
			);
			Assert.Empty( output.Writes );
		} finally {
			controlOutput.Dispose();
		}
	}

	[Fact]
	public async Task PreCancelledNotificationEmitsNothing() {
		RecordingTerminalOutput output = new();
		await using TerminalSession session = await OpenSessionAsync( output );
		using CancellationTokenSource cancellation = new();
		cancellation.Cancel();

		await Assert.ThrowsAnyAsync<OperationCanceledException>(
			() => session.SendNotificationAsync(
				"cancelled",
				cancellation.Token
			).AsTask()
		);

		Assert.Empty( output.Writes );
	}

	[Fact]
	public async Task NotificationWaitsBehindControlOutputLease() {
		RecordingTerminalOutput output = new();
		await using TerminalSession session = await OpenSessionAsync( output );
		IDisposable controlOutput = await session.AcquireControlOutputAsync(
			CancellationToken.None
		);

		Task notification = session.SendNotificationAsync( "queued" ).AsTask();
		await Task.Delay( 50 );
		Assert.False( notification.IsCompleted );
		Assert.Empty( output.Writes );

		controlOutput.Dispose();
		await notification;

		Assert.Single( output.Writes );
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
				() => session.SendNotificationAsync( "not a tty" ).AsTask()
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
				"Size is not used by notification tests."
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
