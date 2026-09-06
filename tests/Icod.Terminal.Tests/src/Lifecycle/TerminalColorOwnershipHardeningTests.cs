namespace Icod.Terminal.Tests.Lifecycle;

using System.Text;
using System.Threading.Channels;
using Icod.Terminal;
using Icod.TermInfo;
using Xunit;

/// <summary>
/// Exercises T144 cancellation, lifecycle, cleanup-retry, and cross-manager lock ordering.
/// </summary>
public sealed class TerminalColorOwnershipHardeningTests {
	[Fact]
	public async Task PreCancelledPaletteAcquisitionEmitsNothing() {
		FaultTransport transport = new();
		await using TerminalSession session = await OpenSessionAsync( transport );
		using CancellationTokenSource cancellation = new();
		cancellation.Cancel();

		await Assert.ThrowsAnyAsync<OperationCanceledException>(
			() => session.AcquirePaletteColorAsync(
				4,
				new TerminalColor( 0x1111, 0x2222, 0x3333 ),
				TimeSpan.FromSeconds( 5 ),
				cancellation.Token
			).AsTask()
		);
		Assert.Equal( 0, transport.WriteCount );
	}

	[Fact]
	public async Task SuspendInterruptsOutstandingPaletteBaselineQueryWithoutDeadlock() {
		FaultTransport transport = new();
		TestLifecycleSource lifecycle = new();
		await using TerminalSession session = await OpenSessionAsync(
			transport,
			lifecycle
		);
		using CancellationTokenSource timeout = new( TimeSpan.FromSeconds( 5 ) );

		Task<TerminalPaletteColorLease> acquisition = session.AcquirePaletteColorAsync(
			6,
			new TerminalColor( 0xaaaa, 0xbbbb, 0xcccc ),
			TimeSpan.FromSeconds( 5 )
		).AsTask();
		await WaitForWriteCountAsync( transport, 1 );
		Assert.Equal(
			Encoding.ASCII.GetBytes( "\u001b]4;6;?\u001b\\" ),
			transport.GetWrite( 0 )
		);

		lifecycle.Publish( TerminalLifecycleSignalKind.Suspend );
		TerminalLifecycleEvent suspending = await session.ReadLifecycleEventAsync( timeout.Token );
		Assert.Equal( TerminalLifecycleEventKind.Suspending, suspending.Kind );
		await Assert.ThrowsAsync<InvalidOperationException>( () => acquisition );

		lifecycle.Publish( TerminalLifecycleSignalKind.Resume );
		TerminalLifecycleEvent resumed = await session.ReadLifecycleEventAsync( timeout.Token );
		Assert.Equal( TerminalLifecycleEventKind.Resumed, resumed.Kind );
		Assert.Equal( 1, transport.WriteCount );
	}

	[Fact]
	public async Task CancelledEmittedPaletteQueryDoesNotPoisonLaterAcquisition() {
		FaultTransport transport = new();
		await using TerminalSession session = await OpenSessionAsync( transport );
		using CancellationTokenSource cancellation = new();

		Task<TerminalPaletteColorLease> cancelled = session.AcquirePaletteColorAsync(
			1,
			new TerminalColor( 0xaaaa, 0xbbbb, 0xcccc ),
			TimeSpan.FromSeconds( 5 ),
			cancellation.Token
		).AsTask();
		await WaitForWriteCountAsync( transport, 1 );
		cancellation.Cancel();
		await Assert.ThrowsAnyAsync<OperationCanceledException>( () => cancelled );

		transport.Publish(
			Encoding.ASCII.GetBytes( "\u001b]4;1;rgb:1111/2222/3333\u001b\\" )
		);

		Task<TerminalPaletteColorLease> next = session.AcquirePaletteColorAsync(
			2,
			new TerminalColor( 0x4444, 0x5555, 0x6666 ),
			TimeSpan.FromSeconds( 5 )
		).AsTask();
		await WaitForWriteCountAsync( transport, 2 );
		Assert.Equal(
			Encoding.ASCII.GetBytes( "\u001b]4;2;?\u001b\\" ),
			transport.GetWrite( 1 )
		);
		transport.Publish(
			Encoding.ASCII.GetBytes( "\u001b]4;2;rgb:7777/8888/9999\u001b\\" )
		);
		TerminalPaletteColorLease lease = await next;
		await WaitForWriteCountAsync( transport, 3 );
		await lease.DisposeAsync();
	}

	[Fact]
	public async Task FailedPaletteReleaseRetainsOwnershipAndRetryRestoresBaseline() {
		FaultTransport transport = new();
		await using TerminalSession session = await OpenSessionAsync( transport );
		TerminalPaletteColorLease lease = await AcquirePaletteAsync(
			session,
			transport,
			3,
			new TerminalColor( 0xaaaa, 0xbbbb, 0xcccc ),
			new TerminalColor( 0x1111, 0x2222, 0x3333 )
		);

		transport.FailNextWrite();
		await Assert.ThrowsAsync<IOException>(
			() => lease.DisposeAsync().AsTask()
		);
		int writesAfterFailure = transport.WriteCount;

		await lease.DisposeAsync();
		await WaitForWriteCountAsync( transport, writesAfterFailure + 2 );
		Assert.Equal(
			Encoding.ASCII.GetBytes( "\u001b]4;3;rgb:aaaa/bbbb/cccc\u001b\\" ),
			transport.GetWrite( writesAfterFailure )
		);
		Assert.Equal(
			Encoding.ASCII.GetBytes( "\u001b]4;3;rgb:1111/2222/3333\u001b\\" ),
			transport.GetWrite( writesAfterFailure + 1 )
		);
	}

	[Fact]
	public async Task FailedDynamicReleaseRetainsOwnershipAndRetryRestoresBaseline() {
		FaultTransport transport = new();
		await using TerminalSession session = await OpenSessionAsync( transport );
		TerminalDynamicColorLease lease = await AcquireDynamicAsync(
			session,
			transport,
			TerminalDynamicColor.TextCursor,
			12,
			new TerminalColor( 0xaaaa, 0xbbbb, 0xcccc ),
			new TerminalColor( 0x1111, 0x2222, 0x3333 )
		);

		transport.FailNextWrite();
		await Assert.ThrowsAsync<IOException>(
			() => lease.DisposeAsync().AsTask()
		);
		int writesAfterFailure = transport.WriteCount;

		await lease.DisposeAsync();
		await WaitForWriteCountAsync( transport, writesAfterFailure + 2 );
		Assert.Equal(
			Encoding.ASCII.GetBytes( "\u001b]12;rgb:aaaa/bbbb/cccc\u001b\\" ),
			transport.GetWrite( writesAfterFailure )
		);
		Assert.Equal(
			Encoding.ASCII.GetBytes( "\u001b]12;rgb:1111/2222/3333\u001b\\" ),
			transport.GetWrite( writesAfterFailure + 1 )
		);
	}

	[Fact]
	public async Task PaletteAndDynamicAcquisitionSerializeQueriesWithoutDeadlock() {
		FaultTransport transport = new();
		await using TerminalSession session = await OpenSessionAsync( transport );

		Task<TerminalPaletteColorLease> paletteTask = session.AcquirePaletteColorAsync(
			5,
			new TerminalColor( 0xaaaa, 0xbbbb, 0xcccc ),
			TimeSpan.FromSeconds( 5 )
		).AsTask();
		Task<TerminalDynamicColorLease> dynamicTask = session.AcquireDynamicColorAsync(
			TerminalDynamicColor.DefaultForeground,
			new TerminalColor( 0x4444, 0x5555, 0x6666 ),
			TimeSpan.FromSeconds( 5 )
		).AsTask();

		int firstQueryIndex = await WaitForAndRespondToNextColorQueryAsync(
			transport,
			0
		);
		_ = await WaitForAndRespondToNextColorQueryAsync(
			transport,
			firstQueryIndex + 1
		);

		TerminalPaletteColorLease palette = await paletteTask;
		TerminalDynamicColorLease dynamic = await dynamicTask;

		await Task.WhenAll(
			palette.DisposeAsync().AsTask(),
			dynamic.DisposeAsync().AsTask()
		);
	}

	[Fact]
	public async Task SessionDisposalRestoresOutstandingPaletteAndDynamicOwners() {
		FaultTransport transport = new();
		TerminalSession session = await OpenSessionAsync( transport );
		TerminalPaletteColorLease palette = await AcquirePaletteAsync(
			session,
			transport,
			7,
			new TerminalColor( 0xaaaa, 0xbbbb, 0xcccc ),
			new TerminalColor( 0x1111, 0x2222, 0x3333 )
		);
		TerminalDynamicColorLease dynamic = await AcquireDynamicAsync(
			session,
			transport,
			TerminalDynamicColor.DefaultBackground,
			11,
			new TerminalColor( 0x4444, 0x5555, 0x6666 ),
			new TerminalColor( 0x7777, 0x8888, 0x9999 )
		);
		int beforeDispose = transport.WriteCount;

		await session.DisposeAsync();
		await WaitForWriteCountAsync( transport, beforeDispose + 2 );
		IReadOnlyList<string> cleanup = transport.GetWrites()
			.Skip( beforeDispose )
			.Select( static bytes => Encoding.ASCII.GetString( bytes ) )
			.ToArray();
		Assert.Contains( "\u001b]4;7;rgb:1111/2222/3333\u001b\\", cleanup );
		Assert.Contains( "\u001b]11;rgb:7777/8888/9999\u001b\\", cleanup );

		int afterDispose = transport.WriteCount;
		await palette.DisposeAsync();
		await dynamic.DisposeAsync();
		Assert.Equal( afterDispose, transport.WriteCount );
	}

	private static async Task<int> WaitForAndRespondToNextColorQueryAsync(
		FaultTransport transport,
		int startIndex
	) {
		ArgumentNullException.ThrowIfNull( transport );
		if ( 0 > startIndex ) {
			throw new ArgumentOutOfRangeException( nameof( startIndex ) );
		}

		for ( int attempt = 0; attempt < 500; ++attempt ) {
			IReadOnlyList<byte[]> writes = transport.GetWrites();
			for ( int index = startIndex; index < writes.Count; ++index ) {
				if ( TryRespondToColorQuery(
					writes[ index ],
					transport
				) ) {
					return index;
				}
			}
			await Task.Delay( 10 );
		}

		throw new TimeoutException( "The next serialized color query was not observed." );
	}

	private static bool TryRespondToColorQuery(
		byte[] request,
		FaultTransport transport
	) {
		ArgumentNullException.ThrowIfNull( request );
		ArgumentNullException.ThrowIfNull( transport );
		string text = Encoding.ASCII.GetString( request );
		if ( "\u001b]4;5;?\u001b\\" == text ) {
			transport.Publish(
				Encoding.ASCII.GetBytes( "\u001b]4;5;rgb:1111/2222/3333\u001b\\" )
			);
			return true;
		}
		if ( "\u001b]10;?\u001b\\" == text ) {
			transport.Publish(
				Encoding.ASCII.GetBytes( "\u001b]10;rgb:7777/8888/9999\u001b\\" )
			);
			return true;
		}

		return false;
	}

	private static async ValueTask<TerminalPaletteColorLease> AcquirePaletteAsync(
		TerminalSession session,
		FaultTransport transport,
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

	private static async ValueTask<TerminalDynamicColorLease> AcquireDynamicAsync(
		TerminalSession session,
		FaultTransport transport,
		TerminalDynamicColor kind,
		int osc,
		TerminalColor requested,
		TerminalColor baseline
	) {
		ArgumentNullException.ThrowIfNull( session );
		ArgumentNullException.ThrowIfNull( transport );
		int initialWrites = transport.WriteCount;
		Task<TerminalDynamicColorLease> acquisition = session.AcquireDynamicColorAsync(
			kind,
			requested,
			TimeSpan.FromSeconds( 5 )
		).AsTask();
		await WaitForWriteCountAsync( transport, initialWrites + 1 );
		transport.Publish(
			Encoding.ASCII.GetBytes(
				$"\u001b]{osc};rgb:{baseline.Red:x4}/{baseline.Green:x4}/{baseline.Blue:x4}\u001b\\"
			)
		);
		TerminalDynamicColorLease lease = await acquisition;
		await WaitForWriteCountAsync( transport, initialWrites + 2 );
		return lease;
	}

	private static ValueTask<TerminalSession> OpenSessionAsync(
		FaultTransport transport,
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
		FaultTransport transport,
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

	private sealed class FaultTransport : ITerminalInput, ITerminalOutput {
		private readonly Channel<byte[]> input = Channel.CreateUnbounded<byte[]>();
		private readonly List<byte[]> writes = [];
		private byte[]? pending;
		private int pendingOffset;
		private int failNextWrite;

		internal int WriteCount {
			get {
				lock ( this.writes ) {
					return this.writes.Count;
				}
			}
		}

		internal void FailNextWrite() {
			Interlocked.Exchange( ref this.failNextWrite, 1 );
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
			if ( 0 != Interlocked.Exchange( ref this.failNextWrite, 0 ) ) {
				throw new IOException( "Injected terminal output failure." );
			}
			lock ( this.writes ) {
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

	private sealed class TestLifecycleSource
		: ITerminalLifecycleSource,
		  ITerminalSuspendController {
		private readonly Channel<TerminalLifecycleSignal> signals =
			Channel.CreateUnbounded<TerminalLifecycleSignal>();

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
			return TerminalControlResult<TerminalSize>.Available(
				new TerminalSize( 80, 24 )
			);
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
