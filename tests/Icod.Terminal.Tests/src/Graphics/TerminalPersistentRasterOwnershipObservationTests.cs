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
/// Verifies the public 1.14 ownership snapshot against live acknowledged persistent-raster handles.
/// </summary>
public sealed class TerminalPersistentRasterOwnershipObservationTests {
	[Fact]
	public async Task ObservationIsSideEffectFreeAndInvalidationThenDisposalIsMonotonic() {
		ScriptedTransport transport = new();
		await using TerminalSession session = await OpenSessionAsync( transport );
		TerminalRasterResource resource = await CreateResourceAsync(
			session,
			transport,
			imageId: 77,
			imageNumber: 1
		);
		TerminalRasterPlacement placement = await CreatePlacementAsync(
			resource,
			transport,
			imageId: 77,
			placementId: 1
		);

		AssertState(
			resource.OwnershipState,
			TerminalRasterOwnershipStatus.Current,
			TerminalRasterOwnershipLossReason.None
		);
		AssertState(
			placement.OwnershipState,
			TerminalRasterOwnershipStatus.Current,
			TerminalRasterOwnershipLossReason.None
		);
		int baselineWrites = transport.WriteCount;

		for ( int index = 0; index < 32; ++index ) {
			_ = resource.OwnershipState;
			_ = placement.OwnershipState;
		}
		Assert.Equal( baselineWrites, transport.WriteCount );

		session.InvalidateState();

		AssertState(
			resource.OwnershipState,
			TerminalRasterOwnershipStatus.Stale,
			TerminalRasterOwnershipLossReason.SessionStateLost
		);
		AssertState(
			placement.OwnershipState,
			TerminalRasterOwnershipStatus.Stale,
			TerminalRasterOwnershipLossReason.SessionStateLost
		);
		Assert.Equal( baselineWrites, transport.WriteCount );

		await placement.DisposeAsync();
		await resource.DisposeAsync();

		AssertState(
			placement.OwnershipState,
			TerminalRasterOwnershipStatus.Disposed,
			TerminalRasterOwnershipLossReason.ExplicitDisposal
		);
		AssertState(
			resource.OwnershipState,
			TerminalRasterOwnershipStatus.Disposed,
			TerminalRasterOwnershipLossReason.ExplicitDisposal
		);
		Assert.Equal( baselineWrites, transport.WriteCount );
	}

	[Fact]
	public async Task ParentCascadeReleasesChildWhileIndependentResourceRemainsCurrent() {
		ScriptedTransport transport = new();
		await using TerminalSession session = await OpenSessionAsync( transport );
		TerminalRasterResource parentResource = await CreateResourceAsync(
			session,
			transport,
			imageId: 77,
			imageNumber: 1
		);
		TerminalRasterPlacement parent = await CreatePlacementAsync(
			parentResource,
			transport,
			imageId: 77,
			placementId: 1
		);
		TerminalRasterResource childResource = await CreateResourceAsync(
			session,
			transport,
			imageId: 88,
			imageNumber: 2
		);
		TerminalRasterPlacement child = await CreateRelativePlacementAsync(
			childResource,
			parent,
			transport,
			imageId: 88,
			placementId: 2
		);

		AssertState(
			child.OwnershipState,
			TerminalRasterOwnershipStatus.Current,
			TerminalRasterOwnershipLossReason.None
		);
		AssertState(
			childResource.OwnershipState,
			TerminalRasterOwnershipStatus.Current,
			TerminalRasterOwnershipLossReason.None
		);

		await parent.DisposeAsync();

		AssertState(
			parent.OwnershipState,
			TerminalRasterOwnershipStatus.Disposed,
			TerminalRasterOwnershipLossReason.ExplicitDisposal
		);
		AssertState(
			child.OwnershipState,
			TerminalRasterOwnershipStatus.Released,
			TerminalRasterOwnershipLossReason.AncestorReleased
		);
		AssertState(
			childResource.OwnershipState,
			TerminalRasterOwnershipStatus.Current,
			TerminalRasterOwnershipLossReason.None
		);

		TerminalRasterPlacement survivingPlacement = await CreatePlacementAsync(
			childResource,
			transport,
			imageId: 88,
			placementId: 3
		);
		AssertState(
			survivingPlacement.OwnershipState,
			TerminalRasterOwnershipStatus.Current,
			TerminalRasterOwnershipLossReason.None
		);

		await child.DisposeAsync();
		AssertState(
			child.OwnershipState,
			TerminalRasterOwnershipStatus.Disposed,
			TerminalRasterOwnershipLossReason.ExplicitDisposal
		);

		await survivingPlacement.DisposeAsync();
		await childResource.DisposeAsync();
		await parentResource.DisposeAsync();
	}

	private static void AssertState(
		TerminalRasterOwnershipState state,
		TerminalRasterOwnershipStatus status,
		TerminalRasterOwnershipLossReason reason
	) {
		Assert.Equal( status, state.Status );
		Assert.Equal( reason, state.LossReason );
	}

	private static TerminalRasterImage CreateSmallImage() {
		return TerminalRasterImage.CreateRgb24(
			1,
			1,
			[ 1, 2, 3 ]
		);
	}

	private static async Task<TerminalRasterResource> CreateResourceAsync(
		TerminalSession session,
		ScriptedTransport transport,
		uint imageId,
		uint imageNumber
	) {
		int expectedWriteCount = transport.WriteCount + 1;
		Task<TerminalControlResult<TerminalRasterResource>> creation =
			session.CreateRasterResourceAsync( CreateSmallImage() ).AsTask();
		await transport.WaitForWriteCountAsync( expectedWriteCount );
		transport.Publish(
			Encoding.ASCII.GetBytes(
				$"\u001b_Gi={imageId},I={imageNumber};OK\u001b\\"
			)
		);
		TerminalControlResult<TerminalRasterResource> result = await creation;
		Assert.Equal( TerminalControlStatus.Available, result.Status );
		return Assert.IsType<TerminalRasterResource>( result.Value );
	}

	private static async Task<TerminalRasterPlacement> CreatePlacementAsync(
		TerminalRasterResource resource,
		ScriptedTransport transport,
		uint imageId,
		uint placementId
	) {
		int expectedWriteCount = transport.WriteCount + 1;
		Task<TerminalControlResult<TerminalRasterPlacement>> creation =
			resource.CreatePlacementAsync().AsTask();
		await transport.WaitForWriteCountAsync( expectedWriteCount );
		transport.Publish(
			Encoding.ASCII.GetBytes(
				$"\u001b_Gi={imageId},p={placementId};OK\u001b\\"
			)
		);
		TerminalControlResult<TerminalRasterPlacement> result = await creation;
		Assert.Equal( TerminalControlStatus.Available, result.Status );
		return Assert.IsType<TerminalRasterPlacement>( result.Value );
	}

	private static async Task<TerminalRasterPlacement> CreateRelativePlacementAsync(
		TerminalRasterResource resource,
		TerminalRasterPlacement parent,
		ScriptedTransport transport,
		uint imageId,
		uint placementId
	) {
		int expectedWriteCount = transport.WriteCount + 1;
		Task<TerminalControlResult<TerminalRasterPlacement>> creation =
			resource.CreateRelativePlacementAsync(
				parent,
				columnOffset: 2,
				rowOffset: -1
			).AsTask();
		await transport.WaitForWriteCountAsync( expectedWriteCount );
		transport.Publish(
			Encoding.ASCII.GetBytes(
				$"\u001b_Gi={imageId},p={placementId};OK\u001b\\"
			)
		);
		TerminalControlResult<TerminalRasterPlacement> result = await creation;
		Assert.Equal( TerminalControlStatus.Available, result.Status );
		return Assert.IsType<TerminalRasterPlacement>( result.Value );
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
				MonotonicClock = new FrozenMonotonicClock(),
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

		internal int WriteCount {
			get {
				lock ( this.synchronization ) {
					return this.writes.Count;
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
			byte[] value
		) {
			if ( !this.input.Writer.TryWrite( value.ToArray() ) ) {
				throw new InvalidOperationException(
					"The scripted terminal input channel rejected a response."
				);
			}
		}

		internal async Task WaitForWriteCountAsync(
			int count
		) {
			while ( true ) {
				lock ( this.synchronization ) {
					if ( count <= this.writes.Count ) {
						return;
					}
				}
				await this.writeSignal.WaitAsync().ConfigureAwait( false );
			}
		}
	}
}
