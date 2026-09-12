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

using System.Reflection;
using System.Text;
using System.Threading.Channels;
using Icod.Terminal;
using Icod.TermInfo;
using Icod.Timing;
using Xunit;

/// <summary>
/// Defines the C114 public persistent-resource and acknowledged-upload contract.
/// </summary>
public sealed class TerminalPersistentRasterResourceTests {
	[Fact]
	public void PublicResourceContractIsOpaqueAndSessionOwned() {
		Type resourceType = typeof( TerminalRasterResource );

		Assert.True( resourceType.IsPublic );
		Assert.True( resourceType.IsSealed );
		Assert.Contains( typeof( IAsyncDisposable ), resourceType.GetInterfaces() );
		Assert.DoesNotContain(
			resourceType.GetMembers( BindingFlags.Instance | BindingFlags.Public ),
			static member => member.Name.Contains( "ImageId", StringComparison.Ordinal )
				|| member.Name.Contains( "ImageNumber", StringComparison.Ordinal )
				|| member.Name.Contains( "PlacementId", StringComparison.Ordinal )
				|| member.Name.Contains( "Backend", StringComparison.Ordinal )
		);

		MethodInfo? create = typeof( TerminalSession ).GetMethod(
			"CreateRasterResourceAsync",
			BindingFlags.Instance | BindingFlags.Public,
			binder: null,
			types: [
				typeof( TerminalRasterImage ),
				typeof( CancellationToken )
			],
			modifiers: null
		);
		Assert.NotNull( create );
		Assert.Equal(
			typeof( ValueTask<TerminalControlResult<TerminalRasterResource>> ),
			create.ReturnType
		);
	}

	[Fact]
	public async Task NullImageIsRejectedBeforeOutput() {
		ScriptedTransport transport = new();
		await using TerminalSession session = await OpenSessionAsync(
			new RecordingTerminalControlProvider(),
			transport,
			new ManualMonotonicClock()
		);
		SeedKittyVerified( session );

		await Assert.ThrowsAsync<ArgumentNullException>(
			async () => await session.CreateRasterResourceAsync( null! )
		);
		Assert.Empty( transport.Writes );
	}

	[Fact]
	public async Task PreCancellationIsRejectedBeforeOutput() {
		ScriptedTransport transport = new();
		await using TerminalSession session = await OpenSessionAsync(
			new RecordingTerminalControlProvider(),
			transport,
			new ManualMonotonicClock()
		);
		SeedKittyVerified( session );
		using CancellationTokenSource cancellation = new();
		cancellation.Cancel();

		await Assert.ThrowsAnyAsync<OperationCanceledException>(
			async () => await session.CreateRasterResourceAsync(
				CreateSmallImage(),
				cancellation.Token
			)
		);
		Assert.Empty( transport.Writes );
	}

	[Fact]
	public async Task RedirectedOutputReturnsUnavailableWithoutProtocolTraffic() {
		ScriptedTransport transport = new();
		await using TerminalSession session = await OpenSessionAsync(
			new RedirectedOutputControlProvider(),
			transport,
			new ManualMonotonicClock()
		);

		TerminalControlResult<TerminalRasterResource> result =
			await session.CreateRasterResourceAsync( CreateSmallImage() );

		Assert.Equal( TerminalControlStatus.Unavailable, result.Status );
		Assert.Null( result.Value );
		Assert.Empty( transport.Writes );
	}

	[Fact]
	public async Task KnownKittyUnsupportedReturnsUnsupportedWithoutSixelFallback() {
		ScriptedTransport transport = new();
		await using TerminalSession session = await OpenSessionAsync(
			new RecordingTerminalControlProvider(),
			transport,
			new ManualMonotonicClock()
		);
		session.RecordSemanticBackendEvidence(
			TerminalProtocolBackend.DcsSixel,
			TerminalCapabilitySupportState.Verified,
			TerminalCapabilityEvidenceSource.ProtocolResponse
		);
		session.RecordSemanticBackendEvidence(
			TerminalProtocolBackend.ApcKittyGraphics,
			TerminalCapabilitySupportState.Unsupported,
			TerminalCapabilityEvidenceSource.ProtocolResponse
		);

		TerminalControlResult<TerminalRasterResource> result =
			await session.CreateRasterResourceAsync( CreateSmallImage() );

		Assert.Equal( TerminalControlStatus.Unsupported, result.Status );
		Assert.Null( result.Value );
		Assert.Empty( transport.Writes );
	}

	[Fact]
	public async Task MatchingAcknowledgementPublishesOpaqueResource() {
		ScriptedTransport transport = new();
		await using TerminalSession session = await OpenSessionAsync(
			new RecordingTerminalControlProvider(),
			transport,
			new ManualMonotonicClock()
		);
		SeedKittyVerified( session );

		Task<TerminalControlResult<TerminalRasterResource>> creation =
			session.CreateRasterResourceAsync( CreateSmallImage() ).AsTask();
		await transport.WaitForWriteCountAsync( 1 );
		Assert.Equal(
			Encoding.ASCII.GetBytes(
				"\u001b_Ga=t,f=24,s=1,v=1,t=d,I=1,m=0;AQID\u001b\\"
			),
			transport.Writes[ 0 ]
		);

		transport.Publish(
			Encoding.ASCII.GetBytes( "\u001b_Gi=77,I=1;OK\u001b\\" )
		);
		TerminalControlResult<TerminalRasterResource> result = await creation;

		Assert.Equal( TerminalControlStatus.Available, result.Status );
		Assert.NotNull( result.Value );
	}

	[Fact]
	public async Task WrongImageNumberRemainsUnrelatedUntilMatchingAcknowledgementArrives() {
		ScriptedTransport transport = new();
		await using TerminalSession session = await OpenSessionAsync(
			new RecordingTerminalControlProvider(),
			transport,
			new ManualMonotonicClock()
		);
		SeedKittyVerified( session );

		Task<TerminalControlResult<TerminalRasterResource>> creation =
			session.CreateRasterResourceAsync( CreateSmallImage() ).AsTask();
		await transport.WaitForWriteCountAsync( 1 );
		transport.Publish(
			Encoding.ASCII.GetBytes( "\u001b_Gi=88,I=2;OK\u001b\\" )
		);
		transport.Publish(
			Encoding.ASCII.GetBytes( "\u001b_Gi=77,I=1;OK\u001b\\" )
		);

		TerminalControlResult<TerminalRasterResource> result = await creation;
		Assert.Equal( TerminalControlStatus.Available, result.Status );
		Assert.NotNull( result.Value );
	}

	[Fact]
	public async Task MalformedCorrelatedAcknowledgementThrowsFormatException() {
		ScriptedTransport transport = new();
		await using TerminalSession session = await OpenSessionAsync(
			new RecordingTerminalControlProvider(),
			transport,
			new ManualMonotonicClock()
		);
		SeedKittyVerified( session );

		Task<TerminalControlResult<TerminalRasterResource>> creation =
			session.CreateRasterResourceAsync( CreateSmallImage() ).AsTask();
		await transport.WaitForWriteCountAsync( 1 );
		transport.Publish(
			Encoding.ASCII.GetBytes( "\u001b_Gi=x,I=1;OK\u001b\\" )
		);

		await Assert.ThrowsAsync<FormatException>( () => creation );
	}

	[Fact]
	public async Task WellFormedNegativeAcknowledgementReturnsFailedWithoutResource() {
		ScriptedTransport transport = new();
		await using TerminalSession session = await OpenSessionAsync(
			new RecordingTerminalControlProvider(),
			transport,
			new ManualMonotonicClock()
		);
		SeedKittyVerified( session );

		Task<TerminalControlResult<TerminalRasterResource>> creation =
			session.CreateRasterResourceAsync( CreateSmallImage() ).AsTask();
		await transport.WaitForWriteCountAsync( 1 );
		transport.Publish(
			Encoding.ASCII.GetBytes( "\u001b_Gi=77,I=1;EINVAL:synthetic failure\u001b\\" )
		);

		TerminalControlResult<TerminalRasterResource> result = await creation;
		Assert.Equal( TerminalControlStatus.Failed, result.Status );
		Assert.Null( result.Value );
	}

	[Fact]
	public async Task EnoentAcknowledgementReturnsUnavailableWithoutResource() {
		ScriptedTransport transport = new();
		await using TerminalSession session = await OpenSessionAsync(
			new RecordingTerminalControlProvider(),
			transport,
			new ManualMonotonicClock()
		);
		SeedKittyVerified( session );

		Task<TerminalControlResult<TerminalRasterResource>> creation =
			session.CreateRasterResourceAsync( CreateSmallImage() ).AsTask();
		await transport.WaitForWriteCountAsync( 1 );
		transport.Publish(
			Encoding.ASCII.GetBytes( "\u001b_GI=1;ENOENT:synthetic missing resource\u001b\\" )
		);

		TerminalControlResult<TerminalRasterResource> result = await creation;
		Assert.Equal( TerminalControlStatus.Unavailable, result.Status );
		Assert.Null( result.Value );
	}

	[Fact]
	public async Task CorrelatedTimeoutThrowsAndDoesNotPublishResource() {
		ScriptedTransport transport = new();
		ManualMonotonicClock clock = new();
		await using TerminalSession session = await OpenSessionAsync(
			new RecordingTerminalControlProvider(),
			transport,
			clock
		);
		SeedKittyVerified( session );

		Task<TerminalControlResult<TerminalRasterResource>> creation =
			session.CreateRasterResourceAsync( CreateSmallImage() ).AsTask();
		await transport.WaitForWriteCountAsync( 1 );
		clock.Advance( TimeSpan.FromSeconds( 2 ) );

		await Assert.ThrowsAsync<TimeoutException>( () => creation );
	}

	[Fact]
	public async Task CancellationAfterFirstCommittedFrameDoesNotTruncateUpload() {
		using CancellationTokenSource cancellation = new();
		ScriptedTransport transport = new(
			onFirstWrite: cancellation.Cancel
		);
		await using TerminalSession session = await OpenSessionAsync(
			new RecordingTerminalControlProvider(),
			transport,
			new ManualMonotonicClock()
		);
		SeedKittyVerified( session );
		byte[] pixels = new byte[ 1025 * 3 ];
		TerminalRasterImage image = TerminalRasterImage.CreateRgb24(
			1025,
			1,
			pixels
		);

		Task<TerminalControlResult<TerminalRasterResource>> creation =
			session.CreateRasterResourceAsync(
				image,
				cancellation.Token
			).AsTask();
		await transport.WaitForWriteCountAsync( 2 );

		await Assert.ThrowsAnyAsync<OperationCanceledException>( () => creation );
		Assert.Equal( 2, transport.Writes.Count );
		transport.Publish(
			Encoding.ASCII.GetBytes( "\u001b_Gi=77,I=1;OK\u001b\\" )
		);
	}

	private static TerminalRasterImage CreateSmallImage() {
		return TerminalRasterImage.CreateRgb24(
			1,
			1,
			[ 1, 2, 3 ]
		);
	}

	private static void SeedKittyVerified(
		TerminalSession session
	) {
		ArgumentNullException.ThrowIfNull( session );
		session.RecordSemanticBackendEvidence(
			TerminalProtocolBackend.ApcKittyGraphics,
			TerminalCapabilitySupportState.Verified,
			TerminalCapabilityEvidenceSource.ProtocolResponse
		);
	}

	private static ValueTask<TerminalSession> OpenSessionAsync(
		ITerminalControlProvider controlProvider,
		ScriptedTransport transport,
		IMonotonicClock clock
	) {
		ArgumentNullException.ThrowIfNull( controlProvider );
		ArgumentNullException.ThrowIfNull( transport );
		ArgumentNullException.ThrowIfNull( clock );

		return TerminalSession.OpenAsync(
			controlProvider,
			TerminalEndpoint.StandardInput,
			TerminalEndpoint.StandardOutput,
			transport,
			transport,
			new TerminalSessionOptions {
				TerminalOverride = TerminalProfiles.Dumb,
				ConfigureOutput = false,
				ObserveLifecycleEvents = false,
				RequireInteractiveOutput = false,
				MonotonicClock = clock
			}
		);
	}

	private sealed class ScriptedTransport : ITerminalInput, ITerminalOutput {
		private readonly Channel<byte[]> input = Channel.CreateUnbounded<byte[]>(
			new UnboundedChannelOptions {
				SingleReader = true,
				SingleWriter = false,
				AllowSynchronousContinuations = false
			}
		);
		private readonly object synchronization = new();
		private readonly SemaphoreSlim writeSignal = new( 0 );
		private readonly Action? onFirstWrite;
		private readonly List<byte[]> writes = [];

		internal ScriptedTransport(
			Action? onFirstWrite = null
		) {
			this.onFirstWrite = onFirstWrite;
		}

		internal IReadOnlyList<byte[]> Writes {
			get {
				lock ( this.synchronization ) {
					return this.writes.Select(
						static item => item.ToArray()
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
			bool first;
			lock ( this.synchronization ) {
				first = 0 == this.writes.Count;
				this.writes.Add( buffer.ToArray() );
			}
			this.writeSignal.Release();
			if ( first ) {
				this.onFirstWrite?.Invoke();
			}
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
				lock ( this.synchronization ) {
					if ( expected <= this.writes.Count ) {
						return;
					}
				}
				await this.writeSignal.WaitAsync(
					timeout.Token
				).ConfigureAwait( false );
			}
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

	private class RecordingTerminalControlProvider : ITerminalControlProvider {
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

		public virtual TerminalControlResult<TerminalEndpointObservation> Observe(
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
				"Size is not required by this test."
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

	private sealed class RedirectedOutputControlProvider : RecordingTerminalControlProvider {
		public override TerminalControlResult<TerminalEndpointObservation> Observe(
			TerminalEndpoint endpoint
		) {
			ArgumentNullException.ThrowIfNull( endpoint );
			bool isInput = ReferenceEquals( endpoint, TerminalEndpoint.StandardInput );
			return TerminalControlResult<TerminalEndpointObservation>.Available(
				new TerminalEndpointObservation(
					isInput,
					null,
					isInput ? TerminalPlatformKind.PosixTermios : null,
					isInput
						? TerminalControlCapabilities.Attachment
							| TerminalControlCapabilities.ModeRead
							| TerminalControlCapabilities.ModeWrite
						: TerminalControlCapabilities.None
				)
			);
		}
	}
}
