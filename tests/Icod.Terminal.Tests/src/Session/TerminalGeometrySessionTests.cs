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
using System.Threading.Channels;
using Icod.Terminal;
using Icod.TermInfo;
using Xunit;

/// <summary>
/// Verifies C163 pixel-geometry queries on the authoritative session query path.
/// </summary>
public sealed class TerminalGeometrySessionTests {
	[Fact]
	public async Task TerminalAndCellPixelQueriesUseOneAuthoritativeInputPath() {
		GeometryTransport transport = new();
		await using TerminalSession session = await OpenSessionAsync( transport );

		Task<TerminalPixelDimensions> terminalQuery = session.QueryTerminalPixelDimensionsAsync(
			TimeSpan.FromSeconds( 30 )
		).AsTask();
		await WaitForWriteCountAsync( transport, 1 );
		Assert.Equal(
			Encoding.ASCII.GetBytes( "\u001b[14t" ),
			transport.GetWrite( 0 )
		);
		transport.Publish(
			Encoding.ASCII.GetBytes( "\u001b[4;800;1200t" )
		);
		TerminalPixelDimensions terminalSize = await terminalQuery;

		Task<TerminalPixelDimensions> cellQuery = session.QueryCellPixelDimensionsAsync(
			TimeSpan.FromSeconds( 30 )
		).AsTask();
		await WaitForWriteCountAsync( transport, 2 );
		Assert.Equal(
			Encoding.ASCII.GetBytes( "\u001b[16t" ),
			transport.GetWrite( 1 )
		);
		transport.Publish(
			Encoding.ASCII.GetBytes( "\u001b[6;20;10t" )
		);
		TerminalPixelDimensions cellSize = await cellQuery;

		Assert.Equal( 1200, terminalSize.Width );
		Assert.Equal( 800, terminalSize.Height );
		Assert.Equal( 10, cellSize.Width );
		Assert.Equal( 20, cellSize.Height );
		Assert.Equal( 1, transport.MaximumConcurrentReads );
	}

	[Fact]
	public async Task MismatchedGeometryReplyRemainsOrdinaryInput() {
		GeometryTransport transport = new();
		await using TerminalSession session = await OpenSessionAsync( transport );

		Task<TerminalPixelDimensions> query = session.QueryTerminalPixelDimensionsAsync(
			TimeSpan.FromSeconds( 30 )
		).AsTask();
		await WaitForWriteCountAsync( transport, 1 );
		transport.Publish(
			Encoding.ASCII.GetBytes( "\u001b[6;20;10t" )
		);
		await Task.Delay( TimeSpan.FromMilliseconds( 50 ) );
		Assert.False( query.IsCompleted );

		transport.Publish(
			Encoding.ASCII.GetBytes( "\u001b[4;800;1200t" )
		);
		Assert.Equal(
			new TerminalPixelDimensions( 1200, 800 ),
			await query
		);
	}

	[Fact]
	public async Task LateCellReplyCannotSatisfyLaterTerminalQuery() {
		GeometryTransport transport = new();
		await using TerminalSession session = await OpenSessionAsync( transport );

		await Assert.ThrowsAsync<TimeoutException>(
			() => session.QueryCellPixelDimensionsAsync(
				TimeSpan.FromMilliseconds( 50 )
			).AsTask()
		);

		Task<TerminalPixelDimensions> terminal = session.QueryTerminalPixelDimensionsAsync(
			TimeSpan.FromSeconds( 30 )
		).AsTask();
		await WaitForWriteCountAsync( transport, 2 );
		transport.Publish(
			Encoding.ASCII.GetBytes( "\u001b[6;20;10t" )
		);
		await Task.Delay( TimeSpan.FromMilliseconds( 50 ) );
		Assert.False( terminal.IsCompleted );

		transport.Publish(
			Encoding.ASCII.GetBytes( "\u001b[4;800;1200t" )
		);
		Assert.Equal(
			new TerminalPixelDimensions( 1200, 800 ),
			await terminal
		);
	}

	[Fact]
	public async Task PreCanceledGeometryQueryWritesNoBytes() {
		GeometryTransport transport = new();
		await using TerminalSession session = await OpenSessionAsync( transport );
		using CancellationTokenSource cancellation = new();
		cancellation.Cancel();

		await Assert.ThrowsAnyAsync<OperationCanceledException>(
			() => session.QueryCellPixelDimensionsAsync(
				TimeSpan.FromSeconds( 30 ),
				cancellation.Token
			).AsTask()
		);
		Assert.Equal( 0, transport.WriteCount );
	}

	[Fact]
	public async Task ResizeDoesNotRefreshOrCachePixelGeometry() {
		GeometryTransport transport = new();
		TestLifecycleSource lifecycle = new();
		GeometryTerminalControlProvider controlProvider = new();
		await using TerminalSession session = await OpenSessionAsync(
			transport,
			lifecycle,
			controlProvider
		);

		Task<TerminalPixelDimensions> first = session.QueryTerminalPixelDimensionsAsync(
			TimeSpan.FromSeconds( 30 )
		).AsTask();
		await WaitForWriteCountAsync( transport, 1 );
		transport.Publish( Encoding.ASCII.GetBytes( "\u001b[4;800;1200t" ) );
		Assert.Equal( new TerminalPixelDimensions( 1200, 800 ), await first );

		controlProvider.Size = new TerminalSize( 121, 40 );
		lifecycle.Publish( TerminalLifecycleSignalKind.Resize );
		using CancellationTokenSource timeout = new( TimeSpan.FromSeconds( 5 ) );
		TerminalLifecycleEvent resized = await session.ReadLifecycleEventAsync( timeout.Token );
		Assert.Equal( TerminalLifecycleEventKind.Resize, resized.Kind );
		Assert.Equal( 1, transport.WriteCount );

		Task<TerminalPixelDimensions> second = session.QueryTerminalPixelDimensionsAsync(
			TimeSpan.FromSeconds( 30 )
		).AsTask();
		await WaitForWriteCountAsync( transport, 2 );
		transport.Publish( Encoding.ASCII.GetBytes( "\u001b[4;900;1400t" ) );
		Assert.Equal( new TerminalPixelDimensions( 1400, 900 ), await second );
	}

	[Fact]
	public async Task CorrelatedMalformedGeometryReplyFailsWithoutRetry() {
		GeometryTransport transport = new();
		await using TerminalSession session = await OpenSessionAsync( transport );

		Task<TerminalPixelDimensions> query = session.QueryTerminalPixelDimensionsAsync(
			TimeSpan.FromSeconds( 30 )
		).AsTask();
		await WaitForWriteCountAsync( transport, 1 );
		transport.Publish( Encoding.ASCII.GetBytes( "\u001b[4;0;1200t" ) );

		await Assert.ThrowsAsync<FormatException>( () => query );
		Assert.Equal( 1, transport.WriteCount );
	}

	[Fact]
	public void ExactDerivationHandlesMaximumValuesWithoutOverflow() {
		Assert.True(
			TerminalPixelGeometry.TryDeriveCellDimensions(
				new TerminalDimensions( 1, 1 ),
				new TerminalPixelDimensions( int.MaxValue, int.MaxValue ),
				out TerminalPixelDimensions exact
			)
		);
		Assert.Equal( new TerminalPixelDimensions( int.MaxValue, int.MaxValue ), exact );
		Assert.False(
			TerminalPixelGeometry.TryDeriveCellDimensions(
				new TerminalDimensions( int.MaxValue, 2 ),
				new TerminalPixelDimensions( int.MaxValue, int.MaxValue ),
				out _
			)
		);
	}

	private static ValueTask<TerminalSession> OpenSessionAsync(
		GeometryTransport transport,
		ITerminalLifecycleSource? lifecycleSource = null,
		GeometryTerminalControlProvider? controlProvider = null
	) {
		ArgumentNullException.ThrowIfNull( transport );

		return TerminalSession.OpenAsync(
			controlProvider ?? new GeometryTerminalControlProvider(),
			TerminalEndpoint.StandardInput,
			TerminalEndpoint.StandardOutput,
			transport,
			transport,
			new TerminalSessionOptions {
				TerminalOverride = TerminalProfiles.Dumb,
				ConfigureOutput = false,
				LifecycleSource = lifecycleSource,
				ObserveLifecycleEvents = false,
				InputDecoderOptions = new TerminalInputDecoderOptions {
					EscapeSequenceTimeout = TimeSpan.Zero
				}
			}
		);
	}

	private sealed class TestLifecycleSource : ITerminalLifecycleSource {
		private readonly Channel<TerminalLifecycleSignal> signals =
			Channel.CreateUnbounded<TerminalLifecycleSignal>();

		internal void Publish( TerminalLifecycleSignalKind kind ) {
			Assert.True( this.signals.Writer.TryWrite( new TerminalLifecycleSignal( kind ) ) );
		}

		public ValueTask<TerminalLifecycleSignal> ReadAsync(
			CancellationToken cancellationToken = default
		) {
			return this.signals.Reader.ReadAsync( cancellationToken );
		}

		public void Dispose() {
			this.signals.Writer.TryComplete();
		}
	}

	private static async Task WaitForWriteCountAsync(
		GeometryTransport transport,
		int expected
	) {
		ArgumentNullException.ThrowIfNull( transport );
		if ( 0 > expected ) {
			throw new ArgumentOutOfRangeException( nameof( expected ) );
		}

		using CancellationTokenSource timeout = new();
		timeout.CancelAfter( TimeSpan.FromSeconds( 5 ) );
		await transport.WaitForWriteCountAsync(
			expected,
			timeout.Token
		);
	}

	private sealed class GeometryTerminalControlProvider : ITerminalControlProvider {
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

		internal TerminalSize Size {
			get;
			set;
		} = new TerminalSize( 120, 40 );

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
			return TerminalControlResult<TerminalSize>.Available( this.Size );
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

	private sealed class GeometryTransport : ITerminalInput, ITerminalOutput {
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
		private int activeReads;
		private int maximumConcurrentReads;

		internal int MaximumConcurrentReads {
			get {
				return Volatile.Read( ref this.maximumConcurrentReads );
			}
		}

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

		internal async ValueTask WaitForWriteCountAsync(
			int expected,
			CancellationToken cancellationToken
		) {
			if ( 0 > expected ) {
				throw new ArgumentOutOfRangeException( nameof( expected ) );
			}
			cancellationToken.ThrowIfCancellationRequested();

			while ( true ) {
				lock ( this.sync ) {
					if ( expected <= this.writes.Count ) {
						return;
					}
				}

				await this.writeSignal.WaitAsync(
					cancellationToken
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

			int active = Interlocked.Increment( ref this.activeReads );
			RecordMaximum(
				ref this.maximumConcurrentReads,
				active
			);
			try {
				byte[] bytes = await this.input.Reader.ReadAsync(
					cancellationToken
				).ConfigureAwait( false );
				if ( bytes.Length > buffer.Length ) {
					throw new InvalidOperationException(
						"The scripted input chunk exceeds the decoder read buffer."
					);
				}

				bytes.AsSpan().CopyTo( buffer.Span );
				return bytes.Length;
			} finally {
				Interlocked.Decrement( ref this.activeReads );
			}
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

		private static void RecordMaximum(
			ref int target,
			int candidate
		) {
			while ( true ) {
				int observed = Volatile.Read( ref target );
				if ( candidate <= observed ) {
					return;
				}
				if ( observed == Interlocked.CompareExchange(
					ref target,
					candidate,
					observed
				) ) {
					return;
				}
			}
		}
	}
}
