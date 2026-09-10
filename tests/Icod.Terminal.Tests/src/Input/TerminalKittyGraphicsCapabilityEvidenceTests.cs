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
/// Verifies A185 Kitty Graphics capability evidence and Primary DA barrier semantics.
/// </summary>
public sealed class TerminalKittyGraphicsCapabilityEvidenceTests {
	private const uint ProbeImageId = 31;
	private const string ExpectedProbeRequest =
		"\u001b_Gi=31,s=1,v=1,a=q,t=d,f=24;AAAA\u001b\\\u001b[c";

	[Fact]
	public async Task OkResponseBeforePrimaryDaVerifiesKittyGraphics() {
		ProbeTransport transport = new();
		ManualMonotonicClock clock = new();
		await using TerminalSession session = await OpenSessionAsync(
			transport,
			clock
		);

		Task<bool> probe = session.ProbeKittyGraphicsSupportAsync(
			ProbeImageId
		).AsTask();
		await transport.WaitForRequestAsync();
		Assert.Equal(
			Encoding.ASCII.GetBytes( ExpectedProbeRequest ),
			transport.GetRequest()
		);

		transport.Publish(
			Encoding.ASCII.GetBytes( "\u001b_Gi=31;OK\u001b\\" )
		);
		transport.Publish(
			Encoding.ASCII.GetBytes( "\u001b[?64;1;2c" )
		);

		Assert.True( await probe );
		TerminalCapabilityResolution evidence = ResolveKittyEvidence( session );
		Assert.Equal( TerminalCapabilitySupportState.Verified, evidence.State );
		Assert.Equal(
			TerminalCapabilityEvidenceSource.ProtocolResponse,
			evidence.EvidenceSource
		);
		TerminalSemanticBackendResolution routing = session.ResolveSemanticBackend(
			TerminalSemanticOperation.RasterGraphics
		);
		Assert.NotNull( routing.SelectedCandidate );
		Assert.Equal(
			TerminalProtocolBackend.ApcKittyGraphics,
			routing.SelectedCandidate.Value.Backend
		);
		Assert.Equal( TerminalBackendSelectionReason.Verified, routing.SelectionReason );
	}

	[Fact]
	public async Task ProtocolErrorResponseStillVerifiesKittyDialect() {
		ProbeTransport transport = new();
		ManualMonotonicClock clock = new();
		await using TerminalSession session = await OpenSessionAsync(
			transport,
			clock
		);

		Task<bool> probe = session.ProbeKittyGraphicsSupportAsync(
			ProbeImageId
		).AsTask();
		await transport.WaitForRequestAsync();
		transport.Publish(
			Encoding.ASCII.GetBytes(
				"\u001b_Gi=31;EINVAL:synthetic probe error\u001b\\"
			)
		);
		transport.Publish(
			Encoding.ASCII.GetBytes( "\u001b[?64;1c" )
		);

		Assert.True( await probe );
		TerminalCapabilityResolution evidence = ResolveKittyEvidence( session );
		Assert.Equal( TerminalCapabilitySupportState.Verified, evidence.State );
		Assert.Equal(
			TerminalCapabilityEvidenceSource.ProtocolResponse,
			evidence.EvidenceSource
		);
	}

	[Fact]
	public async Task PrimaryDaFirstMarksKittyUnsupportedAndCanVerifySixel() {
		ProbeTransport transport = new();
		ManualMonotonicClock clock = new();
		await using TerminalSession session = await OpenSessionAsync(
			transport,
			clock
		);

		Task<bool> probe = session.ProbeKittyGraphicsSupportAsync(
			ProbeImageId
		).AsTask();
		await transport.WaitForRequestAsync();
		transport.Publish(
			Encoding.ASCII.GetBytes( "\u001b[?64;4c" )
		);

		Assert.False( await probe );
		TerminalCapabilityResolution kitty = ResolveKittyEvidence( session );
		Assert.Equal( TerminalCapabilitySupportState.Unsupported, kitty.State );
		Assert.Equal(
			TerminalCapabilityEvidenceSource.ProtocolResponse,
			kitty.EvidenceSource
		);

		TerminalCapabilityResolution sixel = ResolveSixelEvidence( session );
		Assert.Equal( TerminalCapabilitySupportState.Verified, sixel.State );
		Assert.Equal(
			TerminalCapabilityEvidenceSource.ProtocolResponse,
			sixel.EvidenceSource
		);

		TerminalSemanticBackendResolution routing = session.ResolveSemanticBackend(
			TerminalSemanticOperation.RasterGraphics
		);
		Assert.NotNull( routing.SelectedCandidate );
		Assert.Equal(
			TerminalProtocolBackend.DcsSixel,
			routing.SelectedCandidate.Value.Backend
		);
	}

	[Fact]
	public async Task TimeoutWithoutGraphicsResponseRemainsUnknown() {
		ProbeTransport transport = new();
		ManualMonotonicClock clock = new();
		await using TerminalSession session = await OpenSessionAsync(
			transport,
			clock
		);

		Task<bool> probe = session.ProbeKittyGraphicsSupportAsync(
			ProbeImageId
		).AsTask();
		await transport.WaitForRequestAsync();
		clock.Advance( TimeSpan.FromSeconds( 1 ) );

		Assert.False( await probe );
		TerminalCapabilityResolution evidence = ResolveKittyEvidence( session );
		Assert.Equal( TerminalCapabilitySupportState.Unknown, evidence.State );
		Assert.Equal(
			TerminalCapabilityEvidenceSource.LiveProbe,
			evidence.EvidenceSource
		);
	}

	[Fact]
	public async Task GraphicsResponseWithoutPrimaryDaStillVerifiesAtTimeout() {
		ProbeTransport transport = new();
		ManualMonotonicClock clock = new();
		await using TerminalSession session = await OpenSessionAsync(
			transport,
			clock
		);

		Task<bool> probe = session.ProbeKittyGraphicsSupportAsync(
			ProbeImageId
		).AsTask();
		await transport.WaitForRequestAsync();
		transport.Publish(
			Encoding.ASCII.GetBytes( "\u001b_Gi=31;OK\u001b\\" )
		);
		await transport.WaitForReadCountAsync( 2 );
		clock.Advance( TimeSpan.FromSeconds( 1 ) );

		Assert.True( await probe );
		TerminalCapabilityResolution evidence = ResolveKittyEvidence( session );
		Assert.Equal( TerminalCapabilitySupportState.Verified, evidence.State );
		Assert.Equal(
			TerminalCapabilityEvidenceSource.ProtocolResponse,
			evidence.EvidenceSource
		);
	}

	[Fact]
	public async Task MalformedCorrelatedResponseFailsWithoutKittyEvidence() {
		ProbeTransport transport = new();
		ManualMonotonicClock clock = new();
		await using TerminalSession session = await OpenSessionAsync(
			transport,
			clock
		);

		Task<bool> probe = session.ProbeKittyGraphicsSupportAsync(
			ProbeImageId
		).AsTask();
		await transport.WaitForRequestAsync();
		transport.Publish(
			Encoding.ASCII.GetBytes( "\u001b_Gi=31,i=32;OK\u001b\\" )
		);
		transport.Publish(
			Encoding.ASCII.GetBytes( "\u001b[?64;1c" )
		);

		await Assert.ThrowsAsync<FormatException>( () => probe );
		TerminalCapabilityResolution evidence = ResolveKittyEvidence( session );
		Assert.Equal( TerminalCapabilitySupportState.Unknown, evidence.State );
		Assert.Null( evidence.EvidenceSource );
	}

	[Fact]
	public async Task CallerCancellationDoesNotFabricateKittyEvidence() {
		ProbeTransport transport = new();
		ManualMonotonicClock clock = new();
		await using TerminalSession session = await OpenSessionAsync(
			transport,
			clock
		);
		using CancellationTokenSource cancellation = new();

		Task<bool> probe = session.ProbeKittyGraphicsSupportAsync(
			ProbeImageId,
			cancellation.Token
		).AsTask();
		await transport.WaitForRequestAsync();
		cancellation.Cancel();

		await Assert.ThrowsAnyAsync<OperationCanceledException>( () => probe );
		TerminalCapabilityResolution evidence = ResolveKittyEvidence( session );
		Assert.Equal( TerminalCapabilitySupportState.Unknown, evidence.State );
		Assert.Null( evidence.EvidenceSource );
	}

	[Fact]
	public async Task VerifiedKittyEvidenceExpiresWithLiveGeneration() {
		ProbeTransport transport = new();
		ManualMonotonicClock clock = new();
		await using TerminalSession session = await OpenSessionAsync(
			transport,
			clock
		);

		Task<bool> probe = session.ProbeKittyGraphicsSupportAsync(
			ProbeImageId
		).AsTask();
		await transport.WaitForRequestAsync();
		transport.Publish(
			Encoding.ASCII.GetBytes( "\u001b_Gi=31;OK\u001b\\" )
		);
		transport.Publish(
			Encoding.ASCII.GetBytes( "\u001b[?64;1c" )
		);
		Assert.True( await probe );
		Assert.Equal(
			TerminalCapabilitySupportState.Verified,
			ResolveKittyEvidence( session ).State
		);

		session.AdvanceSemanticLiveEvidenceGeneration();
		TerminalCapabilityResolution evidence = ResolveKittyEvidence( session );
		Assert.Equal( TerminalCapabilitySupportState.Unknown, evidence.State );
		Assert.Null( evidence.EvidenceSource );
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

	private static TerminalCapabilityResolution ResolveSixelEvidence(
		TerminalSession session
	) {
		ArgumentNullException.ThrowIfNull( session );
		return session.GetSemanticCapabilityEvidence().Resolve(
			TerminalCapabilitySubject.ForProtocolBackend(
				TerminalProtocolBackend.DcsSixel
			)
		);
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
					"kitty-graphics-evidence-test"
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
		private readonly object sync = new();
		private readonly TaskCompletionSource requestObserved = new(
			TaskCreationOptions.RunContinuationsAsynchronously
		);
		private readonly SemaphoreSlim readSignal = new( 0 );
		private byte[]? request;
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
				if ( this.request is not null ) {
					throw new InvalidOperationException(
						"The scripted Kitty Graphics probe expected exactly one request write."
					);
				}
				this.request = buffer.ToArray();
			}
			this.requestObserved.TrySetResult();
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

		internal Task WaitForRequestAsync() {
			return this.requestObserved.Task;
		}

		internal byte[] GetRequest() {
			lock ( this.sync ) {
				return this.request?.ToArray()
					?? throw new InvalidOperationException(
						"The scripted terminal request has not been observed."
					);
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
				"No scripted live size."
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
