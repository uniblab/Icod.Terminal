namespace Icod.Terminal.Tests.Lifecycle;

using System.Text;
using System.Threading.Channels;
using Icod.Terminal;
using Icod.TermInfo;
using Xunit;

/// <summary>Verifies T143 lifecycle-safe dynamic-color ownership.</summary>
public sealed class TerminalDynamicColorLeaseTests {
	[Theory]
	[InlineData( TerminalDynamicColor.DefaultForeground, 10 )]
	[InlineData( TerminalDynamicColor.DefaultBackground, 11 )]
	[InlineData( TerminalDynamicColor.TextCursor, 12 )]
	[InlineData( TerminalDynamicColor.MouseForeground, 13 )]
	[InlineData( TerminalDynamicColor.MouseBackground, 14 )]
	[InlineData( TerminalDynamicColor.HighlightBackground, 17 )]
	[InlineData( TerminalDynamicColor.HighlightForeground, 19 )]
	public async Task EveryIdentityQueriesBeforeMutationAndReplaysExactBaseline(
		TerminalDynamicColor kind,
		int osc
	) {
		RecordingTransport transport = new();
		await using TerminalSession session = await OpenSessionAsync( transport );
		TerminalColor requested = new( 0xaaaa, 0xbbbb, 0xcccc );

		Task<TerminalDynamicColorLease> acquisition = session.AcquireDynamicColorAsync(
			kind,
			requested,
			TimeSpan.FromSeconds( 5 )
		).AsTask();
		await WaitForWriteCountAsync( transport, 1 );
		Assert.Equal(
			Encoding.ASCII.GetBytes( $"\u001b]{osc};?\u001b\\" ),
			transport.GetWrite( 0 )
		);
		transport.Publish(
			Encoding.ASCII.GetBytes( $"\u001b]{osc};rgb:1234/5678/9abc\u001b\\" )
		);

		TerminalDynamicColorLease lease = await acquisition;
		await WaitForWriteCountAsync( transport, 2 );
		Assert.Equal(
			Encoding.ASCII.GetBytes( $"\u001b]{osc};rgb:aaaa/bbbb/cccc\u001b\\" ),
			transport.GetWrite( 1 )
		);

		await lease.DisposeAsync();
		await WaitForWriteCountAsync( transport, 3 );
		Assert.Equal(
			Encoding.ASCII.GetBytes( $"\u001b]{osc};rgb:1234/5678/9abc\u001b\\" ),
			transport.GetWrite( 2 )
		);
		Assert.DoesNotContain(
			transport.GetWrites(),
			bytes => Encoding.ASCII.GetString( bytes ).Contains( $"]{osc + 100}", StringComparison.Ordinal )
		);
	}

	[Fact]
	public async Task SameIdentityOwnersReleaseOutOfOrderAndDifferentIdentitiesRemainIndependent() {
		RecordingTransport transport = new();
		await using TerminalSession session = await OpenSessionAsync( transport );
		TerminalDynamicColorLease foregroundOuter = await AcquireAsync(
			session,
			transport,
			TerminalDynamicColor.DefaultForeground,
			10,
			new TerminalColor( 0xaaaa, 0xbbbb, 0xcccc ),
			new TerminalColor( 0x1111, 0x2222, 0x3333 )
		);
		TerminalDynamicColorLease background = await AcquireAsync(
			session,
			transport,
			TerminalDynamicColor.DefaultBackground,
			11,
			new TerminalColor( 0x4444, 0x5555, 0x6666 ),
			new TerminalColor( 0x7777, 0x8888, 0x9999 )
		);
		int beforeInner = transport.WriteCount;
		TerminalDynamicColorLease foregroundInner = await session.AcquireDynamicColorAsync(
			TerminalDynamicColor.DefaultForeground,
			new TerminalColor( 0xdddd, 0xeeee, 0xffff ),
			TimeSpan.FromSeconds( 5 )
		);
		await WaitForWriteCountAsync( transport, beforeInner + 1 );
		Assert.Equal(
			Encoding.ASCII.GetBytes( "\u001b]10;rgb:dddd/eeee/ffff\u001b\\" ),
			transport.GetWrite( beforeInner )
		);

		int priorWrites = transport.WriteCount;
		await foregroundOuter.DisposeAsync();
		Assert.Equal( priorWrites, transport.WriteCount );

		await foregroundInner.DisposeAsync();
		await WaitForWriteCountAsync( transport, priorWrites + 1 );
		Assert.Equal(
			Encoding.ASCII.GetBytes( "\u001b]10;rgb:1111/2222/3333\u001b\\" ),
			transport.GetWrite( priorWrites )
		);

		await background.DisposeAsync();
		await WaitForWriteCountAsync( transport, priorWrites + 2 );
		Assert.Equal(
			Encoding.ASCII.GetBytes( "\u001b]11;rgb:7777/8888/9999\u001b\\" ),
			transport.GetWrite( priorWrites + 1 )
		);
	}

	[Theory]
	[InlineData( TerminalDynamicColor.DefaultForeground, 10 )]
	[InlineData( TerminalDynamicColor.HighlightForeground, 19 )]
	public async Task ResumeRefreshesBaselineBeforeReapplyingRetainedOwner(
		TerminalDynamicColor kind,
		int osc
	) {
		RecordingTransport transport = new();
		TestLifecycleSource lifecycle = new() {
			AutoResume = true
		};
		await using TerminalSession session = await OpenSessionAsync(
			transport,
			lifecycle
		);
		TerminalDynamicColorLease lease = await AcquireAsync(
			session,
			transport,
			kind,
			osc,
			new TerminalColor( 0xaaaa, 0xbbbb, 0xcccc ),
			new TerminalColor( 0x1111, 0x2222, 0x3333 )
		);
		using CancellationTokenSource timeout = new( TimeSpan.FromSeconds( 5 ) );

		int priorWrites = transport.WriteCount;
		lifecycle.Publish( TerminalLifecycleSignalKind.Suspend );
		TerminalLifecycleEvent suspending = await session.ReadLifecycleEventAsync( timeout.Token );
		Assert.Equal( TerminalLifecycleEventKind.Suspending, suspending.Kind );
		await WaitForWriteCountAsync( transport, priorWrites + 2 );
		Assert.Equal(
			Encoding.ASCII.GetBytes( $"\u001b]{osc};rgb:1111/2222/3333\u001b\\" ),
			transport.GetWrite( priorWrites )
		);
		Assert.Equal(
			Encoding.ASCII.GetBytes( $"\u001b]{osc};?\u001b\\" ),
			transport.GetWrite( priorWrites + 1 )
		);
		transport.Publish(
			Encoding.ASCII.GetBytes( $"\u001b]{osc};rgb:dead/beef/1234\u001b\\" )
		);

		TerminalLifecycleEvent resumed = await session.ReadLifecycleEventAsync( timeout.Token );
		Assert.Equal( TerminalLifecycleEventKind.Resumed, resumed.Kind );
		await WaitForWriteCountAsync( transport, priorWrites + 3 );
		Assert.Equal(
			Encoding.ASCII.GetBytes( $"\u001b]{osc};rgb:aaaa/bbbb/cccc\u001b\\" ),
			transport.GetWrite( priorWrites + 2 )
		);

		await lease.DisposeAsync();
		await WaitForWriteCountAsync( transport, priorWrites + 4 );
		Assert.Equal(
			Encoding.ASCII.GetBytes( $"\u001b]{osc};rgb:dead/beef/1234\u001b\\" ),
			transport.GetWrite( priorWrites + 3 )
		);
	}

	[Fact]
	public async Task UnscopedMutationIsRejectedWhileScopedDynamicColorOwnershipExists() {
		RecordingTransport transport = new();
		await using TerminalSession session = await OpenSessionAsync( transport );
		TerminalDynamicColorLease lease = await AcquireAsync(
			session,
			transport,
			TerminalDynamicColor.TextCursor,
			12,
			new TerminalColor( 0xaaaa, 0xbbbb, 0xcccc ),
			new TerminalColor( 0x1111, 0x2222, 0x3333 )
		);
		int priorWrites = transport.WriteCount;

		await Assert.ThrowsAsync<InvalidOperationException>(
			() => session.ResetDynamicColorAsync(
				TerminalDynamicColor.DefaultForeground
			).AsTask()
		);
		Assert.Equal( priorWrites, transport.WriteCount );

		await lease.DisposeAsync();
	}

	private static async ValueTask<TerminalDynamicColorLease> AcquireAsync(
		TerminalSession session,
		RecordingTransport transport,
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
