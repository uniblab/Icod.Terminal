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
namespace Icod.Terminal.Tests.Lifecycle;

using System.Text;
using System.Threading.Channels;
using Icod.Terminal;
using Icod.TermInfo;
using Xunit;

/// <summary>
/// Exercises repeated E198 wait and lifecycle stress against semantic events.
/// </summary>
public sealed class TerminalSemanticEventRepeatedStressTests {
	[Fact]
	public async Task RepeatedTimeoutsAndCancellationsPreserveFragmentedSemanticReport() {
		ScriptedTransport transport = new();
		await using TerminalSession session = await OpenSessionAsync( transport );

		transport.Publish(
			Encoding.ASCII.GetBytes( "\u001b]99;i=repeated-waits" )
		);
		Task<TerminalEvent> firstWait = session.ReadEventAsync(
			TimeSpan.FromMilliseconds( 100 )
		).AsTask();
		await transport.WaitForReadCountAsync( 2 );

		TerminalEvent firstTimeout = await firstWait.WaitAsync(
			TimeSpan.FromSeconds( 5 )
		);
		Assert.Equal( TerminalEventKind.Timeout, firstTimeout.Kind );

		for ( int index = 0; index < 2; ++index ) {
			TerminalEvent timedOut = await session.ReadEventAsync(
				TimeSpan.FromMilliseconds( 50 )
			);
			Assert.Equal( TerminalEventKind.Timeout, timedOut.Kind );
		}

		for ( int index = 0; index < 3; ++index ) {
			using CancellationTokenSource cancellation = new(
				TimeSpan.FromMilliseconds( 50 )
			);
			TerminalEvent cancelled = await session.ReadEventAsync(
				cancellation.Token
			);
			Assert.Equal( TerminalEventKind.Cancelled, cancelled.Kind );
		}

		transport.Publish(
			Encoding.ASCII.GetBytes( ";\u001b\\" )
		);
		TerminalEvent semantic = await session.ReadEventAsync(
			TimeSpan.FromSeconds( 5 )
		);
		AssertActivation( semantic, "repeated-waits" );
	}

	[Fact]
	public async Task RepeatedLifecycleCyclesDoNotReplayAndSemanticRoutingRecoversEachTime() {
		ScriptedTransport transport = new();
		TestTerminalLifecycleSource lifecycle = new() {
			AutoResume = true
		};
		await using TerminalSession session = await OpenSessionAsync(
			transport,
			lifecycle
		);

		await session.SendKittyNotificationAsync(
			"Build",
			"Complete",
			new KittyNotificationOptions {
				Identifier = "repeated-lifecycle",
				ReportActivation = true,
				ReportClose = true,
				Buttons = [ "Acknowledge" ]
			}
		);
		int notificationWriteCount = transport.WriteCount;
		Assert.True( 0 < notificationWriteCount );

		for ( int cycle = 0; cycle < 4; ++cycle ) {
			lifecycle.Publish( TerminalLifecycleSignalKind.Suspend );
			TerminalEvent suspendingEvent = await session.ReadEventAsync(
				TimeSpan.FromSeconds( 5 )
			);
			TerminalEvent resumedEvent = await session.ReadEventAsync(
				TimeSpan.FromSeconds( 5 )
			);
			Assert.Equal( TerminalEventKind.Lifecycle, suspendingEvent.Kind );
			Assert.Equal( TerminalEventKind.Lifecycle, resumedEvent.Kind );
			TerminalLifecycleEvent suspending = Assert.IsType<TerminalLifecycleEvent>(
				suspendingEvent.Lifecycle
			);
			TerminalLifecycleEvent resumed = Assert.IsType<TerminalLifecycleEvent>(
				resumedEvent.Lifecycle
			);

			Assert.Equal( TerminalLifecycleEventKind.Suspending, suspending.Kind );
			Assert.Equal( TerminalLifecycleEventKind.Resumed, resumed.Kind );
			Assert.Equal( cycle + 1, lifecycle.SuspendCount );
			Assert.Equal( notificationWriteCount, transport.WriteCount );

			string identifier = $"cycle-{cycle}";
			transport.Publish(
				Encoding.ASCII.GetBytes(
					$"\u001b]99;i={identifier};\u001b\\"
				)
			);
			TerminalEvent semantic = await session.ReadEventAsync(
				TimeSpan.FromSeconds( 5 )
			);
			AssertActivation( semantic, identifier );
		}

		Assert.Equal( notificationWriteCount, transport.WriteCount );
	}

	private static void AssertActivation(
		TerminalEvent terminalEvent,
		string identifier
	) {
		ArgumentNullException.ThrowIfNull( terminalEvent );
		ArgumentException.ThrowIfNullOrEmpty( identifier );

		Assert.Equal( TerminalEventKind.Semantic, terminalEvent.Kind );
		TerminalSemanticEvent semantic = Assert.IsType<TerminalSemanticEvent>(
			terminalEvent.Semantic
		);
		TerminalNotificationEvent notification = Assert.IsType<TerminalNotificationEvent>(
			semantic.Notification
		);
		Assert.Equal( TerminalNotificationEventKind.Activated, notification.Kind );
		Assert.Equal( identifier, notification.Identifier );
		Assert.Null( notification.ButtonNumber );
	}

	private static ValueTask<TerminalSession> OpenSessionAsync(
		ScriptedTransport transport,
		TestTerminalLifecycleSource? lifecycle = null
	) {
		ArgumentNullException.ThrowIfNull( transport );

		return TerminalSession.OpenAsync(
			new RecordingTerminalControlProvider(),
			TerminalEndpoint.StandardInput,
			TerminalEndpoint.StandardOutput,
			transport,
			transport,
			new TerminalSessionOptions {
				TerminalOverride = TerminalProfiles.Dumb,
				ConfigureOutput = false,
				ObserveLifecycleEvents = false,
				LifecycleSource = lifecycle
			}
		);
	}

	private sealed class ScriptedTransport : ITerminalInput, ITerminalOutput {
		private readonly object sync = new();
		private readonly Channel<byte[]> input = Channel.CreateUnbounded<byte[]>(
			new UnboundedChannelOptions {
				SingleReader = true,
				SingleWriter = false,
				AllowSynchronousContinuations = false
			}
		);
		private readonly List<byte[]> writes = [];
		private readonly SemaphoreSlim readSignal = new( 0 );
		private int readCount;

		internal int WriteCount {
			get {
				lock ( this.sync ) {
					return this.writes.Count;
				}
			}
		}

		internal void Publish(
			byte[] bytes
		) {
			ArgumentNullException.ThrowIfNull( bytes );
			if ( !this.input.Writer.TryWrite( bytes.ToArray() ) ) {
				throw new InvalidOperationException(
					"The scripted terminal input channel is closed."
				);
			}
		}

		internal async ValueTask WaitForReadCountAsync(
			int expected
		) {
			if ( 0 > expected ) {
				throw new ArgumentOutOfRangeException( nameof( expected ) );
			}

			using CancellationTokenSource timeout = new( TimeSpan.FromSeconds( 5 ) );
			while ( expected > Volatile.Read( ref this.readCount ) ) {
				await this.readSignal.WaitAsync(
					timeout.Token
				).ConfigureAwait( false );
			}
		}

		public async ValueTask<int> ReadAsync(
			Memory<byte> buffer,
			CancellationToken cancellationToken = default
		) {
			if ( buffer.IsEmpty ) {
				return 0;
			}

			Interlocked.Increment( ref this.readCount );
			this.readSignal.Release();
			while ( await this.input.Reader.WaitToReadAsync(
				cancellationToken
			).ConfigureAwait( false ) ) {
				if ( !this.input.Reader.TryRead( out byte[]? bytes ) ) {
					continue;
				}
				if ( bytes.Length > buffer.Length ) {
					throw new InvalidOperationException(
						"The scripted input chunk exceeds the decoder read buffer."
					);
				}

				bytes.AsSpan().CopyTo( buffer.Span );
				return bytes.Length;
			}

			return 0;
		}

		public ValueTask WriteAsync(
			ReadOnlyMemory<byte> buffer,
			CancellationToken cancellationToken = default
		) {
			cancellationToken.ThrowIfCancellationRequested();
			lock ( this.sync ) {
				this.writes.Add( buffer.ToArray() );
			}
			return ValueTask.CompletedTask;
		}

		public ValueTask FlushAsync(
			CancellationToken cancellationToken = default
		) {
			cancellationToken.ThrowIfCancellationRequested();
			return ValueTask.CompletedTask;
		}
	}

	private sealed class TestTerminalLifecycleSource
		: ITerminalLifecycleSource,
		  ITerminalSuspendController {
		private readonly Channel<TerminalLifecycleSignal> signals =
			Channel.CreateUnbounded<TerminalLifecycleSignal>();

		internal bool AutoResume {
			get;
			init;
		}

		internal int SuspendCount {
			get;
			private set;
		}

		internal void Publish(
			TerminalLifecycleSignalKind kind
		) {
			if ( !this.signals.Writer.TryWrite( new TerminalLifecycleSignal( kind ) ) ) {
				throw new InvalidOperationException(
					"The scripted lifecycle source is closed."
				);
			}
		}

		public ValueTask<TerminalLifecycleSignal> ReadAsync(
			CancellationToken cancellationToken = default
		) {
			return this.signals.Reader.ReadAsync( cancellationToken );
		}

		public TerminalControlMutationResult SuspendCurrentProcess() {
			++this.SuspendCount;
			if ( this.AutoResume ) {
				this.Publish( TerminalLifecycleSignalKind.Resume );
			}

			return TerminalControlMutationResult.Success();
		}

		public void Dispose() {
			this.signals.Writer.TryComplete();
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

		public TerminalControlResult<TerminalEndpointObservation> Observe(
			TerminalEndpoint endpoint
		) {
			ArgumentNullException.ThrowIfNull( endpoint );
			return TerminalControlResult<TerminalEndpointObservation>.Available(
				new TerminalEndpointObservation(
					true,
					null,
					TerminalPlatformKind.PosixTermios,
					TerminalControlCapabilities.Attachment
						| TerminalControlCapabilities.ModeRead
						| TerminalControlCapabilities.ModeWrite
				)
			);
		}

		public TerminalControlResult<TerminalSize> GetSize(
			TerminalEndpoint endpoint
		) {
			ArgumentNullException.ThrowIfNull( endpoint );
			return TerminalControlResult<TerminalSize>.Unsupported(
				"Size is not required by this test."
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
