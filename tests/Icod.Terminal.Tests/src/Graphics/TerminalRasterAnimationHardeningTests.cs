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
using Xunit;

/// <summary>
/// Adversarial T167 tests for bounded persistent-raster animation ownership.
/// </summary>
public sealed class TerminalRasterAnimationHardeningTests {
	[Fact]
	public void DirectResourceReleaseReclaimsAnimationCapacityBeforeNextAllocation() {
		TerminalPersistentRasterRegistry resources = new();
		TerminalPersistentRasterAnimationRegistry animations = new();
		TerminalPersistentRasterResourceState firstResource = ReserveResource( resources );
		TerminalPersistentRasterAnimationState firstAnimation = CreateAnimation(
			animations,
			firstResource
		);
		PublishOneAppend(
			animations,
			firstAnimation
		);
		Assert.Equal( 2, animations.KnownFrameCount );

		Assert.True( resources.TryReleaseResource( firstResource ) );
		Assert.Equal(
			TerminalRasterAnimationStatus.Released,
			firstAnimation.ObserveState().Status
		);
		Assert.Equal( 0, animations.KnownFrameCount );

		TerminalPersistentRasterResourceState secondResource = ReserveResource( resources );
		Assert.True( animations.TryGetOrCreate( secondResource, out _ ) );
		Assert.Equal( 1, animations.KnownFrameCount );
	}

	[Fact]
	public void DirectResourceMissingReclaimsAnimationCapacityBeforeNextAllocation() {
		TerminalPersistentRasterRegistry resources = new();
		TerminalPersistentRasterAnimationRegistry animations = new();
		TerminalPersistentRasterResourceState firstResource = ReserveResource( resources );
		TerminalPersistentRasterAnimationState firstAnimation = CreateAnimation(
			animations,
			firstResource
		);
		PublishOneAppend(
			animations,
			firstAnimation
		);
		Assert.Equal( 2, animations.KnownFrameCount );

		Assert.True( resources.InvalidateResource( firstResource ) );
		Assert.Equal(
			new TerminalRasterAnimationState(
				TerminalRasterAnimationStatus.Stale,
				TerminalRasterAnimationLossReason.ResourceMissing
			),
			firstAnimation.ObserveState()
		);
		Assert.Equal( 0, animations.KnownFrameCount );

		TerminalPersistentRasterResourceState secondResource = ReserveResource( resources );
		Assert.True( animations.TryGetOrCreate( secondResource, out _ ) );
		Assert.Equal( 1, animations.KnownFrameCount );
	}

	[Fact]
	public void SequenceUncertainAnimationRetainsKnownFrameCapacity() {
		TerminalPersistentRasterRegistry resources = new();
		TerminalPersistentRasterAnimationRegistry animations = new();
		TerminalPersistentRasterAnimationState animation = CreateAnimation(
			animations,
			ReserveResource( resources )
		);
		TerminalPersistentRasterAnimationFrameState appended = PublishOneAppend(
			animations,
			animation
		);

		Assert.True( animation.TryMarkSequenceUncertain() );
		Assert.Equal( 2, animations.KnownFrameCount );
		Assert.True( animations.OwnsFrame( animation, animation.RootFrame ) );
		Assert.True( animations.OwnsFrame( animation, appended ) );
		Assert.False( animations.TryReserveAppend( animation, out _ ) );
	}

	[Fact]
	public async Task StoragePressureFailureRollsBackAndReusesNextFrameNumber() {
		ScriptedTransport transport = new();
		await using TerminalSession session = await OpenSessionAsync( transport );
		await using TerminalRasterResource resource = await CreateResourceAsync(
			session,
			transport,
			imageId: 77u
		);

		Task<TerminalControlResult<TerminalRasterAnimationFrame>> failed =
			resource.Animation.AddFrameAsync(
				CreateFrameImage(),
				TimeSpan.FromMilliseconds( 40 )
			).AsTask();
		await transport.WaitForWriteCountAsync( 2 );
		transport.Publish(
			Encoding.ASCII.GetBytes( "\u001b_Gi=77;ENOSPC:synthetic storage pressure\u001b\\" )
		);
		TerminalControlResult<TerminalRasterAnimationFrame> failure = await failed;

		Assert.Equal( TerminalControlStatus.Failed, failure.Status );
		Assert.Null( failure.Value );
		Assert.Equal(
			new TerminalRasterAnimationState(
				TerminalRasterAnimationStatus.Current,
				TerminalRasterAnimationLossReason.None
			),
			resource.Animation.State
		);

		Task<TerminalControlResult<TerminalRasterAnimationFrame>> retry =
			resource.Animation.AddFrameAsync(
				CreateFrameImage(),
				TimeSpan.FromMilliseconds( 40 )
			).AsTask();
		await transport.WaitForWriteCountAsync( 3 );
		transport.Publish(
			Encoding.ASCII.GetBytes( "\u001b_Gi=77;OK\u001b\\" )
		);
		TerminalControlResult<TerminalRasterAnimationFrame> result = await retry;

		Assert.Equal( TerminalControlStatus.Available, result.Status );
		Assert.Equal(
			2,
			Assert.IsType<TerminalRasterAnimationFrame>( result.Value ).SequenceNumber
		);
	}

	[Fact]
	public async Task SequenceUncertaintyRejectsNewAppendButRetainsKnownFrameControls() {
		ScriptedTransport transport = new();
		await using TerminalSession session = await OpenSessionAsync( transport );
		await using TerminalRasterResource resource = await CreateResourceAsync(
			session,
			transport,
			imageId: 77u
		);
		TerminalRasterAnimationFrame frame = await AppendFrameAsync(
			resource,
			transport,
			expectedWriteCount: 2
		);
		TerminalPersistentRasterAnimationFrameState frameState = Assert.IsType<
			TerminalPersistentRasterAnimationFrameState
		>( frame.State );
		Assert.True( frameState.Animation.TryMarkSequenceUncertain() );
		int writesBeforeRejectedAppend = transport.Writes.Count;

		TerminalControlResult<TerminalRasterAnimationFrame> rejected =
			await resource.Animation.AddFrameAsync(
				CreateFrameImage(),
				TimeSpan.FromMilliseconds( 50 )
			);
		Assert.Equal( TerminalControlStatus.Unavailable, rejected.Status );
		Assert.Null( rejected.Value );
		Assert.Equal( writesBeforeRejectedAppend, transport.Writes.Count );

		Task<TerminalControlMutationResult> duration = resource.Animation
			.SetFrameDurationAsync(
				frame,
				TimeSpan.FromMilliseconds( 75 )
			).AsTask();
		await transport.WaitForWriteCountAsync( writesBeforeRejectedAppend + 1 );
		transport.Publish(
			Encoding.ASCII.GetBytes( "\u001b_Gi=77;OK\u001b\\" )
		);
		Assert.True( ( await duration ).Succeeded );

		Task<TerminalControlMutationResult> selection = resource.Animation
			.SelectFrameAsync( frame )
			.AsTask();
		await transport.WaitForWriteCountAsync( writesBeforeRejectedAppend + 2 );
		transport.Publish(
			Encoding.ASCII.GetBytes( "\u001b_Gi=77;OK\u001b\\" )
		);
		Assert.True( ( await selection ).Succeeded );
		Assert.Equal(
			new TerminalRasterAnimationState(
				TerminalRasterAnimationStatus.SequenceUncertain,
				TerminalRasterAnimationLossReason.FrameSequenceAmbiguous
			),
			resource.Animation.State
		);
	}

	private static TerminalPersistentRasterResourceState ReserveResource(
		TerminalPersistentRasterRegistry registry
	) {
		Assert.True(
			registry.TryReserveResource(
				sourceWidth: 8,
				sourceHeight: 8,
				out TerminalPersistentRasterResourceState? resource
			)
		);
		return Assert.IsType<TerminalPersistentRasterResourceState>( resource );
	}

	private static TerminalPersistentRasterAnimationState CreateAnimation(
		TerminalPersistentRasterAnimationRegistry registry,
		TerminalPersistentRasterResourceState resource
	) {
		Assert.True(
			registry.TryGetOrCreate(
				resource,
				out TerminalPersistentRasterAnimationState? animation
			)
		);
		return Assert.IsType<TerminalPersistentRasterAnimationState>( animation );
	}

	private static TerminalPersistentRasterAnimationFrameState PublishOneAppend(
		TerminalPersistentRasterAnimationRegistry registry,
		TerminalPersistentRasterAnimationState animation
	) {
		Assert.True(
			registry.TryReserveAppend(
				animation,
				out TerminalPersistentRasterAnimationRegistry.AppendReservation? reservation
			)
		);
		Assert.NotNull( reservation );
		Assert.True(
			registry.TryPublishAppend(
				reservation,
				out TerminalPersistentRasterAnimationFrameState? frame
			)
		);
		return Assert.IsType<TerminalPersistentRasterAnimationFrameState>( frame );
	}

	private static TerminalRasterImage CreateFrameImage() {
		return TerminalRasterImage.CreateRgb24(
			1,
			1,
			[ 4, 5, 6 ]
		);
	}

	private static async Task<TerminalRasterAnimationFrame> AppendFrameAsync(
		TerminalRasterResource resource,
		ScriptedTransport transport,
		int expectedWriteCount
	) {
		Task<TerminalControlResult<TerminalRasterAnimationFrame>> append =
			resource.Animation.AddFrameAsync(
				CreateFrameImage(),
				TimeSpan.FromMilliseconds( 40 )
			).AsTask();
		await transport.WaitForWriteCountAsync( expectedWriteCount );
		transport.Publish(
			Encoding.ASCII.GetBytes( "\u001b_Gi=77;OK\u001b\\" )
		);
		TerminalControlResult<TerminalRasterAnimationFrame> result = await append;
		Assert.Equal( TerminalControlStatus.Available, result.Status );
		return Assert.IsType<TerminalRasterAnimationFrame>( result.Value );
	}

	private static async Task<TerminalRasterResource> CreateResourceAsync(
		TerminalSession session,
		ScriptedTransport transport,
		uint imageId
	) {
		Task<TerminalControlResult<TerminalRasterResource>> creation =
			session.CreateRasterResourceAsync(
				TerminalRasterImage.CreateRgb24(
					1,
					1,
					[ 1, 2, 3 ]
				)
			).AsTask();
		await transport.WaitForWriteCountAsync( 1 );
		transport.Publish(
			Encoding.ASCII.GetBytes(
				$"\u001b_Gi={imageId},I=1;OK\u001b\\"
			)
		);
		TerminalControlResult<TerminalRasterResource> result = await creation;
		Assert.Equal( TerminalControlStatus.Available, result.Status );
		return Assert.IsType<TerminalRasterResource>( result.Value );
	}

	private static async ValueTask<TerminalSession> OpenSessionAsync(
		ScriptedTransport transport
	) {
		TerminalSession session = await TerminalSession.OpenAsync(
			new RecordingTerminalControlProvider(),
			TerminalEndpoint.StandardInput,
			TerminalEndpoint.StandardOutput,
			transport,
			transport,
			new TerminalSessionOptions {
				TerminalOverride = TerminalProfiles.Dumb,
				ConfigureOutput = false,
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
		private readonly List<byte[]> writes = [];

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
			using CancellationTokenSource timeout = new(
				TimeSpan.FromSeconds( 5 )
			);
			while ( this.Writes.Count < expected ) {
				await this.writeSignal.WaitAsync(
					timeout.Token
				).ConfigureAwait( false );
			}
		}
	}
}
