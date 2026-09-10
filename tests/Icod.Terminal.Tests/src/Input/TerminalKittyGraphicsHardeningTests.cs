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
using Icod.Timing;
using Xunit;

/// <summary>
/// Verifies A188 adversarial framing, fragmentation, and late-response behavior
/// for the Kitty Graphics support probe.
/// </summary>
public sealed class TerminalKittyGraphicsHardeningTests {
	private const uint ProbeImageId = 31;
	private static readonly byte[] PrimaryDeviceAttributesResponse =
		Encoding.ASCII.GetBytes( "\u001b[?64;1c" );

	[Fact]
	public void PrefixCorrelationRequiresCompleteMatchingImageIdField() {
		Assert.False(
			KittyGraphicsCapabilityProtocol.IsCorrelatedResponsePrefix(
				Encoding.ASCII.GetBytes( "\u001b_Gi=31" ),
				ProbeImageId
			)
		);
		Assert.True(
			KittyGraphicsCapabilityProtocol.IsCorrelatedResponsePrefix(
				Encoding.ASCII.GetBytes( "\u001b_Gi=31;" ),
				ProbeImageId
			)
		);
		Assert.False(
			KittyGraphicsCapabilityProtocol.IsCorrelatedResponsePrefix(
				Encoding.ASCII.GetBytes( "\u001b_Gi=32;" ),
				ProbeImageId
			)
		);
		Assert.True(
			KittyGraphicsCapabilityProtocol.IsCorrelatedResponsePrefix(
				[
					0x9F,
					(byte)'G',
					(byte)'i',
					(byte)'=',
					(byte)'3',
					(byte)'1',
					(byte)';'
				],
				ProbeImageId
			)
		);
	}

	[Fact]
	public async Task SevenBitResponseSucceedsAtEverySplitPoint() {
		byte[] response = Encoding.ASCII.GetBytes( "\u001b_Gi=31;OK\u001b\\" );
		for ( int split = 1; split < response.Length; split++ ) {
			ProbeTransport transport = new();
			await using TerminalSession session = await OpenSessionAsync(
				transport,
				new NonAdvancingMonotonicClock()
			);

			Task<bool> probe = session.ProbeKittyGraphicsSupportAsync(
				ProbeImageId
			).AsTask();
			await transport.WaitForWriteCountAsync( 1 );
			transport.Publish( response[..split] );
			transport.Publish( response[split..] );
			transport.Publish( PrimaryDeviceAttributesResponse );

			Assert.True( await probe );
		}
	}

	[Fact]
	public async Task EightBitResponseSucceedsAtEverySplitPoint() {
		byte[] payload = Encoding.ASCII.GetBytes( "Gi=31;OK" );
		byte[] response = new byte[ payload.Length + 2 ];
		response[ 0 ] = 0x9F;
		payload.CopyTo(
			response,
			1
		);
		response[ ^1 ] = 0x9C;

		for ( int split = 1; split < response.Length; split++ ) {
			ProbeTransport transport = new();
			await using TerminalSession session = await OpenSessionAsync(
				transport,
				new NonAdvancingMonotonicClock()
			);

			Task<bool> probe = session.ProbeKittyGraphicsSupportAsync(
				ProbeImageId
			).AsTask();
			await transport.WaitForWriteCountAsync( 1 );
			transport.Publish( response[..split] );
			transport.Publish( response[split..] );
			transport.Publish( PrimaryDeviceAttributesResponse );

			Assert.True( await probe );
		}
	}

	[Theory]
	[InlineData( 0x18 )]
	[InlineData( 0x1A )]
	public async Task CorrelatedCanOrSubAbortFailsProbeWithoutLeakingIntoBarrier(
		int abortValue
	) {
		ProbeTransport transport = new();
		await using TerminalSession session = await OpenSessionAsync(
			transport,
			new NonAdvancingMonotonicClock()
		);
		Task<bool> probe = session.ProbeKittyGraphicsSupportAsync(
			ProbeImageId
		).AsTask();
		await transport.WaitForWriteCountAsync( 1 );

		byte[] prefix = Encoding.ASCII.GetBytes( "\u001b_Gi=31;OK" );
		byte[] aborted = new byte[ prefix.Length + 1 ];
		prefix.CopyTo(
			aborted,
			0
		);
		aborted[ ^1 ] = checked( (byte)abortValue );
		transport.Publish( aborted );
		transport.Publish( PrimaryDeviceAttributesResponse );

		await Assert.ThrowsAsync<FormatException>( () => probe );
		AssertUnknownKittyEvidence( session );
	}

	[Fact]
	public async Task CorrelatedBadEscapeTerminationFailsProbe() {
		ProbeTransport transport = new();
		await using TerminalSession session = await OpenSessionAsync(
			transport,
			new NonAdvancingMonotonicClock()
		);
		Task<bool> probe = session.ProbeKittyGraphicsSupportAsync(
			ProbeImageId
		).AsTask();
		await transport.WaitForWriteCountAsync( 1 );

		transport.Publish(
			Encoding.ASCII.GetBytes( "\u001b_Gi=31;OK\u001bX" )
		);
		transport.Publish( PrimaryDeviceAttributesResponse );

		await Assert.ThrowsAsync<FormatException>( () => probe );
		AssertUnknownKittyEvidence( session );
	}

	[Fact]
	public async Task OversizedCorrelatedResponseIsDrainedThroughStBeforeBarrier() {
		ProbeTransport transport = new();
		await using TerminalSession session = await OpenSessionAsync(
			transport,
			new NonAdvancingMonotonicClock()
		);
		Task<bool> probe = session.ProbeKittyGraphicsSupportAsync(
			ProbeImageId
		).AsTask();
		await transport.WaitForWriteCountAsync( 1 );

		byte[] prefix = Encoding.ASCII.GetBytes( "\u001b_Gi=31;" );
		byte[] oversized = new byte[ 5000 ];
		prefix.CopyTo(
			oversized,
			0
		);
		Array.Fill(
			oversized,
			(byte)'E',
			prefix.Length,
			oversized.Length - prefix.Length - 2
		);
		oversized[ ^2 ] = 0x1B;
		oversized[ ^1 ] = (byte)'\\';
		PublishInChunks(
			transport,
			oversized,
			512
		);
		transport.Publish( PrimaryDeviceAttributesResponse );

		await Assert.ThrowsAsync<FormatException>( () => probe );
		AssertUnknownKittyEvidence( session );
	}

	[Fact]
	public async Task UnterminatedCorrelatedResponseFailsAtLogicalProbeDeadline() {
		ProbeTransport transport = new();
		ManualMonotonicClock clock = new();
		await using TerminalSession session = await OpenSessionAsync(
			transport,
			clock
		);
		Task<bool> probe = session.ProbeKittyGraphicsSupportAsync(
			ProbeImageId
		).AsTask();
		await transport.WaitForWriteCountAsync( 1 );

		transport.Publish(
			Encoding.ASCII.GetBytes( "\u001b_Gi=31;OK" )
		);
		await transport.WaitForReadCountAsync( 2 );
		clock.Advance( TimeSpan.FromSeconds( 1 ) );

		await Assert.ThrowsAsync<FormatException>( () => probe );
		AssertUnknownKittyEvidence( session );
	}

	[Fact]
	public async Task UnrelatedControlTrafficDoesNotSatisfyOrBreakActiveProbe() {
		ProbeTransport transport = new();
		await using TerminalSession session = await OpenSessionAsync(
			transport,
			new NonAdvancingMonotonicClock()
		);
		Task<bool> probe = session.ProbeKittyGraphicsSupportAsync(
			ProbeImageId
		).AsTask();
		await transport.WaitForWriteCountAsync( 1 );

		transport.Publish( Encoding.ASCII.GetBytes( "\u001b_Gi=99;OK\u001b\\" ) );
		transport.Publish( Encoding.ASCII.GetBytes( "\u001b]0;unrelated\u0007" ) );
		transport.Publish( Encoding.ASCII.GetBytes( "\u001bP$qm\u001b\\" ) );
		transport.Publish( Encoding.ASCII.GetBytes( "\u001b[5n" ) );
		transport.Publish( Encoding.ASCII.GetBytes( "\u001b_Gi=31;OK\u001b\\" ) );
		transport.Publish( PrimaryDeviceAttributesResponse );

		Assert.True( await probe );
	}

	[Fact]
	public async Task LateKittyResponseAfterDaBarrierDoesNotCorruptNextCsiQuery() {
		ProbeTransport transport = new();
		await using TerminalSession session = await OpenSessionAsync(
			transport,
			new NonAdvancingMonotonicClock()
		);
		Task<bool> probe = session.ProbeKittyGraphicsSupportAsync(
			ProbeImageId
		).AsTask();
		await transport.WaitForWriteCountAsync( 1 );
		transport.Publish( PrimaryDeviceAttributesResponse );
		Assert.False( await probe );

		transport.Publish( Encoding.ASCII.GetBytes( "\u001b_Gi=31;OK\u001b\\" ) );
		Task<TerminalPrimaryDeviceAttributes> next = session.QueryPrimaryDeviceAttributesAsync(
			TimeSpan.FromSeconds( 30 )
		).AsTask();
		await transport.WaitForWriteCountAsync( 2 );
		transport.Publish( Encoding.ASCII.GetBytes( "\u001b[?64;4c" ) );

		TerminalPrimaryDeviceAttributes attributes = await next;
		Assert.True( attributes.HasAttribute( 4 ) );
		TerminalCapabilityResolution kitty = ResolveKittyEvidence( session );
		Assert.Equal( TerminalCapabilitySupportState.Unsupported, kitty.State );
		Assert.Equal(
			TerminalCapabilityEvidenceSource.ProtocolResponse,
			kitty.EvidenceSource
		);
	}

	[Fact]
	public async Task RepeatedLiveEvidenceGenerationsExpireKittyEvidenceDeterministically() {
		ProbeTransport transport = new();
		await using TerminalSession session = await OpenSessionAsync(
			transport,
			new NonAdvancingMonotonicClock()
		);

		for ( int generation = 0; generation < 8; generation++ ) {
			session.RecordSemanticBackendEvidence(
				TerminalProtocolBackend.ApcKittyGraphics,
				TerminalCapabilitySupportState.Verified,
				TerminalCapabilityEvidenceSource.ProtocolResponse
			);
			Assert.Equal(
				TerminalCapabilitySupportState.Verified,
				ResolveKittyEvidence( session ).State
			);
			session.AdvanceSemanticLiveEvidenceGeneration();
			Assert.Equal(
				TerminalCapabilitySupportState.Unknown,
				ResolveKittyEvidence( session ).State
			);
		}
	}

	private static void PublishInChunks(
		ProbeTransport transport,
		byte[] bytes,
		int chunkBytes
	) {
		ArgumentNullException.ThrowIfNull( transport );
		ArgumentNullException.ThrowIfNull( bytes );
		if ( 1 > chunkBytes ) {
			throw new ArgumentOutOfRangeException( nameof( chunkBytes ) );
		}

		for ( int offset = 0; offset < bytes.Length; offset += chunkBytes ) {
			int count = Math.Min(
				chunkBytes,
				bytes.Length - offset
			);
			transport.Publish(
				bytes.AsSpan(
					offset,
					count
				).ToArray()
			);
		}
	}

	private static TerminalCapabilityResolution ResolveKittyEvidence(
		TerminalSession session
	) {
		ArgumentNullException.ThrowIfNull( session );
		return session.GetSemanticCapabilityEvidence().Resolve(
			TerminalCapabilitySubject.ForProtocolBackend(
				TerminalProtocolBackend.ApcKittyGraphics
			)
		);
	}

	private static void AssertUnknownKittyEvidence(
		TerminalSession session
	) {
		ArgumentNullException.ThrowIfNull( session );
		TerminalCapabilityResolution evidence = ResolveKittyEvidence( session );
		Assert.Equal( TerminalCapabilitySupportState.Unknown, evidence.State );
		Assert.Null( evidence.EvidenceSource );
	}

	private static ValueTask<TerminalSession> OpenSessionAsync(
		ProbeTransport transport,
		IMonotonicClock monotonicClock
	) {
		ArgumentNullException.ThrowIfNull( transport );
		ArgumentNullException.ThrowIfNull( monotonicClock );
		return TerminalSession.OpenAsync(
			new RecordingTerminalControlProvider(),
			TerminalEndpoint.StandardInput,
			TerminalEndpoint.StandardOutput,
			transport,
			transport,
			new TerminalSessionOptions {
				TerminalOverride = new TerminalDescriptionBuilder(
					"kitty-graphics-hardening-test"
				).Build(),
				ConfigureOutput = false,
				ObserveLifecycleEvents = false,
				MonotonicClock = monotonicClock
			}
		);
	}

	private sealed class ProbeTransport : ITerminalInput, ITerminalOutput {
		private readonly Channel<byte[]> input = Channel.CreateUnbounded<byte[]>(
			new UnboundedChannelOptions {
				SingleReader = true,
				SingleWriter = false,
				AllowSynchronousContinuations = false
			}
		);
		private readonly SemaphoreSlim writeSignal = new( 0 );
		private readonly SemaphoreSlim readSignal = new( 0 );
		private readonly object sync = new();
		private readonly List<byte[]> writes = [];
		private int readCount;

		public async ValueTask<int> ReadAsync(
			Memory<byte> buffer,
			CancellationToken cancellationToken = default
		) {
			Interlocked.Increment( ref this.readCount );
			this.readSignal.Release();
			byte[] value = await this.input.Reader.ReadAsync(
				cancellationToken
			).ConfigureAwait( false );
			if ( value.Length > buffer.Length ) {
				throw new InvalidOperationException(
					"The scripted terminal response exceeds the decoder read buffer."
				);
			}
			value.AsSpan().CopyTo( buffer.Span );
			return value.Length;
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

		internal async Task WaitForWriteCountAsync(
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

		internal async Task WaitForReadCountAsync(
			int expected
		) {
			if ( 0 > expected ) {
				throw new ArgumentOutOfRangeException( nameof( expected ) );
			}
			using CancellationTokenSource timeout = new();
			timeout.CancelAfter( TimeSpan.FromSeconds( 5 ) );
			while ( Volatile.Read( ref this.readCount ) < expected ) {
				await this.readSignal.WaitAsync(
					timeout.Token
				).ConfigureAwait( false );
			}
		}
	}

	private sealed class NonAdvancingMonotonicClock : IMonotonicClock {
		public long GetTimestamp() {
			return 0;
		}

		public TimeSpan GetElapsedTime(
			long startingTimestamp,
			long endingTimestamp
		) {
			return TimeSpan.Zero;
		}

		public ValueTask DelayAsync(
			TimeSpan delay,
			CancellationToken cancellationToken = default
		) {
			if ( TimeSpan.Zero > delay ) {
				throw new ArgumentOutOfRangeException( nameof( delay ) );
			}
			cancellationToken.ThrowIfCancellationRequested();
			return TimeSpan.Zero == delay
				? ValueTask.CompletedTask
				: new ValueTask(
					Task.Delay(
						Timeout.InfiniteTimeSpan,
						cancellationToken
					)
				)
			;
		}
	}

	private sealed class ManualMonotonicClock : IMonotonicClock {
		private readonly object sync = new();
		private readonly List<DelayWaiter> waiters = [];
		private long timestamp;

		public long GetTimestamp() {
			lock ( this.sync ) {
				return this.timestamp;
			}
		}

		public TimeSpan GetElapsedTime(
			long startingTimestamp,
			long endingTimestamp
		) {
			return TimeSpan.FromTicks(
				endingTimestamp - startingTimestamp
			);
		}

		public ValueTask DelayAsync(
			TimeSpan delay,
			CancellationToken cancellationToken = default
		) {
			if ( TimeSpan.Zero > delay ) {
				throw new ArgumentOutOfRangeException( nameof( delay ) );
			}
			cancellationToken.ThrowIfCancellationRequested();
			if ( TimeSpan.Zero == delay ) {
				return ValueTask.CompletedTask;
			}
			return new ValueTask(
				this.DelayCoreAsync(
					delay,
					cancellationToken
				)
			);
		}

		internal void Advance(
			TimeSpan elapsed
		) {
			if ( TimeSpan.Zero > elapsed ) {
				throw new ArgumentOutOfRangeException( nameof( elapsed ) );
			}

			List<DelayWaiter> due;
			lock ( this.sync ) {
				this.timestamp = checked(
					this.timestamp + elapsed.Ticks
				);
				due = this.waiters
					.Where( waiter => waiter.DueTimestamp <= this.timestamp )
					.ToList();
			}
			foreach ( DelayWaiter waiter in due ) {
				waiter.Completion.TrySetResult();
			}
		}

		private async Task DelayCoreAsync(
			TimeSpan delay,
			CancellationToken cancellationToken
		) {
			DelayWaiter waiter;
			lock ( this.sync ) {
				waiter = new DelayWaiter(
					checked( this.timestamp + delay.Ticks )
				);
				this.waiters.Add( waiter );
			}

			using CancellationTokenRegistration registration = cancellationToken.Register(
				static state => {
					var tuple = (Tuple<TaskCompletionSource, CancellationToken>)state!;
					tuple.Item1.TrySetCanceled( tuple.Item2 );
				},
				Tuple.Create(
					waiter.Completion,
					cancellationToken
				)
			);
			try {
				await waiter.Completion.Task.ConfigureAwait( false );
			} finally {
				lock ( this.sync ) {
					this.waiters.Remove( waiter );
				}
			}
		}

		private sealed class DelayWaiter {
			internal DelayWaiter(
				long dueTimestamp
			) {
				this.DueTimestamp = dueTimestamp;
			}

			internal long DueTimestamp {
				get;
			}

			internal TaskCompletionSource Completion {
				get;
			} = new(
				TaskCreationOptions.RunContinuationsAsynchronously
			);
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
			return TerminalControlResult<TerminalSize>.Unavailable(
				"Live size is not required by this test."
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
