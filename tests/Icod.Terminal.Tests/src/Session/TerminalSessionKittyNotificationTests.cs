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
/// Verifies the public Icod.Terminal 1.4 Kitty OSC 99 notification-output contract.
/// </summary>
public sealed class TerminalSessionKittyNotificationTests {
	[Fact]
	public async Task EmitsExpectedKittyNotificationWithoutFlush() {
		RecordingTerminalOutput output = new();
		await using TerminalSession session = await OpenSessionAsync( output );

		await session.SendKittyNotificationAsync(
			"Build",
			string.Empty
		);

		Assert.Single( output.Writes );
		Assert.Equal(
			Encoding.ASCII.GetBytes(
				"\u001b]99;p=title:e=1:d=1;QnVpbGQ=\u001b\\"
			),
			output.Writes[ 0 ]
		);
		Assert.Single( output.WriteCancellationTokens );
		Assert.False( output.WriteCancellationTokens[ 0 ].CanBeCanceled );
		Assert.Equal( 0, output.FlushCount );
	}

	[Fact]
	public async Task InvalidRequestFailsBeforeWaitingForOutputGate() {
		RecordingTerminalOutput output = new();
		await using TerminalSession session = await OpenSessionAsync( output );
		IDisposable controlOutput = await session.AcquireControlOutputAsync(
			CancellationToken.None
		);

		try {
			Assert.Throws<ArgumentException>(
				() => session.SendKittyNotificationAsync(
					string.Empty,
					string.Empty
				)
			);
			Assert.Throws<ArgumentException>(
				() => session.SendKittyNotificationAsync(
					"title",
					string.Empty,
					new KittyNotificationOptions {
						Identifier = "bad:id"
					}
				)
			);
			Assert.Empty( output.Writes );
		} finally {
			controlOutput.Dispose();
		}
	}

	[Fact]
	public async Task NotificationWaitsBehindSharedOutputGate() {
		RecordingTerminalOutput output = new();
		await using TerminalSession session = await OpenSessionAsync( output );
		IDisposable controlOutput = await session.AcquireControlOutputAsync(
			CancellationToken.None
		);

		Task notification = session.SendKittyNotificationAsync(
			"Build",
			"Queued",
			new KittyNotificationOptions {
				Identifier = "build.42"
			}
		).AsTask();
		await Task.Delay( 50 );
		Assert.False( notification.IsCompleted );
		Assert.Empty( output.Writes );

		controlOutput.Dispose();
		await notification;

		Assert.Equal( 2, output.Writes.Count );
	}

	[Fact]
	public async Task LegacyTitledAndKittyNotificationsRemainIndependent() {
		RecordingTerminalOutput output = new();
		await using TerminalSession session = await OpenSessionAsync( output );

		await session.SendNotificationAsync( "legacy" );
		await session.SendTitledNotificationAsync(
			"Title",
			"Body"
		);
		await session.SendKittyNotificationAsync(
			"Kitty",
			string.Empty
		);

		Assert.Equal( 3, output.Writes.Count );
		Assert.Equal(
			Encoding.ASCII.GetBytes( "\u001b]9;legacy\u001b\\" ),
			output.Writes[ 0 ]
		);
		Assert.Equal(
			Encoding.ASCII.GetBytes(
				"\u001b]777;notify;Title;Body\u001b\\"
			),
			output.Writes[ 1 ]
		);
		Assert.Equal(
			Encoding.ASCII.GetBytes(
				"\u001b]99;p=title:e=1:d=1;S2l0dHk=\u001b\\"
			),
			output.Writes[ 2 ]
		);
	}

	[Fact]
	public async Task CloseRequestUsesSharedNotificationOutputPath() {
		RecordingTerminalOutput output = new();
		await using TerminalSession session = await OpenSessionAsync( output );

		await session.CloseKittyNotificationAsync( "build.42" );

		Assert.Single( output.Writes );
		Assert.Equal(
			Encoding.ASCII.GetBytes(
				"\u001b]99;i=build.42:p=close;\u001b\\"
			),
			output.Writes[ 0 ]
		);
	}

	[Fact]
	public async Task PreCancelledRequestEmitsNothing() {
		RecordingTerminalOutput output = new();
		await using TerminalSession session = await OpenSessionAsync( output );
		using CancellationTokenSource cancellation = new();
		cancellation.Cancel();

		await Assert.ThrowsAnyAsync<OperationCanceledException>(
			() => session.SendKittyNotificationAsync(
				"title",
				string.Empty,
				null,
				cancellation.Token
			).AsTask()
		);
		Assert.Empty( output.Writes );
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
				() => session.SendKittyNotificationAsync(
					"Title",
					"Body"
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
				"Size is not used by Kitty OSC 99 notification tests."
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
