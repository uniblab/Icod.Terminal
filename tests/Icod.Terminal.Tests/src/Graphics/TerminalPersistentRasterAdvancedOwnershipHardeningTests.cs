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
namespace Icod.Terminal.Tests.Graphics;

using System.Text;
using System.Threading.Channels;
using Icod.Terminal;
using Icod.TermInfo;
using Icod.Timing;
using Xunit;

/// <summary>
/// Verifies T125 timeout ownership and repeated advanced-placement lifecycle hardening.
/// </summary>
public sealed class TerminalPersistentRasterAdvancedOwnershipHardeningTests {
	private const int OwnershipCycleCount = 24;

	[Fact]
	public async Task TimedOutAdvancedPlacementDoesNotAcceptLateResponseForLaterPlacement() {
		ManualMonotonicClock clock = new();
		AcknowledgingTransport transport = new() {
			AutoAcknowledgePlacements = false
		};
		await using TerminalSession session = await OpenSessionAsync(
			transport,
			clock
		);
		TerminalRasterResource resource = await CreateResourceAsync( session );
		await using ( resource ) {
			TerminalRasterPlacementOptions firstOptions = new() {
				SourceRectangle = new TerminalRasterSourceRectangle(
					0,
					0,
					1,
					1
				),
				Columns = 2,
				Rows = 1,
				ZIndex = -4
			};
			Task<TerminalControlResult<TerminalRasterPlacement>> first =
				resource.CreatePlacementAsync( firstOptions ).AsTask();
			await transport.WaitForWriteCountAsync( 2 );
			await YieldSeveralTimesAsync();
			clock.Advance( TimeSpan.FromSeconds( 2 ) );

			await Assert.ThrowsAsync<TimeoutException>( () => first );

			TerminalRasterPlacementOptions secondOptions = new() {
				SourceRectangle = new TerminalRasterSourceRectangle(
					0,
					0,
					1,
					1
				),
				Columns = 1,
				Rows = 2,
				ZIndex = 5
			};
			Task<TerminalControlResult<TerminalRasterPlacement>> second =
				resource.CreatePlacementAsync( secondOptions ).AsTask();
			await transport.WaitForWriteCountAsync( 3 );
			Assert.Equal(
				Encoding.ASCII.GetBytes(
					"\u001b_Ga=p,i=1001,p=2,C=1,x=0,y=0,w=1,h=1,c=1,r=2,z=5\u001b\\"
				),
				transport.Writes[ 2 ]
			);

			transport.Publish(
				Encoding.ASCII.GetBytes( "\u001b_Gi=1001,p=1;OK\u001b\\" )
			);
			await YieldSeveralTimesAsync();
			Assert.False( second.IsCompleted );

			transport.Publish(
				Encoding.ASCII.GetBytes( "\u001b_Gi=1001,p=2;OK\u001b\\" )
			);
			TerminalControlResult<TerminalRasterPlacement> result = await second;
			Assert.Equal( TerminalControlStatus.Available, result.Status );
			TerminalRasterPlacement placement = Assert.IsType<TerminalRasterPlacement>(
				result.Value
			);
			await placement.DisposeAsync();
		}
	}

	[Fact]
	public async Task RepeatedAdvancedOwnershipCyclesRemainUsableAndIdempotent() {
		ManualMonotonicClock clock = new();
		AcknowledgingTransport transport = new();
		await using TerminalSession session = await OpenSessionAsync(
			transport,
			clock
		);

		for ( int cycle = 0; cycle < OwnershipCycleCount; ++cycle ) {
			TerminalRasterResource resource = await CreateResourceAsync( session );
			TerminalControlResult<TerminalRasterPlacement> placementResult =
				await resource.CreatePlacementAsync(
					new TerminalRasterPlacementOptions {
						SourceRectangle = new TerminalRasterSourceRectangle(
							0,
							0,
							1,
							1
						),
						Columns = 3,
						Rows = 2,
						ZIndex = -cycle - 1
					}
				);
			Assert.Equal( TerminalControlStatus.Available, placementResult.Status );
			TerminalRasterPlacement placement = Assert.IsType<TerminalRasterPlacement>(
				placementResult.Value
			);

			TerminalControlMutationResult update = await placement.UpdateAsync(
				new TerminalRasterPlacementOptions {
					SourceRectangle = new TerminalRasterSourceRectangle(
						0,
						0,
						1,
						1
					),
					Columns = 2,
					Rows = 1,
					ZIndex = cycle + 1
				}
			);
			Assert.True( update.Succeeded );

			int cycleOffset = cycle * 5;
			string createText = Encoding.ASCII.GetString(
				transport.Writes[ cycleOffset + 1 ]
			);
			string updateText = Encoding.ASCII.GetString(
				transport.Writes[ cycleOffset + 2 ]
			);
			Assert.Contains( ",x=0,y=0,w=1,h=1,c=3,r=2,z=", createText );
			Assert.Contains( ",x=0,y=0,w=1,h=1,c=2,r=1,z=", updateText );

			await placement.DisposeAsync();
			int afterPlacementDispose = transport.Writes.Count;
			await placement.DisposeAsync();
			Assert.Equal( afterPlacementDispose, transport.Writes.Count );

			await resource.DisposeAsync();
			int afterResourceDispose = transport.Writes.Count;
			await resource.DisposeAsync();
			Assert.Equal( afterResourceDispose, transport.Writes.Count );
		}

		Assert.Equal(
			OwnershipCycleCount * 5,
			transport.Writes.Count
		);
	}

	private static TerminalRasterImage CreateSmallImage() {
		return TerminalRasterImage.CreateRgb24(
			1,
			1,
			[ 1, 2, 3 ]
		);
	}

	private static async Task<TerminalRasterResource> CreateResourceAsync(
		TerminalSession session
	) {
		ArgumentNullException.ThrowIfNull( session );
		TerminalControlResult<TerminalRasterResource> result =
			await session.CreateRasterResourceAsync( CreateSmallImage() );
		Assert.Equal( TerminalControlStatus.Available, result.Status );
		return Assert.IsType<TerminalRasterResource>( result.Value );
	}

	private static async ValueTask<TerminalSession> OpenSessionAsync(
		AcknowledgingTransport transport,
		IMonotonicClock clock
	) {
		ArgumentNullException.ThrowIfNull( transport );
		ArgumentNullException.ThrowIfNull( clock );
		TerminalSession session = await TerminalSession.OpenAsync(
			new RecordingTerminalControlProvider(),
			TerminalEndpoint.StandardInput,
			TerminalEndpoint.StandardOutput,
			transport,
			transport,
			new TerminalSessionOptions {
				TerminalOverride = TerminalProfiles.Dumb,
				ConfigureOutput = false,
				MonotonicClock = clock,
				ObserveLifecycleEvents = false,
				RequireInteractiveOutput = false
			}
		);
		session.RecordSemanticBackendEvidence(
			TerminalProtocolBackend.ApcKittyGraphics,
			TerminalCapabilitySupportState.Verified,
			TerminalCapabilityEvidenceSource.ProtocolResponse
		);
		return session;
	}

	private static async Task YieldSeveralTimesAsync() {
		for ( int iteration = 0; iteration < 8; ++iteration ) {
			await Task.Yield();
		}
	}

	private sealed class AcknowledgingTransport : ITerminalInput, ITerminalOutput {
		private readonly Channel<byte[]> input = Channel.CreateUnbounded<byte[]>(
			new UnboundedChannelOptions {
				SingleReader = true,
				SingleWriter = false,
				AllowSynchronousContinuations = false
			}
		);
		private readonly object synchronization = new();
		private readonly SemaphoreSlim writeSignal = new( 0 );
		private readonly List<byte[]> writes = [];

		internal bool AutoAcknowledgePlacements {
			get;
			set;
		} = true;

		internal IReadOnlyList<byte[]> Writes {
			get {
				lock ( this.synchronization ) {
					return this.writes.Select(
						static value => value.ToArray()
					).ToArray();
				}
			}
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
					"The scripted response exceeds the terminal input buffer."
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
			lock ( this.synchronization ) {
				this.writes.Add( buffer.ToArray() );
			}
			this.writeSignal.Release();
			this.PublishAcknowledgement( buffer.Span );
			return ValueTask.CompletedTask;
		}

		public ValueTask FlushAsync(
			CancellationToken cancellationToken = default
		) {
			cancellationToken.ThrowIfCancellationRequested();
			return ValueTask.CompletedTask;
		}

		internal void Publish(
			byte[] value
		) {
			ArgumentNullException.ThrowIfNull( value );
			if ( !this.input.Writer.TryWrite( value.ToArray() ) ) {
				throw new InvalidOperationException(
					"The scripted terminal input channel rejected a response."
				);
			}
		}

		internal async Task WaitForWriteCountAsync(
			int expected
		) {
			if ( 0 > expected ) {
				throw new ArgumentOutOfRangeException( nameof( expected ) );
			}

			using CancellationTokenSource timeout = new(
				TimeSpan.FromSeconds( 5 )
			);
			while ( this.Writes.Count < expected ) {
				await this.writeSignal.WaitAsync(
					timeout.Token
				).ConfigureAwait( false );
			}
		}

		private void PublishAcknowledgement(
			ReadOnlySpan<byte> frame
		) {
			string text = Encoding.ASCII.GetString( frame );
			if ( text.StartsWith(
				"\u001b_Ga=t,",
				StringComparison.Ordinal
			) && TryReadIdentityField(
				text,
				",I=",
				out uint imageNumber
			) ) {
				this.Publish(
					Encoding.ASCII.GetBytes(
						$"\u001b_Gi={1000u + imageNumber},I={imageNumber};OK\u001b\\"
					)
				);
				return;
			}

			if ( !this.AutoAcknowledgePlacements ) {
				return;
			}
			if ( text.StartsWith(
				"\u001b_Ga=p,",
				StringComparison.Ordinal
			) && TryReadIdentityField(
				text,
				",i=",
				out uint imageId
			) && TryReadIdentityField(
				text,
				",p=",
				out uint placementId
			) ) {
				this.Publish(
					Encoding.ASCII.GetBytes(
						$"\u001b_Gi={imageId},p={placementId};OK\u001b\\"
					)
				);
			}
		}

		private static bool TryReadIdentityField(
			string text,
			string marker,
			out uint value
		) {
			ArgumentNullException.ThrowIfNull( text );
			ArgumentException.ThrowIfNullOrEmpty( marker );
			value = 0u;

			int start = text.IndexOf(
				marker,
				StringComparison.Ordinal
			);
			if ( 0 > start ) {
				return false;
			}
			start += marker.Length;
			int end = start;
			while ( end < text.Length
				&& text[ end ] is >= '0' and <= '9' ) {
				++end;
			}
			return start < end
				&& uint.TryParse(
					text.AsSpan(
						start,
						end - start
					),
					out value
				)
			;
		}
	}

	private sealed class ManualMonotonicClock : IMonotonicClock {
		private readonly object synchronization = new();
		private readonly List<DelayWaiter> waiters = [];
		private long timestamp;

		public long GetTimestamp() {
			lock ( this.synchronization ) {
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
			lock ( this.synchronization ) {
				this.timestamp = checked(
					this.timestamp + elapsed.Ticks
				);
				due = this.waiters
					.Where(
						waiter => waiter.DueTimestamp <= this.timestamp
					).ToList();
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
			lock ( this.synchronization ) {
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
				lock ( this.synchronization ) {
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
}
