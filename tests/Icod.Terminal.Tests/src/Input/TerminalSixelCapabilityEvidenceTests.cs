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
/// Verifies D177 Sixel capability evidence derived from Primary Device Attributes.
/// </summary>
public sealed class TerminalSixelCapabilityEvidenceTests {
	private const string PrimaryDeviceAttributesRequest = "\u001b[c";

	[Fact]
	public async Task PrimaryDaAttributeFourVerifiesSixelAndEnablesRasterRouting() {
		ProbeTransport transport = new( "\u001b[?64;4c" );
		ManualMonotonicClock clock = new();
		await using TerminalSession session = await OpenSessionAsync(
			transport,
			clock
		);

		TerminalPrimaryDeviceAttributes attributes =
			await session.QueryPrimaryDeviceAttributesAsync(
				TimeSpan.FromSeconds( 30 )
			);
		TerminalCapabilityResolution evidence = ResolveSixelEvidence( session );
		TerminalSemanticBackendResolution routing = session.ResolveSemanticBackend(
			TerminalSemanticOperation.RasterGraphics
		);

		Assert.True( attributes.HasAttribute( 4 ) );
		Assert.Equal( TerminalCapabilitySupportState.Verified, evidence.State );
		Assert.Equal(
			TerminalCapabilityEvidenceSource.ProtocolResponse,
			evidence.EvidenceSource
		);
		Assert.NotNull( routing.SelectedCandidate );
		Assert.Equal(
			TerminalProtocolBackend.DcsSixel,
			routing.SelectedCandidate.Value.Backend
		);
		Assert.Equal( TerminalBackendSelectionReason.Verified, routing.SelectionReason );
	}

	[Fact]
	public async Task PrimaryDaWithoutAttributeFourRemainsUnknown() {
		ProbeTransport transport = new( "\u001b[?64;1;2c" );
		ManualMonotonicClock clock = new();
		await using TerminalSession session = await OpenSessionAsync(
			transport,
			clock
		);

		TerminalPrimaryDeviceAttributes attributes =
			await session.QueryPrimaryDeviceAttributesAsync(
				TimeSpan.FromSeconds( 30 )
			);
		TerminalCapabilityResolution evidence = ResolveSixelEvidence( session );
		TerminalSemanticBackendResolution routing = session.ResolveSemanticBackend(
			TerminalSemanticOperation.RasterGraphics
		);

		Assert.False( attributes.HasAttribute( 4 ) );
		Assert.Equal( TerminalCapabilitySupportState.Unknown, evidence.State );
		Assert.Equal(
			TerminalCapabilityEvidenceSource.ProtocolResponse,
			evidence.EvidenceSource
		);
		Assert.Null( routing.SelectedCandidate );
		Assert.Equal( TerminalCapabilitySupportState.Unknown, routing.State );
		Assert.Equal( TerminalBackendSelectionReason.None, routing.SelectionReason );
	}

	[Fact]
	public async Task ProbeTimeoutRemainsUnknownAndDoesNotBecomeUnsupported() {
		ProbeTransport transport = new( response: null );
		ManualMonotonicClock clock = new();
		await using TerminalSession session = await OpenSessionAsync(
			transport,
			clock
		);

		Task<bool> probe = session.ProbeSixelSupportAsync().AsTask();
		await transport.WaitForPrimaryDeviceAttributesRequestAsync();
		clock.Advance( TimeSpan.FromSeconds( 1 ) );

		Assert.False( await probe );
		TerminalCapabilityResolution evidence = ResolveSixelEvidence( session );
		Assert.Equal( TerminalCapabilitySupportState.Unknown, evidence.State );
		Assert.Equal(
			TerminalCapabilityEvidenceSource.LiveProbe,
			evidence.EvidenceSource
		);
	}

	[Fact]
	public async Task LiveGenerationAdvanceExpiresVerifiedSixelEvidence() {
		ProbeTransport transport = new( "\u001b[?64;4c" );
		ManualMonotonicClock clock = new();
		await using TerminalSession session = await OpenSessionAsync(
			transport,
			clock
		);

		_ = await session.QueryPrimaryDeviceAttributesAsync(
			TimeSpan.FromSeconds( 30 )
		);
		Assert.Equal(
			TerminalCapabilitySupportState.Verified,
			ResolveSixelEvidence( session ).State
		);

		session.AdvanceSemanticLiveEvidenceGeneration();
		TerminalCapabilityResolution evidence = ResolveSixelEvidence( session );
		Assert.Equal( TerminalCapabilitySupportState.Unknown, evidence.State );
		Assert.Null( evidence.EvidenceSource );
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
				TerminalOverride = new TerminalDescriptionBuilder( "sixel-evidence-test" ).Build(),
				ConfigureOutput = false,
				ObserveLifecycleEvents = false,
				MonotonicClock = monotonicClock
			}
		);
	}

	private sealed class ProbeTransport : ITerminalInput, ITerminalOutput {
		private readonly Channel<byte[]> input = Channel.CreateUnbounded<byte[]>();
		private readonly byte[]? response;
		private readonly TaskCompletionSource requestObserved = new(
			TaskCreationOptions.RunContinuationsAsynchronously
		);

		internal ProbeTransport(
			string? response
		) {
			this.response = response is null
				? null
				: Encoding.ASCII.GetBytes( response )
			;
		}

		public async ValueTask<int> ReadAsync(
			Memory<byte> buffer,
			CancellationToken cancellationToken = default
		) {
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
			if ( buffer.Span.SequenceEqual(
				Encoding.ASCII.GetBytes( PrimaryDeviceAttributesRequest )
			) ) {
				this.requestObserved.TrySetResult();
				if ( this.response is not null
					&& !this.input.Writer.TryWrite( this.response.ToArray() ) ) {
					throw new InvalidOperationException(
						"The scripted terminal input channel is closed."
					);
				}
			}
			return ValueTask.CompletedTask;
		}

		public ValueTask FlushAsync(
			CancellationToken cancellationToken = default
		) {
			cancellationToken.ThrowIfCancellationRequested();
			return ValueTask.CompletedTask;
		}

		internal Task WaitForPrimaryDeviceAttributesRequestAsync() {
			return this.requestObserved.Task;
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
