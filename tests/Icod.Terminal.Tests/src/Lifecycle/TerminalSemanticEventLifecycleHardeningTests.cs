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
/// Defines E197 lifecycle, cancellation, disposal, late-response, and end-of-input
/// behavior for unsolicited semantic terminal events.
/// </summary>
public sealed class TerminalSemanticEventLifecycleHardeningTests {
	[Fact]
	public async Task CallerCancellationPreservesFragmentedSemanticReport() {
		ScriptedTransport transport = new();
		await using TerminalSession session = await OpenSessionAsync( transport );
		using CancellationTokenSource cancellation = new();

		Task<TerminalEvent> pending = session.ReadEventAsync(
			cancellation.Token
		).AsTask();
		transport.Publish(
			Encoding.ASCII.GetBytes( "\u001b]99;i=fragmented" )
		);
		await transport.WaitForReadCountAsync( 2 );

		cancellation.Cancel();
		TerminalEvent cancelled = await pending.WaitAsync(
			TimeSpan.FromSeconds( 5 )
		);
		Assert.Equal( TerminalEventKind.Cancelled, cancelled.Kind );

		transport.Publish(
			Encoding.ASCII.GetBytes( ";\u001b\\" )
		);
		TerminalEvent semantic = await session.ReadEventAsync(
			TimeSpan.FromSeconds( 5 )
		);
		AssertActivation( semantic, "fragmented" );
	}

	[Fact]
	public async Task QueuedSemanticEventSurvivesSuspendResumeWithoutReplay() {
		ScriptedTransport transport = new();
		TestTerminalLifecycleSource lifecycle = new() {
			AutoResume = true
		};
		await using TerminalSession session = await OpenSessionAsync(
			transport,
			lifecycle
		);
		using CancellationTokenSource timeout = new( TimeSpan.FromSeconds( 5 ) );

		Task<KittyNotificationSupport> query = session.QueryKittyNotificationSupportAsync(
			TimeSpan.FromSeconds( 30 )
		).AsTask();
		await transport.WaitForWriteCountAsync( 1 );
		transport.Publish(
			Encoding.ASCII.GetBytes( "\u001b]99;i=queued;\u001b\\" )
		);
		await transport.WaitForReadCountAsync( 2 );

		lifecycle.Publish( TerminalLifecycleSignalKind.Suspend );
		TerminalLifecycleEvent suspending = await session.ReadLifecycleEventAsync(
			timeout.Token
		);
		TerminalLifecycleEvent resumed = await session.ReadLifecycleEventAsync(
			timeout.Token
		);
		Assert.Equal( TerminalLifecycleEventKind.Suspending, suspending.Kind );
		Assert.Equal( TerminalLifecycleEventKind.Resumed, resumed.Kind );
		await Assert.ThrowsAsync<InvalidOperationException>(
			() => query.WaitAsync( TimeSpan.FromSeconds( 5 ) )
		);

		TerminalEvent semantic = await session.ReadEventAsync(
			TimeSpan.FromSeconds( 5 )
		);
		AssertActivation( semantic, "queued" );
		TerminalEvent trailing = await session.ReadEventAsync( TimeSpan.Zero );
		Assert.Equal( TerminalEventKind.Timeout, trailing.Kind );
	}

	[Fact]
	public async Task SuspendResumeAndDisposalDoNotReplayOrAutoCloseNotification() {
		ScriptedTransport transport = new();
		TestTerminalLifecycleSource lifecycle = new() {
			AutoResume = true
		};
		TerminalSession session = await OpenSessionAsync(
			transport,
			lifecycle
		);
		using CancellationTokenSource timeout = new( TimeSpan.FromSeconds( 5 ) );

		try {
			await session.SendKittyNotificationAsync(
				"Build",
				"Complete",
				new KittyNotificationOptions {
					Identifier = "no-replay",
					ReportActivation = true,
					ReportClose = true,
					Buttons = [ "Acknowledge" ]
				}
			);
			IReadOnlyList<byte[]> beforeLifecycle = transport.GetWrites();
			Assert.NotEmpty( beforeLifecycle );

			lifecycle.Publish( TerminalLifecycleSignalKind.Suspend );
			TerminalLifecycleEvent suspending = await session.ReadLifecycleEventAsync(
				timeout.Token
			);
			TerminalLifecycleEvent resumed = await session.ReadLifecycleEventAsync(
				timeout.Token
			);
			Assert.Equal( TerminalLifecycleEventKind.Suspending, suspending.Kind );
			Assert.Equal( TerminalLifecycleEventKind.Resumed, resumed.Kind );
			AssertWritesEqual( beforeLifecycle, transport.GetWrites() );

			await session.DisposeAsync();
			AssertWritesEqual( beforeLifecycle, transport.GetWrites() );
		} finally {
			await session.DisposeAsync();
		}
	}

	[Fact]
	public async Task DisposalUnblocksPendingSemanticEventWait() {
		ScriptedTransport transport = new();
		TerminalSession session = await OpenSessionAsync( transport );

		Task<TerminalEvent> pending = session.ReadEventAsync().AsTask();
		await transport.WaitForReadCountAsync( 1 );
		await session.DisposeAsync();

		await Assert.ThrowsAnyAsync<OperationCanceledException>(
			() => pending.WaitAsync( TimeSpan.FromSeconds( 5 ) )
		);
	}

	[Fact]
	public async Task SemanticEventPrecedesEndOfInputWithoutFabricatingLifecycleEvent() {
		ScriptedTransport transport = new();
		await using TerminalSession session = await OpenSessionAsync( transport );

		transport.Publish(
			Encoding.ASCII.GetBytes( "\u001b]99;i=before-eof;\u001b\\" )
		);
		transport.CompleteInput();

		TerminalEvent semantic = await session.ReadEventAsync(
			TimeSpan.FromSeconds( 5 )
		);
		AssertActivation( semantic, "before-eof" );

		TerminalEvent endOfInput = await session.ReadEventAsync(
			TimeSpan.FromSeconds( 5 )
		);
		Assert.Equal( TerminalEventKind.Input, endOfInput.Kind );
		TerminalInputEvent input = Assert.IsType<TerminalInputEvent>(
			endOfInput.Input
		);
		Assert.Equal( TerminalInputEventKind.EndOfInput, input.Kind );
		Assert.Null( endOfInput.Semantic );
	}

	[Fact]
	public async Task LateTimedOutQueryResponseDoesNotLeakIntoSemanticLane() {
		ScriptedTransport transport = new();
		await using TerminalSession session = await OpenSessionAsync( transport );

		Task<KittyNotificationSupport> query = session.QueryKittyNotificationSupportAsync(
			TimeSpan.FromMilliseconds( 100 )
		).AsTask();
		await transport.WaitForWriteCountAsync( 1 );
		string identifier = GetSupportQueryIdentifier( transport.GetWrite( 0 ) );
		await Assert.ThrowsAsync<TimeoutException>(
			() => query.WaitAsync( TimeSpan.FromSeconds( 5 ) )
		);

		transport.Publish(
			Encoding.ASCII.GetBytes(
				$"\u001b]99;i={identifier}:p=?;p=title,body\u001b\\"
			)
		);
		transport.Publish(
			Encoding.ASCII.GetBytes( "\u001b]99;i=after-late;\u001b\\" )
		);

		TerminalEvent semantic = await session.ReadEventAsync(
			TimeSpan.FromSeconds( 5 )
		);
		AssertActivation( semantic, "after-late" );
		TerminalEvent trailing = await session.ReadEventAsync( TimeSpan.Zero );
		Assert.Equal( TerminalEventKind.Timeout, trailing.Kind );
	}

	private static void AssertActivation(
		TerminalEvent terminalEvent,
		string identifier
	) {
		ArgumentNullException.ThrowIfNull( terminalEvent );
		ArgumentException.ThrowIfNullOrEmpty( identifier );

		Assert.Equal( TerminalEventKind.Semantic, terminalEvent.Kind );
		Assert.Null( terminalEvent.Input );
		TerminalSemanticEvent semantic = Assert.IsType<TerminalSemanticEvent>(
			terminalEvent.Semantic
		);
		Assert.Equal( TerminalSemanticEventKind.Notification, semantic.Kind );
		TerminalNotificationEvent notification = Assert.IsType<TerminalNotificationEvent>(
			semantic.Notification
		);
		Assert.Equal( TerminalNotificationEventKind.Activated, notification.Kind );
		Assert.Equal( identifier, notification.Identifier );
		Assert.Null( notification.ButtonNumber );
	}

	private static void AssertWritesEqual(
		IReadOnlyList<byte[]> expected,
		IReadOnlyList<byte[]> actual
	) {
		ArgumentNullException.ThrowIfNull( expected );
		ArgumentNullException.ThrowIfNull( actual );

		Assert.Equal( expected.Count, actual.Count );
		for ( int index = 0; index < expected.Count; ++index ) {
			Assert.Equal( expected[ index ], actual[ index ] );
		}
	}

	private static string GetSupportQueryIdentifier(
		byte[] requestBytes
	) {
		ArgumentNullException.ThrowIfNull( requestBytes );
		string request = Encoding.ASCII.GetString( requestBytes );
		const string prefix = "\u001b]99;i=";
		const string suffix = ":p=?;\u001b\\";
		Assert.StartsWith( prefix, request, StringComparison.Ordinal );
		Assert.EndsWith( suffix, request, StringComparison.Ordinal );
		return request.Substring(
			prefix.Length,
			request.Length - prefix.Length - suffix.Length
		);
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
		private readonly SemaphoreSlim writeSignal = new( 0 );
		private readonly SemaphoreSlim readSignal = new( 0 );
		private int readCount;

		internal int WriteCount {
			get {
				lock ( this.sync ) {
					return this.writes.Count;
				}
			}
		}

		internal byte[] GetWrite(
			int index
		) {
			lock ( this.sync ) {
				return this.writes[ index ].ToArray();
			}
		}

		internal IReadOnlyList<byte[]> GetWrites() {
			lock ( this.sync ) {
				return this.writes
					.Select( static bytes => bytes.ToArray() )
					.ToArray();
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

		internal void CompleteInput() {
			this.input.Writer.TryComplete();
		}

		internal async ValueTask WaitForReadCountAsync(
			int expected
		) {
			if ( 0 > expected ) {
				throw new ArgumentOutOfRangeException( nameof( expected ) );
			}

			using CancellationTokenSource timeout = new();
			timeout.CancelAfter( TimeSpan.FromSeconds( 5 ) );
			while ( expected > Volatile.Read( ref this.readCount ) ) {
				await this.readSignal.WaitAsync(
					timeout.Token
				).ConfigureAwait( false );
			}
		}

		internal async ValueTask WaitForWriteCountAsync(
			int expected
		) {
			if ( 0 > expected ) {
				throw new ArgumentOutOfRangeException( nameof( expected ) );
			}

			using CancellationTokenSource timeout = new();
			timeout.CancelAfter( TimeSpan.FromSeconds( 5 ) );
			while ( true ) {
				lock ( this.sync ) {
					if ( expected <= this.writes.Count ) {
						return;
					}
				}

				await this.writeSignal.WaitAsync(
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
			this.writeSignal.Release();
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

		internal void Publish(
			TerminalLifecycleSignalKind kind
		) {
			if ( !this.signals.Writer.TryWrite( new TerminalLifecycleSignal( kind ) ) ) {
				throw new InvalidOperationException(
					"The scripted lifecycle channel is closed."
				);
			}
		}

		public ValueTask<TerminalLifecycleSignal> ReadAsync(
			CancellationToken cancellationToken = default
		) {
			return this.signals.Reader.ReadAsync( cancellationToken );
		}

		public TerminalControlMutationResult SuspendCurrentProcess() {
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
