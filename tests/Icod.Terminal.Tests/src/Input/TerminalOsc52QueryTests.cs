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
namespace Icod.Terminal.Tests.Input;

using System.Text;
using System.Threading.Channels;
using Icod.Terminal;
using Icod.TermInfo;
using Xunit;

/// <summary>
/// Verifies the T57 explicit OSC 52 clipboard-read API on the shared query substrate.
/// </summary>
public sealed class TerminalOsc52QueryTests {
	[Fact]
	public async Task ConcurrentSessionsKeepReadBudgetsIndependent() {
		Osc52Transport smallTransport = new();
		Osc52Transport largeTransport = new();
		await using TerminalSession small = await OpenSessionAsync( smallTransport );
		await using TerminalSession large = await OpenSessionAsync( largeTransport, options: LargeReadOptions() );
		Task<byte[]> smallRead = small.ReadClipboardAsync( TerminalClipboardSelection.Clipboard, TimeSpan.FromSeconds( 5 ) ).AsTask();
		Task<byte[]> largeRead = large.ReadClipboardAsync( TerminalClipboardSelection.Clipboard, TimeSpan.FromSeconds( 5 ) ).AsTask();
		await Task.WhenAll( WaitForWriteCountAsync( smallTransport, 1 ), WaitForWriteCountAsync( largeTransport, 1 ) );
		byte[] payload = new byte[ 100_000 ];
		byte[] frame = Encoding.ASCII.GetBytes( "\u001b]52;c;" + Convert.ToBase64String( payload ) + "\u001b\\" );
		smallTransport.Publish( frame );
		largeTransport.Publish( frame );
		await Assert.ThrowsAsync<FormatException>( () => smallRead );
		Assert.Equal( payload, await largeRead );
	}

	[Theory]
	[InlineData( "\u001b]52;c;", "\u0007" )]
	[InlineData( "\u001b]52;c;", "\u001b\\" )]
	[InlineData( "\u009d52;c;", "\u009c" )]
	public async Task OptInOverflowOnTerminatorPreservesNextKey( string prefix, string terminator ) {
		Osc52Transport transport = new();
		await using TerminalSession session = await OpenSessionAsync( transport, options: LargeReadOptions() );
		Task<byte[]> query = session.ReadClipboardAsync(
			TerminalClipboardSelection.Clipboard, TimeSpan.FromSeconds( 2 ) ).AsTask();
		await WaitForWriteCountAsync( transport, 1 );
		int limit = TerminalClipboardLimits.GetResponseFrameLimit( 131_072 );
		transport.Publish( Encoding.Latin1.GetBytes( prefix
			+ new string( 'A', limit + 1 - prefix.Length - terminator.Length ) + terminator + "z" ) );
		await Assert.ThrowsAsync<FormatException>( () => query );
		Assert.Equal( new Rune( 'z' ), ( await session.ReadEventAsync( TimeSpan.FromSeconds( 1 ) ) ).Input?.Character );
	}

	[Fact]
	public async Task OptInRecoveryExhaustionFailsClosed() {
		Osc52Transport transport = new();
		await using TerminalSession session = await OpenSessionAsync( transport, options: LargeReadOptions() );
		Task<byte[]> query = session.ReadClipboardAsync(
			TerminalClipboardSelection.Clipboard, TimeSpan.FromSeconds( 5 ) ).AsTask();
		await WaitForWriteCountAsync( transport, 1 );
		transport.Publish( Encoding.ASCII.GetBytes( "\u001b]52;c;" + new string( 'A', 360_000 ) ) );
		await Assert.ThrowsAsync<InvalidOperationException>( () => query );
		await Assert.ThrowsAnyAsync<Exception>( async () => await session.ReadEventAsync( TimeSpan.FromSeconds( 1 ) ) );
	}

	[Fact]
	public async Task OptInReadWithSmallOrdinaryBufferIgnoresWrongSelection() {
		Osc52Transport transport = new() { MaximumReadChunk = 1 };
		await using TerminalSession session = await OpenSessionAsync( transport, options: new() {
			TerminalOverride = TerminalProfiles.Dumb,
			InputDecoderOptions = new() { MaximumBufferedBytes = 32 },
			ClipboardOptions = new() { MaximumReadBytes = 131_072 }
		} );
		Task<byte[]> query = session.ReadClipboardAsync(
			TerminalClipboardSelection.Clipboard, TimeSpan.FromSeconds( 5 ) ).AsTask();
		await WaitForWriteCountAsync( transport, 1 );
		byte[] payload = new byte[ 100_000 ];
		transport.Publish( Encoding.ASCII.GetBytes( "\u001b]52;p;eA==\u001b\\"
			+ "\u001b]52;c;" + Convert.ToBase64String( payload ) + "\u001b\\z" ) );
		Assert.Equal( payload, await query );
		// Wrong-selection frames retain ordinary decoding; they gain no clipboard allowance.
		TerminalEvent trailing;
		int events = 0;
		do {
			trailing = await session.ReadEventAsync( TimeSpan.FromSeconds( 1 ) );
			Assert.Equal( TerminalEventKind.Input, trailing.Kind );
			Assert.True( ++events <= 17 );
		} while ( trailing.Input?.Character != new Rune( 'z' ) );
		Assert.Equal( 32, session.Options.InputDecoderOptions.MaximumBufferedBytes );
	}

	private static TerminalSessionOptions LargeReadOptions() => new() {
		TerminalOverride = TerminalProfiles.Dumb,
		ClipboardOptions = new() { MaximumReadBytes = 131_072 }
	};

	[Theory]
	[InlineData( "\u001b]52;c;Zh==\u001b\\" )]
	[InlineData( "\u001b]52;c;SGVsbG8_\u0007" )]
	[InlineData( "\u001b]52;c;\u001b\u0007" )]
	[InlineData( "\u001b]52;c;\u001bXAAAA\u0007" )]
	public async Task OptInMalformedReadIsRejectedWithoutPayloadLeak( string response ) {
		Osc52Transport transport = new() { MaximumReadChunk = 1 };
		await using TerminalSession session = await OpenSessionAsync( transport, options: LargeReadOptions() );
		Task<byte[]> query = session.ReadClipboardAsync(
			TerminalClipboardSelection.Clipboard, TimeSpan.FromSeconds( 5 ) ).AsTask();
		await WaitForWriteCountAsync( transport, 1 );
		transport.Publish( Encoding.ASCII.GetBytes( response + "z" ) );
		FormatException failure = await Assert.ThrowsAsync<FormatException>( () => query );
		Assert.DoesNotContain( response, failure.Message );
		Assert.Equal( new Rune( 'z' ), ( await session.ReadEventAsync( TimeSpan.FromSeconds( 1 ) ) ).Input?.Character );
	}

	[Theory]
	[InlineData( "\u001b]52;c;", "\u001b\\" )]
	[InlineData( "\u001b]52;c;", "\u0007" )]
	[InlineData( "\u009d52;c;", "\u009c" )]
	public async Task OptInFrameOverflowDrainsOnlyThroughTerminator( string prefix, string terminator ) {
		Osc52Transport transport = new() { MaximumReadChunk = 127 };
		await using TerminalSession session = await OpenSessionAsync( transport, options: LargeReadOptions() );
		Task<byte[]> query = session.ReadClipboardAsync(
			TerminalClipboardSelection.Clipboard, TimeSpan.FromSeconds( 5 ) ).AsTask();
		await WaitForWriteCountAsync( transport, 1 );
		transport.Publish( Encoding.Latin1.GetBytes(
			prefix + new string( 'A', 180_000 ) + terminator + "z" ) );
		await Assert.ThrowsAsync<FormatException>( () => query );
		Assert.Equal( new Rune( 'z' ), ( await session.ReadEventAsync( TimeSpan.FromSeconds( 1 ) ) ).Input?.Character );
	}

	[Fact]
	public async Task OptInReadCancellationRetainsLateResponseOwnership() {
		Osc52Transport transport = new();
		await using TerminalSession session = await OpenSessionAsync( transport, options: LargeReadOptions() );
		using CancellationTokenSource stop = new();
		Task<byte[]> first = session.ReadClipboardAsync(
			TerminalClipboardSelection.Clipboard, TimeSpan.FromSeconds( 5 ), stop.Token ).AsTask();
		await WaitForWriteCountAsync( transport, 1 );
		transport.Publish( Encoding.ASCII.GetBytes( "\u001b]52;c;b2xk" ) );
		stop.Cancel();
		await Assert.ThrowsAnyAsync<OperationCanceledException>( () => first );
		Task<byte[]> second = session.ReadClipboardAsync(
			TerminalClipboardSelection.Clipboard, TimeSpan.FromSeconds( 5 ) ).AsTask();
		transport.Publish( Encoding.ASCII.GetBytes( "\u001b\\" ) );
		await WaitForWriteCountAsync( transport, 2 );
		transport.Publish( Encoding.ASCII.GetBytes( "\u001b]52;c;bmV3\u001b\\" ) );
		Assert.Equal( Encoding.ASCII.GetBytes( "new" ), await second );
	}

	[Fact]
	public async Task OptInReadEofFailsRatherThanReturningPartialData() {
		Osc52Transport transport = new();
		await using TerminalSession session = await OpenSessionAsync( transport, options: LargeReadOptions() );
		Task<byte[]> query = session.ReadClipboardAsync(
			TerminalClipboardSelection.Clipboard, TimeSpan.FromSeconds( 5 ) ).AsTask();
		await WaitForWriteCountAsync( transport, 1 );
		transport.Publish( Encoding.ASCII.GetBytes( "\u001b]52;c;YWJj" ) );
		transport.Publish( [] );
		await Assert.ThrowsAsync<FormatException>( () => query );
		Assert.Equal( TerminalInputEventKind.EndOfInput,
			( await session.ReadEventAsync( TimeSpan.FromSeconds( 1 ) ) ).Input?.Kind );
	}

	[Fact]
	public async Task OptInReadDisposalInterruptsUnterminatedResponse() {
		Osc52Transport transport = new();
		TerminalSession session = await OpenSessionAsync( transport, options: LargeReadOptions() );
		Task<byte[]> query = session.ReadClipboardAsync(
			TerminalClipboardSelection.Clipboard, TimeSpan.FromSeconds( 30 ) ).AsTask();
		await WaitForWriteCountAsync( transport, 1 );
		transport.Publish( Encoding.ASCII.GetBytes( "\u001b]52;c;" + new string( 'A', 90_000 ) ) );
		await session.DisposeAsync().AsTask().WaitAsync( TimeSpan.FromSeconds( 3 ) );
		await Assert.ThrowsAsync<ObjectDisposedException>( () => query );
	}

	[Fact]
	public async Task OptInReadTimeoutDoesNotReturnAnUnterminatedFrameAsKeys() {
		Osc52Transport transport = new();
		await using TerminalSession session = await OpenSessionAsync( transport, options: LargeReadOptions() );
		Task<byte[]> query = session.ReadClipboardAsync(
			TerminalClipboardSelection.Clipboard, TimeSpan.FromMilliseconds( 200 ) ).AsTask();
		await WaitForWriteCountAsync( transport, 1 );
		transport.Publish( Encoding.ASCII.GetBytes( "\u001b]52;c;YWJj" ) );
		await Assert.ThrowsAsync<TimeoutException>( () => query );
		await Assert.ThrowsAnyAsync<Exception>( async () =>
			await session.ReadEventAsync( TimeSpan.FromSeconds( 3 ) ) );
	}

	[Theory]
	[InlineData( 65_537, 256 )]
	[InlineData( 1_048_576, 256 )]
	[InlineData( 16_777_216, 256 )]
	[InlineData( 65_537, 1 )]
	public async Task OptInReadBudgetReturnsLargerPayloadAndPreservesTrailingInput( int size, int chunk ) {
		Osc52Transport transport = new() { MaximumReadChunk = chunk };
		await using TerminalSession session = await OpenSessionAsync( transport,
			options: new TerminalSessionOptions {
				TerminalOverride = TerminalProfiles.Dumb,
				ClipboardOptions = new() { MaximumReadBytes = size }
			} );
		byte[] payload = new byte[ size ];
		for ( int i = 0; i < payload.Length; i++ ) payload[ i ] = (byte)i;
		Task<byte[]> query = session.ReadClipboardAsync(
			TerminalClipboardSelection.Clipboard, TimeSpan.FromSeconds( 30 ) ).AsTask();
		await WaitForWriteCountAsync( transport, 1 );
		transport.Publish( Encoding.ASCII.GetBytes(
			"\u001b]52;c;" + Convert.ToBase64String( payload ) + "\u001b\\z" ) );
		Assert.Equal( payload, await query.WaitAsync( TimeSpan.FromSeconds( 35 ) ) );
		TerminalEvent trailing = await session.ReadEventAsync( TimeSpan.FromSeconds( 1 ) );
		Assert.Equal( new Rune( 'z' ), trailing.Input?.Character );
		Assert.Equal( 87_400, session.Options.InputDecoderOptions.MaximumBufferedBytes );
		Assert.Equal( 65_536, session.Options.ClipboardOptions.MaximumWriteBytes );
	}

	[Fact]
	public async Task SmallReadBudgetRejectsDecodedOverflowInSameBase64Block() {
		Osc52Transport transport = new();
		await using TerminalSession session = await OpenSessionAsync( transport,
			options: new TerminalSessionOptions {
				TerminalOverride = TerminalProfiles.Dumb,
				ClipboardOptions = new() { MaximumReadBytes = 1 }
			} );
		Task<byte[]> query = session.ReadClipboardAsync(
			TerminalClipboardSelection.Clipboard, TimeSpan.FromSeconds( 5 ) ).AsTask();
		await WaitForWriteCountAsync( transport, 1 );
		transport.Publish( Encoding.ASCII.GetBytes( "\u001b]52;c;YWI=\u001b\\" ) );
		await Assert.ThrowsAsync<FormatException>( () => query );
	}

	[Theory]
	[InlineData( TerminalClipboardSelection.Clipboard, 'c' )]
	[InlineData( TerminalClipboardSelection.Primary, 'p' )]
	[InlineData( TerminalClipboardSelection.Secondary, 'q' )]
	[InlineData( TerminalClipboardSelection.Select, 's' )]
	public async Task ExplicitReadUsesCanonicalQueryAndReturnsDecodedBytes(
		TerminalClipboardSelection selection,
		char selector
	) {
		Osc52Transport transport = new();
		await using TerminalSession session = await OpenSessionAsync( transport );

		Task<byte[]> query = session.ReadClipboardAsync(
			selection,
			TimeSpan.FromSeconds( 30 )
		).AsTask();
		await WaitForWriteCountAsync( transport, 1 );

		Assert.Equal(
			Encoding.ASCII.GetBytes( $"\u001b]52;{selector};?\u001b\\" ),
			transport.GetWrite( 0 )
		);
		Assert.Equal( 1, transport.FlushCount );

		transport.Publish(
			Encoding.ASCII.GetBytes( $"\u001b]52;{selector};AAECA/8=\u001b\\" )
		);

		Assert.Equal(
			new byte[] { 0, 1, 2, 3, 255 },
			await query
		);
	}

	[Fact]
	public async Task BelTerminatedResponseIsAccepted() {
		Osc52Transport transport = new();
		await using TerminalSession session = await OpenSessionAsync( transport );

		Task<byte[]> query = session.ReadClipboardAsync(
			TerminalClipboardSelection.Clipboard,
			TimeSpan.FromSeconds( 30 )
		).AsTask();
		await WaitForWriteCountAsync( transport, 1 );

		transport.Publish(
			Encoding.ASCII.GetBytes( "\u001b]52;c;SGVsbG8=\u0007" )
		);

		Assert.Equal( Encoding.ASCII.GetBytes( "Hello" ), await query );
	}

	[Fact]
	public async Task C1ResponseIsAccepted() {
		Osc52Transport transport = new();
		await using TerminalSession session = await OpenSessionAsync( transport );

		Task<byte[]> query = session.ReadClipboardAsync(
			TerminalClipboardSelection.Select,
			TimeSpan.FromSeconds( 30 )
		).AsTask();
		await WaitForWriteCountAsync( transport, 1 );

		transport.Publish(
			[
				0x9D,
				(byte)'5',
				(byte)'2',
				(byte)';',
				(byte)'s',
				(byte)';',
				(byte)'Z',
				(byte)'g',
				(byte)'=',
				(byte)'=',
				0x9C
			]
		);

		Assert.Equal( new byte[] { (byte)'f' }, await query );
	}

	[Theory]
	[InlineData( "\u001b]52;c;SGVsbG8_\u001b\\" )]
	[InlineData( "\u001b]52;c;Zh==\u001b\\" )]
	public async Task CorrelatedMalformedResponseFailsDeterministically(
		string response
	) {
		Osc52Transport transport = new();
		await using TerminalSession session = await OpenSessionAsync( transport );

		Task<byte[]> query = session.ReadClipboardAsync(
			TerminalClipboardSelection.Clipboard,
			TimeSpan.FromSeconds( 30 )
		).AsTask();
		await WaitForWriteCountAsync( transport, 1 );
		transport.Publish( Encoding.Latin1.GetBytes( response ) );

		await Assert.ThrowsAsync<FormatException>( () => query );
	}

	[Fact]
	public async Task OversizedCorrelatedResponseFailsAndPreservesTrailingInput() {
		Osc52Transport transport = new();
		await using TerminalSession session = await OpenSessionAsync( transport );

		Task<byte[]> query = session.ReadClipboardAsync(
			TerminalClipboardSelection.Clipboard,
			TimeSpan.FromSeconds( 30 )
		).AsTask();
		await WaitForWriteCountAsync( transport, 1 );

		byte[] prefix = Encoding.ASCII.GetBytes( "\u001b]52;c;" );
		byte[] response = Enumerable.Repeat(
			(byte)'A',
			TerminalOsc52PayloadCodec.MaximumFrameBytes + 3
		).ToArray();
		prefix.CopyTo( response, 0 );
		response[ TerminalOsc52PayloadCodec.MaximumFrameBytes ] = 0x1B;
		response[ TerminalOsc52PayloadCodec.MaximumFrameBytes + 1 ] = (byte)'\\';
		response[ TerminalOsc52PayloadCodec.MaximumFrameBytes + 2 ] = (byte)'z';
		transport.Publish( response );

		await Assert.ThrowsAsync<FormatException>( () => query );

		TerminalEvent trailing = await session.ReadEventAsync(
			TimeSpan.FromSeconds( 1 )
		);
		Assert.Equal( TerminalEventKind.Input, trailing.Kind );
		Assert.Equal( new Rune( 'z' ), trailing.Input?.Character );
	}

	[Fact]
	public async Task WrongSelectionDoesNotSatisfyQuery() {
		Osc52Transport transport = new();
		await using TerminalSession session = await OpenSessionAsync( transport );

		Task<byte[]> query = session.ReadClipboardAsync(
			TerminalClipboardSelection.Clipboard,
			TimeSpan.FromMilliseconds( 50 )
		).AsTask();
		await WaitForWriteCountAsync( transport, 1 );
		transport.Publish(
			Encoding.ASCII.GetBytes( "\u001b]52;p;SGVsbG8=\u001b\\" )
		);

		await Assert.ThrowsAsync<TimeoutException>( () => query );
	}

	[Fact]
	public async Task CancellationBeforeEmissionWritesNothing() {
		Osc52Transport transport = new();
		await using TerminalSession session = await OpenSessionAsync( transport );
		using CancellationTokenSource cancellationSource = new();
		cancellationSource.Cancel();

		await Assert.ThrowsAnyAsync<OperationCanceledException>(
			() => session.ReadClipboardAsync(
				TerminalClipboardSelection.Clipboard,
				TimeSpan.FromSeconds( 1 ),
				cancellationSource.Token
			).AsTask()
		);

		Assert.Equal( 0, transport.WriteCount );
		Assert.Equal( 0, transport.FlushCount );
	}

	[Fact]
	public async Task RedirectedOutputIsRejectedWithoutQueryEmission() {
		Osc52Transport transport = new();
		RecordingTerminalControlProvider provider = new() {
			OutputIsTerminal = false
		};
		await using TerminalSession session = await OpenSessionAsync(
			transport,
			provider,
			new TerminalSessionOptions {
				RequireInteractiveOutput = false,
				TerminalOverride = TerminalProfiles.Dumb
			}
		);

		await Assert.ThrowsAsync<InvalidOperationException>(
			() => session.ReadClipboardAsync(
				TerminalClipboardSelection.Clipboard,
				TimeSpan.FromSeconds( 1 )
			).AsTask()
		);

		Assert.Equal( 0, transport.WriteCount );
		Assert.Equal( 0, transport.FlushCount );
	}

	[Fact]
	public async Task InvalidSelectionAndTimeoutAreRejectedBeforeOutput() {
		Osc52Transport transport = new();
		await using TerminalSession session = await OpenSessionAsync( transport );

		await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
			() => session.ReadClipboardAsync(
				(TerminalClipboardSelection)int.MaxValue,
				TimeSpan.FromSeconds( 1 )
			).AsTask()
		);
		await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
			() => session.ReadClipboardAsync(
				TerminalClipboardSelection.Clipboard,
				TimeSpan.FromMilliseconds( -1 )
			).AsTask()
		);

		Assert.Equal( 0, transport.WriteCount );
	}

	private static ValueTask<TerminalSession> OpenSessionAsync(
		Osc52Transport transport,
		RecordingTerminalControlProvider? provider = null,
		TerminalSessionOptions? options = null
	) {
		ArgumentNullException.ThrowIfNull( transport );

		return TerminalSession.OpenAsync(
			provider ?? new RecordingTerminalControlProvider(),
			TerminalEndpoint.StandardInput,
			TerminalEndpoint.StandardOutput,
			transport,
			transport,
			options
			?? new TerminalSessionOptions {
				TerminalOverride = TerminalProfiles.Dumb
			}
		);
	}

	private static async Task WaitForWriteCountAsync(
		Osc52Transport transport,
		int count
	) {
		ArgumentNullException.ThrowIfNull( transport );

		for ( int attempt = 0; attempt < 500; attempt++ ) {
			if ( count <= transport.WriteCount ) {
				return;
			}
			await Task.Delay( 10 );
		}

		throw new TimeoutException( "The expected terminal query write was not observed." );
	}

	private sealed class Osc52Transport : ITerminalInput, ITerminalOutput {
		internal int MaximumReadChunk { get; init; } = int.MaxValue;
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
				Math.Min( buffer.Length, this.MaximumReadChunk ),
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

		internal bool OutputIsTerminal {
			get;
			init;
		} = true;

		public TerminalControlResult<TerminalEndpointObservation> Observe(
			TerminalEndpoint endpoint
		) {
			ArgumentNullException.ThrowIfNull( endpoint );
			bool isTerminal = !ReferenceEquals(
				endpoint,
				TerminalEndpoint.StandardOutput
			)
				|| this.OutputIsTerminal;
			return TerminalControlResult<TerminalEndpointObservation>.Available(
				new TerminalEndpointObservation(
					isTerminal,
					null,
					isTerminal ? TerminalPlatformKind.PosixTermios : null,
					isTerminal
						? TerminalControlCapabilities.Attachment
							| TerminalControlCapabilities.ModeRead
							| TerminalControlCapabilities.ModeWrite
						: TerminalControlCapabilities.None
				)
			);
		}

		public TerminalControlResult<TerminalSize> GetSize(
			TerminalEndpoint endpoint
		) {
			ArgumentNullException.ThrowIfNull( endpoint );
			return TerminalControlResult<TerminalSize>.Unsupported(
				"Size is not used by this test provider."
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
