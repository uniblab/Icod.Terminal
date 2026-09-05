namespace Icod.Terminal.Tests.Input;

using System.Text;
using System.Threading.Channels;
using Icod.Terminal;
using Icod.TermInfo;
using Xunit;

/// <summary>
/// Verifies Kitty-compatible OSC 22 pointer-shape queries on the shared active-query router.
/// </summary>
public sealed class TerminalOsc22QueryTests {
	[Theory]
	[InlineData( "current", "\u001b]22;?__current__\u001b\\", "pointer", TerminalPointerShape.Pointer )]
	[InlineData( "default", "\u001b]22;?__default__\u001b\\", "text", TerminalPointerShape.Text )]
	[InlineData( "grabbed", "\u001b]22;?__grabbed__\u001b\\", "grabbing", TerminalPointerShape.Grabbing )]
	public async Task ShapeQueriesUseCanonicalRequestsAndParseSemanticReplies(
		string queryKind,
		string request,
		string responseShape,
		TerminalPointerShape expected
	) {
		Osc22Transport transport = new();
		await using TerminalSession session = await OpenSessionAsync( transport );

		Task<TerminalPointerShapeObservation> query = queryKind switch {
			"current" => session.QueryCurrentPointerShapeAsync(
				TimeSpan.FromSeconds( 30 )
			).AsTask(),
			"default" => session.QueryDefaultPointerShapeAsync(
				TimeSpan.FromSeconds( 30 )
			).AsTask(),
			"grabbed" => session.QueryGrabbedPointerShapeAsync(
				TimeSpan.FromSeconds( 30 )
			).AsTask(),
			_ => throw new InvalidOperationException( "Unknown test query kind." )
		};
		await WaitForWriteCountAsync( transport, 1 );

		Assert.Equal(
			Encoding.ASCII.GetBytes( request ),
			transport.GetWrite( 0 )
		);
		Assert.Equal( 1, transport.FlushCount );
		transport.Publish(
			Encoding.ASCII.GetBytes( $"\u001b]22;{responseShape}\u001b\\" )
		);

		TerminalPointerShapeObservation observation = await query;
		Assert.True( observation.HasShape );
		Assert.Equal( expected, observation.Shape );
	}

	[Fact]
	public async Task CurrentQueryExplicitZeroMeansNoApplicationShape() {
		Osc22Transport transport = new();
		await using TerminalSession session = await OpenSessionAsync( transport );

		Task<TerminalPointerShapeObservation> query = session.QueryCurrentPointerShapeAsync(
			TimeSpan.FromSeconds( 30 )
		).AsTask();
		await WaitForWriteCountAsync( transport, 1 );
		transport.Publish(
			Encoding.ASCII.GetBytes( "\u001b]22;0\u001b\\" )
		);

		TerminalPointerShapeObservation observation = await query;
		Assert.False( observation.HasShape );
		Assert.Null( observation.Shape );
	}

	[Theory]
	[InlineData( "1", true )]
	[InlineData( "0", false )]
	public async Task SingleShapeSupportQueryReturnsExplicitReply(
		string reply,
		bool expected
	) {
		Osc22Transport transport = new();
		await using TerminalSession session = await OpenSessionAsync( transport );

		Task<bool> query = session.QueryPointerShapeSupportAsync(
			TerminalPointerShape.Crosshair,
			TimeSpan.FromSeconds( 30 )
		).AsTask();
		await WaitForWriteCountAsync( transport, 1 );

		Assert.Equal(
			Encoding.ASCII.GetBytes( "\u001b]22;?crosshair\u001b\\" ),
			transport.GetWrite( 0 )
		);
		transport.Publish(
			Encoding.ASCII.GetBytes( $"\u001b]22;{reply}\u001b\\" )
		);

		Assert.Equal( expected, await query );
	}

	[Fact]
	public async Task BelTerminatedShapeResponseIsAccepted() {
		Osc22Transport transport = new();
		await using TerminalSession session = await OpenSessionAsync( transport );
		Task<TerminalPointerShapeObservation> query = session.QueryDefaultPointerShapeAsync(
			TimeSpan.FromSeconds( 30 )
		).AsTask();
		await WaitForWriteCountAsync( transport, 1 );

		transport.Publish(
			Encoding.ASCII.GetBytes( "\u001b]22;wait\u0007" )
		);

		TerminalPointerShapeObservation observation = await query;
		Assert.Equal( TerminalPointerShape.Wait, observation.Shape );
	}

	[Fact]
	public async Task C1OscResponseIsAccepted() {
		Osc22Transport transport = new();
		await using TerminalSession session = await OpenSessionAsync( transport );
		Task<bool> query = session.QueryPointerShapeSupportAsync(
			TerminalPointerShape.Pointer,
			TimeSpan.FromSeconds( 30 )
		).AsTask();
		await WaitForWriteCountAsync( transport, 1 );

		transport.Publish(
			[
				0x9d,
				(byte)'2',
				(byte)'2',
				(byte)';',
				(byte)'1',
				0x9c
			]
		);

		Assert.True( await query );
	}

	[Theory]
	[InlineData( "\u001b]22;unknown\u001b\\" )]
	[InlineData( "\u001b]22;1,0\u001b\\" )]
	public async Task CorrelatedMalformedShapeResponseFailsDeterministically(
		string response
	) {
		Osc22Transport transport = new();
		await using TerminalSession session = await OpenSessionAsync( transport );
		Task<TerminalPointerShapeObservation> query = session.QueryCurrentPointerShapeAsync(
			TimeSpan.FromSeconds( 30 )
		).AsTask();
		await WaitForWriteCountAsync( transport, 1 );
		transport.Publish( Encoding.Latin1.GetBytes( response ) );

		await Assert.ThrowsAsync<FormatException>( () => query );
	}

	[Fact]
	public async Task MalformedSupportResponseFailsDeterministically() {
		Osc22Transport transport = new();
		await using TerminalSession session = await OpenSessionAsync( transport );
		Task<bool> query = session.QueryPointerShapeSupportAsync(
			TerminalPointerShape.Pointer,
			TimeSpan.FromSeconds( 30 )
		).AsTask();
		await WaitForWriteCountAsync( transport, 1 );
		transport.Publish(
			Encoding.ASCII.GetBytes( "\u001b]22;yes\u001b\\" )
		);

		await Assert.ThrowsAsync<FormatException>( () => query );
	}

	[Fact]
	public async Task PreCancelledQueryWritesNothing() {
		Osc22Transport transport = new();
		await using TerminalSession session = await OpenSessionAsync( transport );
		using CancellationTokenSource cancellation = new();
		cancellation.Cancel();

		await Assert.ThrowsAnyAsync<OperationCanceledException>(
			() => session.QueryCurrentPointerShapeAsync(
				TimeSpan.FromSeconds( 1 ),
				cancellation.Token
			).AsTask()
		);
		Assert.Equal( 0, transport.WriteCount );
		Assert.Equal( 0, transport.FlushCount );
	}

	private static ValueTask<TerminalSession> OpenSessionAsync(
		Osc22Transport transport
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
				ObserveLifecycleEvents = false
			}
		);
	}

	private static async Task WaitForWriteCountAsync(
		Osc22Transport transport,
		int count
	) {
		ArgumentNullException.ThrowIfNull( transport );
		for ( int attempt = 0; attempt < 500; ++attempt ) {
			if ( count <= transport.WriteCount ) {
				return;
			}
			await Task.Delay( 10 );
		}
		throw new TimeoutException( "The expected OSC 22 query write was not observed." );
	}

	private sealed class Osc22Transport : ITerminalInput, ITerminalOutput {
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

		internal int FlushCount {
			get {
				return Volatile.Read( ref this.flushCount );
			}
		}

		internal byte[] GetWrite(
			int index
		) {
			lock ( this.writes ) {
				return this.writes[ index ].ToArray();
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
				"Size is not used by OSC 22 query tests."
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
