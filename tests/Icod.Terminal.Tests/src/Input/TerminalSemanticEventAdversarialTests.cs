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
using Icod.TermInfo;
using Icod.Timing;
using Xunit;

/// <summary>
/// Defines the E198 adversarial semantic-event decoder and buffering matrix.
/// </summary>
public sealed class TerminalSemanticEventAdversarialTests {
	[Fact]
	public async Task EverySevenBitSplitPointRoutesActivationReport() {
		byte[] frame = Encoding.ASCII.GetBytes(
			"\u001b]99;i=split-seven;\u001b\\"
		);

		for ( int split = 1; split < frame.Length; ++split ) {
			SegmentedTerminalInput input = new(
				frame[..split],
				frame[split..]
			);
			TerminalInputDecoder decoder = CreateDecoder( input );

			TerminalNotificationEvent notification = await ReadNotificationAsync(
				decoder
			);

			Assert.Equal( TerminalNotificationEventKind.Activated, notification.Kind );
			Assert.Equal( "split-seven", notification.Identifier );
		}
	}

	[Fact]
	public async Task EveryEightBitSplitPointRoutesActivationReport() {
		byte[] body = Encoding.ASCII.GetBytes( "99;i=split-eight;" );
		byte[] frame = new byte[ body.Length + 2 ];
		frame[ 0 ] = 0x9d;
		body.CopyTo( frame, 1 );
		frame[ ^1 ] = 0x9c;

		for ( int split = 1; split < frame.Length; ++split ) {
			SegmentedTerminalInput input = new(
				frame[..split],
				frame[split..]
			);
			TerminalInputDecoder decoder = CreateDecoder( input );

			TerminalNotificationEvent notification = await ReadNotificationAsync(
				decoder
			);

			Assert.Equal( TerminalNotificationEventKind.Activated, notification.Kind );
			Assert.Equal( "split-eight", notification.Identifier );
		}
	}

	[Fact]
	public async Task RepeatedIdentifierEventsRemainIndependentAndOrdered() {
		SegmentedTerminalInput input = new(
			Encoding.ASCII.GetBytes(
				"\u001b]99;i=same;\u001b\\"
					+ "\u001b]99;i=same;2\u001b\\"
					+ "\u001b]99;i=same:p=close;\u001b\\"
			)
		);
		TerminalInputDecoder decoder = CreateDecoder( input );

		TerminalNotificationEvent first = await ReadNotificationAsync( decoder );
		TerminalNotificationEvent second = await ReadNotificationAsync( decoder );
		TerminalNotificationEvent third = await ReadNotificationAsync( decoder );

		Assert.Equal( TerminalNotificationEventKind.Activated, first.Kind );
		Assert.Equal( TerminalNotificationEventKind.ButtonActivated, second.Kind );
		Assert.Equal( 2, second.ButtonNumber );
		Assert.Equal( TerminalNotificationEventKind.Closed, third.Kind );
		Assert.Equal( "same", first.Identifier );
		Assert.Equal( "same", second.Identifier );
		Assert.Equal( "same", third.Identifier );
	}

	[Fact]
	public async Task MalformedOwnedReportIsDiscardedBeforeFollowingInput() {
		SegmentedTerminalInput input = new(
			Encoding.ASCII.GetBytes(
				"\u001b]99;i=first:i=duplicate;\u001b\\x"
			)
		);
		using CancellationTokenSource stop = new();
		TerminalInputCoordinator coordinator = CreateCoordinator(
			input,
			stop.Token
		);

		try {
			TerminalEvent terminalEvent = (
				await coordinator.ReadAsync().AsTask().WaitAsync(
					TimeSpan.FromSeconds( 5 )
				)
			).ToTerminalEvent();

			Assert.Equal( TerminalEventKind.Input, terminalEvent.Kind );
			Assert.Equal( new Rune( 'x' ), terminalEvent.Input?.Character );
		} finally {
			stop.Cancel();
		}
	}

	[Theory]
	[InlineData( 0x18 )]
	[InlineData( 0x1a )]
	public async Task CancelledOwnedReportDoesNotLeakIntoOrdinaryInput(
		int cancellationByte
	) {
		byte[] prefix = Encoding.ASCII.GetBytes( "\u001b]99;i=cancelled;" );
		byte[] bytes = new byte[ prefix.Length + 2 ];
		prefix.CopyTo( bytes, 0 );
		bytes[ prefix.Length ] = checked( (byte)cancellationByte );
		bytes[ ^1 ] = (byte)'x';
		SegmentedTerminalInput input = new( bytes );
		using CancellationTokenSource stop = new();
		TerminalInputCoordinator coordinator = CreateCoordinator(
			input,
			stop.Token
		);

		try {
			TerminalEvent terminalEvent = (
				await coordinator.ReadAsync().AsTask().WaitAsync(
					TimeSpan.FromSeconds( 5 )
				)
			).ToTerminalEvent();

			Assert.Equal( TerminalEventKind.Input, terminalEvent.Kind );
			Assert.Equal( new Rune( 'x' ), terminalEvent.Input?.Character );
		} finally {
			stop.Cancel();
		}
	}

	[Fact]
	public async Task InvalidMixedWidthTerminatorDoesNotLeakOwnedReportBytes() {
		byte[] prefix = Encoding.ASCII.GetBytes( "\u001b]99;i=mixed;" );
		byte[] bytes = new byte[ prefix.Length + 2 ];
		prefix.CopyTo( bytes, 0 );
		bytes[ prefix.Length ] = 0x9c;
		bytes[ ^1 ] = (byte)'x';
		SegmentedTerminalInput input = new( bytes );
		using CancellationTokenSource stop = new();
		TerminalInputCoordinator coordinator = CreateCoordinator(
			input,
			stop.Token
		);

		try {
			TerminalEvent terminalEvent = (
				await coordinator.ReadAsync().AsTask().WaitAsync(
					TimeSpan.FromSeconds( 5 )
				)
			).ToTerminalEvent();

			Assert.Equal( TerminalEventKind.Input, terminalEvent.Kind );
			Assert.Equal( new Rune( 'x' ), terminalEvent.Input?.Character );
		} finally {
			stop.Cancel();
		}
	}

	[Fact]
	public async Task OversizedOwnedReportDrainsThroughTerminatorBeforeRecovery() {
		string oversizedIdentifier = new( 'a', 96 );
		byte[] bytes = Encoding.ASCII.GetBytes(
			"\u001b]99;i=" + oversizedIdentifier + ";\u001b\\x"
		);
		SegmentedTerminalInput input = new(
			SplitEvery( bytes, 16 )
		);
		using CancellationTokenSource stop = new();
		TerminalInputCoordinator coordinator = CreateCoordinator(
			input,
			stop.Token,
			maximumBufferedBytes: 64
		);

		try {
			TerminalEvent terminalEvent = (
				await coordinator.ReadAsync().AsTask().WaitAsync(
					TimeSpan.FromSeconds( 5 )
				)
			).ToTerminalEvent();

			Assert.Equal( TerminalEventKind.Input, terminalEvent.Kind );
			Assert.Equal( new Rune( 'x' ), terminalEvent.Input?.Character );
		} finally {
			stop.Cancel();
		}
	}

	[Fact]
	public async Task BoundedCoordinatorBackpressuresSemanticBurst() {
		ChannelTerminalInput input = new();
		using CancellationTokenSource stop = new();
		TerminalInputCoordinator coordinator = CreateCoordinator(
			input,
			stop.Token,
			deferredEventCapacity: 2
		);
		using IDisposable queryDemand = coordinator.AcquireQueryDemand();

		for ( int index = 1; index <= 4; ++index ) {
			input.Publish(
				Encoding.ASCII.GetBytes(
					$"\u001b]99;i=burst-{index};\u001b\\"
				)
			);
		}

		try {
			await input.WaitForReadCountAsync( 3 );
			Assert.Equal( 3, input.ReadCount );

			TerminalApplicationEvent first = await coordinator.ReadAsync().AsTask().WaitAsync(
				TimeSpan.FromSeconds( 5 )
			);
			Assert.Equal( "burst-1", GetIdentifier( first ) );

			await input.WaitForReadCountAsync( 4 );
			Assert.Equal( 4, input.ReadCount );
			queryDemand.Dispose();

			TerminalApplicationEvent second = await coordinator.ReadAsync().AsTask().WaitAsync(
				TimeSpan.FromSeconds( 5 )
			);
			TerminalApplicationEvent third = await coordinator.ReadAsync().AsTask().WaitAsync(
				TimeSpan.FromSeconds( 5 )
			);
			TerminalApplicationEvent fourth = await coordinator.ReadAsync().AsTask().WaitAsync(
				TimeSpan.FromSeconds( 5 )
			);

			Assert.Equal( "burst-2", GetIdentifier( second ) );
			Assert.Equal( "burst-3", GetIdentifier( third ) );
			Assert.Equal( "burst-4", GetIdentifier( fourth ) );
			Assert.Equal( 4, input.ReadCount );
		} finally {
			stop.Cancel();
		}
	}

	private static TerminalInputDecoder CreateDecoder(
		ITerminalInput input,
		int maximumBufferedBytes = TerminalResponseFramer.DefaultMaximumFrameBytes
	) {
		ArgumentNullException.ThrowIfNull( input );
		return new TerminalInputDecoder(
			input,
			new TerminalDescriptionBuilder( "e198-semantic-adversarial" ).Build(),
			SystemMonotonicClock.Instance,
			TimeSpan.FromSeconds( 1 ),
			maximumBufferedBytes
		);
	}

	private static TerminalInputCoordinator CreateCoordinator(
		ITerminalInput input,
		CancellationToken stopToken,
		int maximumBufferedBytes = TerminalResponseFramer.DefaultMaximumFrameBytes,
		int deferredEventCapacity = TerminalInputCoordinator.DefaultDeferredEventCapacity
	) {
		ArgumentNullException.ThrowIfNull( input );
		return new TerminalInputCoordinator(
			CreateDecoder(
				input,
				maximumBufferedBytes
			),
			stopToken,
			deferredEventCapacity
		);
	}

	private static async ValueTask<TerminalNotificationEvent> ReadNotificationAsync(
		TerminalInputDecoder decoder
	) {
		ArgumentNullException.ThrowIfNull( decoder );
		TerminalInputDecodeResult result = await decoder.ReadNextAsync();
		Assert.False( result.ResponseRouted );
		Assert.True( result.ApplicationEvent.HasValue );
		TerminalSemanticEvent semantic = Assert.IsType<TerminalSemanticEvent>(
			result.ApplicationEvent.Value.SemanticEvent
		);
		return Assert.IsType<TerminalNotificationEvent>( semantic.Notification );
	}

	private static string GetIdentifier(
		TerminalApplicationEvent applicationEvent
	) {
		TerminalSemanticEvent semantic = Assert.IsType<TerminalSemanticEvent>(
			applicationEvent.SemanticEvent
		);
		TerminalNotificationEvent notification = Assert.IsType<TerminalNotificationEvent>(
			semantic.Notification
		);
		return notification.Identifier;
	}

	private static byte[][] SplitEvery(
		byte[] bytes,
		int chunkSize
	) {
		ArgumentNullException.ThrowIfNull( bytes );
		if ( 1 > chunkSize ) {
			throw new ArgumentOutOfRangeException( nameof( chunkSize ) );
		}

		List<byte[]> chunks = [];
		for ( int offset = 0; offset < bytes.Length; offset += chunkSize ) {
			int count = Math.Min(
				chunkSize,
				bytes.Length - offset
			);
			chunks.Add( bytes.AsSpan( offset, count ).ToArray() );
		}
		return chunks.ToArray();
	}

	private sealed class SegmentedTerminalInput : ITerminalInput {
		private readonly byte[][] segments;
		private int segmentIndex;
		private int segmentOffset;

		internal SegmentedTerminalInput(
			params byte[][] segments
		) {
			ArgumentNullException.ThrowIfNull( segments );
			this.segments = segments
				.Select( static segment => segment.ToArray() )
				.ToArray();
		}

		public ValueTask<int> ReadAsync(
			Memory<byte> buffer,
			CancellationToken cancellationToken = default
		) {
			cancellationToken.ThrowIfCancellationRequested();
			if ( buffer.IsEmpty ) {
				return ValueTask.FromResult( 0 );
			}

			while ( this.segmentIndex < this.segments.Length ) {
				byte[] segment = this.segments[ this.segmentIndex ];
				int remaining = segment.Length - this.segmentOffset;
				if ( 0 == remaining ) {
					++this.segmentIndex;
					this.segmentOffset = 0;
					continue;
				}

				int count = Math.Min( buffer.Length, remaining );
				segment.AsSpan(
					this.segmentOffset,
					count
				).CopyTo( buffer.Span );
				this.segmentOffset += count;
				return ValueTask.FromResult( count );
			}

			return ValueTask.FromResult( 0 );
		}
	}

	private sealed class ChannelTerminalInput : ITerminalInput {
		private readonly Channel<byte[]> input = Channel.CreateUnbounded<byte[]>(
			new UnboundedChannelOptions {
				SingleReader = true,
				SingleWriter = true,
				AllowSynchronousContinuations = false
			}
		);
		private readonly SemaphoreSlim readSignal = new( 0 );
		private int readCount;

		internal int ReadCount {
			get {
				return Volatile.Read( ref this.readCount );
			}
		}

		internal void Publish(
			byte[] bytes
		) {
			ArgumentNullException.ThrowIfNull( bytes );
			if ( !this.input.Writer.TryWrite( bytes.ToArray() ) ) {
				throw new InvalidOperationException(
					"The scripted semantic-input channel is closed."
				);
			}
		}

		internal async ValueTask WaitForReadCountAsync(
			int expected
		) {
			if ( 0 > expected ) {
				throw new ArgumentOutOfRangeException( nameof( expected ) );
			}

			using CancellationTokenSource timeout = new();
			timeout.CancelAfter( TimeSpan.FromSeconds( 5 ) );
			while ( expected > this.ReadCount ) {
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
			byte[] bytes = await this.input.Reader.ReadAsync(
				cancellationToken
			).ConfigureAwait( false );
			if ( bytes.Length > buffer.Length ) {
				throw new InvalidOperationException(
					"The scripted semantic-input chunk exceeds the decoder read buffer."
				);
			}
			bytes.AsSpan().CopyTo( buffer.Span );
			return bytes.Length;
		}
	}
}
