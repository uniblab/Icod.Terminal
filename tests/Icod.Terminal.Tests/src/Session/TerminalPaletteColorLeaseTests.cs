namespace Icod.Terminal.Tests.Session;

using System.Text;
using System.Threading.Channels;
using Icod.Terminal;
using Icod.TermInfo;
using Xunit;

/// <summary>
/// Verifies T142 lifecycle-safe indexed-palette ownership and exact restoration.
/// </summary>
public sealed class TerminalPaletteColorLeaseTests {
	[Fact]
	public async Task FirstOwnerQueriesBeforeMutationAndFinalReleaseReplaysExactBaseline() {
		RecordingTransport transport = new();
		await using TerminalSession session = await OpenSessionAsync( transport );

		Task<TerminalPaletteColorLease> acquisition = session.AcquirePaletteColorAsync(
			7,
			new TerminalColor( 0xaaaa, 0xbbbb, 0xcccc ),
			TimeSpan.FromSeconds( 5 )
		).AsTask();
		await WaitForWriteCountAsync( transport, 1 );

		Assert.Equal(
			Encoding.ASCII.GetBytes( "\u001b]4;7;?\u001b\\" ),
			transport.GetWrite( 0 )
		);
		transport.Publish(
			Encoding.ASCII.GetBytes( "\u001b]4;7;rgb:1234/5678/9abc\u001b\\" )
		);

		TerminalPaletteColorLease lease = await acquisition;
		await WaitForWriteCountAsync( transport, 2 );
		Assert.Equal(
			Encoding.ASCII.GetBytes( "\u001b]4;7;rgb:aaaa/bbbb/cccc\u001b\\" ),
			transport.GetWrite( 1 )
		);

		await lease.DisposeAsync();
		await WaitForWriteCountAsync( transport, 3 );
		Assert.Equal(
			Encoding.ASCII.GetBytes( "\u001b]4;7;rgb:1234/5678/9abc\u001b\\" ),
			transport.GetWrite( 2 )
		);
		Assert.DoesNotContain(
			transport.GetWrites(),
			static bytes => Encoding.ASCII.GetString( bytes ).Contains( "]104", StringComparison.Ordinal )
		);
	}

	[Fact]
	public async Task SameIndexOwnersMayReleaseOutOfOrderWithoutOverRestoring() {
		RecordingTransport transport = new();
		await using TerminalSession session = await OpenSessionAsync( transport );

		TerminalPaletteColorLease outer = await AcquireAsync(
			session,
			transport,
			3,
			new TerminalColor( 0x1111, 0x2222, 0x3333 ),
			new TerminalColor( 0x0101, 0x0202, 0x0303 )
		);
		TerminalPaletteColorLease inner = await session.AcquirePaletteColorAsync(
			3,
			new TerminalColor( 0x4444, 0x5555, 0x6666 ),
			TimeSpan.FromSeconds( 5 )
		);
		await WaitForWriteCountAsync( transport, 3 );

		Assert.Equal(
			Encoding.ASCII.GetBytes( "\u001b]4;3;rgb:4444/5555/6666\u001b\\" ),
			transport.GetWrite( 2 )
		);

		await outer.DisposeAsync();
		Assert.Equal( 3, transport.WriteCount );

		await inner.DisposeAsync();
		await WaitForWriteCountAsync( transport, 4 );
		Assert.Equal(
			Encoding.ASCII.GetBytes( "\u001b]4;3;rgb:0101/0202/0303\u001b\\" ),
			transport.GetWrite( 3 )
		);
	}

	[Fact]
	public async Task UnscopedMutationIsRejectedWhileScopedPaletteOwnershipExists() {
		RecordingTransport transport = new();
		await using TerminalSession session = await OpenSessionAsync( transport );
		TerminalPaletteColorLease lease = await AcquireAsync(
			session,
			transport,
			2,
			new TerminalColor( 0xaaaa, 0xbbbb, 0xcccc ),
			new TerminalColor( 0x1111, 0x2222, 0x3333 )
		);
		int priorWrites = transport.WriteCount;

		await Assert.ThrowsAsync<InvalidOperationException>(
			() => session.SetPaletteColorAsync(
				9,
				new TerminalColor( 0x7777, 0x8888, 0x9999 )
			).AsTask()
		);
		Assert.Equal( priorWrites, transport.WriteCount );

		await lease.DisposeAsync();
	}

	[Fact]
	public async Task SuspendRestoresOldBaselineResumeObservesNewBaselineAndFinalReleaseUsesNewBaseline() {
		RecordingTransport transport = new();
		TestLifecycleSource lifecycle = new() {
			AutoResume = true
		};
		await using TerminalSession session = await OpenSessionAsync(
			transport,
			lifecycle
		);
		TerminalPaletteColorLease lease = await AcquireAsync(
			session,
			transport,
			5,
			new TerminalColor( 0xaaaa, 0xbbbb, 0xcccc ),
			new TerminalColor( 0x1111, 0x2222, 0x3333 )
		);
		using CancellationTokenSource timeout = new( TimeSpan.FromSeconds( 5 ) );

		lifecycle.Publish( TerminalLifecycleSignalKind.Suspend );
		TerminalLifecycleEvent suspending = await session.ReadLifecycleEventAsync( timeout.Token );
		Assert.Equal( TerminalLifecycleEventKind.Suspending, suspending.Kind );
		await WaitForWriteCountAsync( transport, 3 );
		Assert.Equal(
			Encoding.ASCII.GetBytes( "\u001b]4;5;rgb:1111/2222/3333\u001b\\" ),
			transport.GetWrite( 2 )
		);

		await WaitForWriteCountAsync( transport, 4 );
		Assert.Equal(
			Encoding.ASCII.GetBytes( "\u001b]4;5;?\u001b\\" ),
			transport.GetWrite( 3 )
		);
		transport.Publish(
			Encoding.ASCII.GetBytes( "\u001b]4;5;rgb:dead/beef/1234\u001b\\" )
		);

		TerminalLifecycleEvent resumed = await session.ReadLifecycleEventAsync( timeout.Token );
		Assert.Equal( TerminalLifecycleEventKind.Resumed, resumed.Kind );
		await WaitForWriteCountAsync( transport, 5 );
		Assert.Equal(
			Encoding.ASCII.GetBytes( "\u001b]4;5;rgb:aaaa/bbbb/cccc\u001b\\" ),
			transport.GetWrite( 4 )
		);

		await lease.DisposeAsync();
		await WaitForWriteCountAsync( transport, 6 );
		Assert.Equal(
			Encoding.ASCII.GetBytes( "\u001b]4;5;rgb:dead/beef/1234\u001b\\" ),
			transport.GetWrite( 5 )
		);
	}

	[Fact]
	public async Task InvalidationReestablishesOwnedColorBeforeFinalBaselineRestoration() {
		RecordingTransport transport = new();
		await using TerminalSession session = await OpenSessionAsync( transport );
		TerminalPaletteColorLease lease = await AcquireAsync(
			session,
			transport,
			8,
			new TerminalColor( 0xaaaa, 0xbbbb, 0xcccc ),
			new TerminalColor( 0x0101, 0x0202, 0x0303 )
		);

		session.InvalidateState();
		await lease.DisposeAsync();
		await WaitForWriteCountAsync( transport, 4 );
		Assert.Equal(
			Encoding.ASCII.GetBytes( "\u001b]4;8;rgb:aaaa/bbbb/cccc\u001b\\" ),
			transport.GetWrite( 2 )
		);
		Assert.Equal(
			Encoding.ASCII.GetBytes( "\u001b]4;8;rgb:0101/0202/0303\u001b\\" ),
			transport.GetWrite( 3 )
		);
	}

	[Fact]
	public async Task SessionDisposalRestoresBaselineAndLateLeaseDisposalIsNoOp() {
		RecordingTransport transport = new();
		TerminalSession session = await OpenSessionAsync( transport );
		TerminalPaletteColorLease lease = await AcquireAsync(
			session,
			transport,
			9,
			new TerminalColor( 0xaaaa, 0xbbbb, 0xcccc ),
			new TerminalColor( 0x9999, 0x8888, 0x7777 )
		);

		await session.DisposeAsync();
		await WaitForWriteCountAsync( transport, 3 );
		Assert.Equal(
			Encoding.ASCII.GetBytes( "\u001b]4;9;rgb:9999/8888/7777\u001b\\" ),
			transport.GetWrite( 2 )
		);
		int priorWrites = transport.WriteCount;

		await lease.DisposeAsync();
		Assert.Equal( priorWrites, transport.WriteCount );
	}

	private static async ValueTask<TerminalPaletteColorLease> AcquireAsync(
		TerminalSession session,
		RecordingTransport transport,
		byte index,
		TerminalColor requested,
		TerminalColor baseline
	) {
		ArgumentNullException.ThrowIfNull( session );
		ArgumentNullException.ThrowIfNull( transport );

		int initialWrites = transport.WriteCount;
		Task<TerminalPaletteColorLease> acquisition = session.AcquirePaletteColorAsync(
			index,
			requested,
			TimeSpan.FromSeconds( 5 )
		).AsTask();
		await WaitForWriteCountAsync( transport, initialWrites + 1 );
		transport.Publish(
			Encoding.ASCII.GetBytes(
				$"\u001b]4;{index};rgb:{baseline.Red:x4}/{baseline.Green:x4}/{baseline.Blue:x4}\u001b\\"
			)
		);
		TerminalPaletteColorLease lease = await acquisition;
		await WaitForWriteCountAsync( transport, initialWrites + 2 );
		return lease;
	}

	private static ValueTask<TerminalSession> OpenSessionAsync(
		RecordingTransport transport,
		TestLifecycleSource? lifecycle = null
	) {
		ArgumentNullException.ThrowIfNull( transport );
		return TerminalSession.OpenAsync(
			new TestTerminalControlProvider(),
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

	private static async Task WaitForWriteCountAsync(
		RecordingTransport transport,
		int count
	) {
		ArgumentNullException.ThrowIfNull( transport );
		for ( int attempt = 0; attempt < 500; ++attempt ) {
			if ( count <= transport.WriteCount ) {
				return;
			}
			await Task.Delay( 10 );
		}
		throw new TimeoutException( "The expected terminal output was not observed." );
	}

	private sealed class RecordingTransport : ITerminalInput, ITerminalOutput {
		private readonly Channel<byte[]> input = Channel.CreateUnbounded<byte[]>();
		private readonly List<byte[]> writes = [];
		private byte[]? pending;
		private int pendingOffset;
		private int flushCount;

		internal int WriteCount {
			get {
				lock ( this.writes ) {
					return this.writes.Count;
				}
			}
		}

		internal byte[] GetWrite(
			int index
		) {
			lock ( this.writes ) {
				return this.writes[ index ].ToArray();
			}
		}

		internal IReadOnlyList<byte[]> GetWrites() {
			lock ( this.writes ) {
				return this.writes.Select( static bytes => bytes.ToArray() ).ToArray();
			}
		}

		internal void Publish(
			byte[] bytes
		) {
			ArgumentNullException.ThrowIfNull( bytes );
			if ( !this.input.Writer.TryWrite( bytes.ToArray() ) ) {
				throw new InvalidOperationException( "The test input channel is closed." );
			}
		}

		public async ValueTask<int> ReadAsync(
			Memory<byte> buffer,
			CancellationToken cancellationToken = default
		) {
			cancellationToken.ThrowIfCancellationRequested();
			if ( this.pending is null ) {
				this.pending = await this.input.Reader.ReadAsync(
					cancellationToken
				).ConfigureAwait( false );
				this.pendingOffset = 0;
			}

			int count = Math.Min(
				buffer.Length,
				this.pending.Length - this.pendingOffset
			);
			this.pending.AsSpan( this.pendingOffset, count ).CopyTo( buffer.Span );
			this.pendingOffset += count;
			if ( this.pendingOffset == this.pending.Length ) {
				this.pending = null;
				this.pendingOffset = 0;
			}
			return count;
		}

		public ValueTask WriteAsync(
			ReadOnlyMemory<byte> buffer,
			CancellationToken cancellationToken = default
		) {
			cancellationToken.ThrowIfCancellationRequested();
			lock ( this.writes ) {
				this.writes.Add( buffer.ToArray() );
			}
			return ValueTask.CompletedTask;
		}

		public ValueTask FlushAsync(
			CancellationToken cancellationToken = default
		) {
			cancellationToken.ThrowIfCancellationRequested();
			Interlocked.Increment( ref this.flushCount );
			return ValueTask.CompletedTask;
		}
	}

	private sealed class TestLifecycleSource
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
				throw new InvalidOperationException( "The lifecycle test queue rejected a signal." );
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

	private sealed class TestTerminalControlProvider : ITerminalControlProvider {
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
						| TerminalControlCapabilities.LiveSize
						| TerminalControlCapabilities.ModeRead
						| TerminalControlCapabilities.ModeWrite
				)
			);
		}

		public TerminalControlResult<TerminalSize> GetSize(
			TerminalEndpoint endpoint
		) {
			ArgumentNullException.ThrowIfNull( endpoint );
			return TerminalControlResult<TerminalSize>.Available( new TerminalSize( 80, 24 ) );
		}

		public TerminalControlResult<TerminalModeSnapshot> GetMode(
			TerminalEndpoint endpoint
		) {
			ArgumentNullException.ThrowIfNull( endpoint );
			return TerminalControlResult<TerminalModeSnapshot>.Available( this.baseline );
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
