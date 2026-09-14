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
/// Freezes the T155 virtual-placeholder relative-parent contract.
/// </summary>
public sealed class TerminalRasterPlaceholderRelativeParentTests {
	[Fact]
	public async Task VirtualParentCreationAndUpdateUsePrivateParentIdentity() {
		ScriptedTransport transport = new();
		await using TerminalSession session = await OpenSessionAsync( transport );
		TerminalRasterResource parentResource = await CreateResourceAsync(
			session,
			transport,
			imageId: 77u,
			imageNumber: 1u
		);
		TerminalRasterPlaceholder parent = await CreatePlaceholderAsync(
			parentResource,
			transport,
			imageId: 77u,
			placementId: 1u
		);
		TerminalRasterResource childResource = await CreateResourceAsync(
			session,
			transport,
			imageId: 88u,
			imageNumber: 2u
		);
		int expectedWriteCount = transport.Writes.Count + 1;

		Task<TerminalControlResult<TerminalRasterPlacement>> creation =
			childResource.CreateRelativePlacementFromPlaceholderAsync(
				parent,
				columnOffset: -2,
				rowOffset: 3,
				new TerminalRasterPlacementOptions {
					SourceRectangle = new TerminalRasterSourceRectangle( 1, 0, 1, 2 ),
					Columns = 4,
					Rows = 2,
					ZIndex = -1
				}
			).AsTask();
		Assert.False( creation.IsCompleted );
		await transport.WaitForWriteCountAsync( expectedWriteCount );
		Assert.Equal(
			Encoding.ASCII.GetBytes(
				"\u001b_Ga=p,i=88,p=2,C=1,P=77,Q=1,H=-2,V=3,x=1,y=0,w=1,h=2,c=4,r=2,z=-1\u001b\\"
			),
			transport.Writes[ expectedWriteCount - 1 ]
		);
		transport.Publish(
			Encoding.ASCII.GetBytes( "\u001b_Gi=88,p=2;OK\u001b\\" )
		);
		TerminalControlResult<TerminalRasterPlacement> creationResult = await creation;
		Assert.Equal( TerminalControlStatus.Available, creationResult.Status );
		TerminalRasterPlacement child = Assert.IsType<TerminalRasterPlacement>(
			creationResult.Value
		);

		expectedWriteCount = transport.Writes.Count + 1;
		Task<TerminalControlMutationResult> update = child.UpdateRelativeAsync(
			columnOffset: 5,
			rowOffset: -6,
			new TerminalRasterPlacementOptions {
				Rows = 1,
				ZIndex = 4
			}
		).AsTask();
		await transport.WaitForWriteCountAsync( expectedWriteCount );
		Assert.Equal(
			Encoding.ASCII.GetBytes(
				"\u001b_Ga=p,i=88,p=2,C=1,P=77,Q=1,H=5,V=-6,r=1,z=4\u001b\\"
			),
			transport.Writes[ expectedWriteCount - 1 ]
		);
		transport.Publish(
			Encoding.ASCII.GetBytes( "\u001b_Gi=88,p=2;OK\u001b\\" )
		);
		TerminalControlMutationResult updateResult = await update;
		Assert.Equal( TerminalControlStatus.Available, updateResult.Status );
	}

	[Fact]
	public async Task DisposingVirtualParentReleasesPhysicalDescendantButNotResources() {
		ScriptedTransport transport = new();
		await using TerminalSession session = await OpenSessionAsync( transport );
		TerminalRasterResource parentResource = await CreateResourceAsync(
			session,
			transport,
			imageId: 77u,
			imageNumber: 1u
		);
		TerminalRasterPlaceholder parent = await CreatePlaceholderAsync(
			parentResource,
			transport,
			imageId: 77u,
			placementId: 1u
		);
		TerminalRasterResource childResource = await CreateResourceAsync(
			session,
			transport,
			imageId: 88u,
			imageNumber: 2u
		);
		TerminalRasterPlacement child = await CreateRelativeChildAsync(
			childResource,
			parent,
			transport,
			childImageId: 88u,
			childPlacementId: 2u
		);
		int baselineWrites = transport.Writes.Count;

		await parent.DisposeAsync();

		Assert.Equal( baselineWrites + 2, transport.Writes.Count );
		Assert.Equal(
			Encoding.ASCII.GetBytes(
				"\u001b_Ga=d,d=i,i=88,p=2,q=2\u001b\\"
			),
			transport.Writes[ baselineWrites ]
		);
		Assert.Equal(
			Encoding.ASCII.GetBytes(
				"\u001b_Ga=d,d=i,i=77,p=1,q=2\u001b\\"
			),
			transport.Writes[ baselineWrites + 1 ]
		);
		Assert.Equal(
			new TerminalRasterOwnershipState(
				TerminalRasterOwnershipStatus.Disposed,
				TerminalRasterOwnershipLossReason.ExplicitDisposal
			),
			parent.OwnershipState
		);
		Assert.Equal(
			new TerminalRasterOwnershipState(
				TerminalRasterOwnershipStatus.Released,
				TerminalRasterOwnershipLossReason.AncestorReleased
			),
			child.OwnershipState
		);
		Assert.Equal(
			new TerminalRasterOwnershipState(
				TerminalRasterOwnershipStatus.Current,
				TerminalRasterOwnershipLossReason.None
			),
			parentResource.OwnershipState
		);
		Assert.Equal(
			new TerminalRasterOwnershipState(
				TerminalRasterOwnershipStatus.Current,
				TerminalRasterOwnershipLossReason.None
			),
			childResource.OwnershipState
		);
	}

	[Fact]
	public async Task EnoParentStalesVirtualParentAndLeavesResourcesCurrent() {
		ScriptedTransport transport = new();
		await using TerminalSession session = await OpenSessionAsync( transport );
		TerminalRasterResource parentResource = await CreateResourceAsync(
			session,
			transport,
			imageId: 77u,
			imageNumber: 1u
		);
		TerminalRasterPlaceholder parent = await CreatePlaceholderAsync(
			parentResource,
			transport,
			imageId: 77u,
			placementId: 1u
		);
		TerminalRasterResource childResource = await CreateResourceAsync(
			session,
			transport,
			imageId: 88u,
			imageNumber: 2u
		);
		int expectedWriteCount = transport.Writes.Count + 1;

		Task<TerminalControlResult<TerminalRasterPlacement>> creation =
			childResource.CreateRelativePlacementFromPlaceholderAsync(
				parent,
				columnOffset: 1,
				rowOffset: -1
			).AsTask();
		Assert.False( creation.IsCompleted );
		await transport.WaitForWriteCountAsync( expectedWriteCount );
		transport.Publish(
			Encoding.ASCII.GetBytes(
				"\u001b_Gi=88,p=2;ENOPARENT:synthetic missing virtual parent\u001b\\"
			)
		);
		TerminalControlResult<TerminalRasterPlacement> result = await creation;

		Assert.Equal( TerminalControlStatus.Unavailable, result.Status );
		Assert.Null( result.Value );
		Assert.Equal(
			new TerminalRasterOwnershipState(
				TerminalRasterOwnershipStatus.Stale,
				TerminalRasterOwnershipLossReason.ParentPlacementLost
			),
			parent.OwnershipState
		);
		Assert.Equal(
			new TerminalRasterOwnershipState(
				TerminalRasterOwnershipStatus.Current,
				TerminalRasterOwnershipLossReason.None
			),
			parentResource.OwnershipState
		);
		Assert.Equal(
			new TerminalRasterOwnershipState(
				TerminalRasterOwnershipStatus.Current,
				TerminalRasterOwnershipLossReason.None
			),
			childResource.OwnershipState
		);
	}

	private static async Task<TerminalRasterPlacement> CreateRelativeChildAsync(
		TerminalRasterResource resource,
		TerminalRasterPlaceholder parent,
		ScriptedTransport transport,
		uint childImageId,
		uint childPlacementId
	) {
		int expectedWriteCount = transport.Writes.Count + 1;
		Task<TerminalControlResult<TerminalRasterPlacement>> creation =
			resource.CreateRelativePlacementFromPlaceholderAsync(
				parent,
				columnOffset: 1,
				rowOffset: 1
			).AsTask();
		await transport.WaitForWriteCountAsync( expectedWriteCount );
		transport.Publish(
			Encoding.ASCII.GetBytes(
				$"\u001b_Gi={childImageId},p={childPlacementId};OK\u001b\\"
			)
		);
		TerminalControlResult<TerminalRasterPlacement> result = await creation;
		Assert.Equal( TerminalControlStatus.Available, result.Status );
		return Assert.IsType<TerminalRasterPlacement>( result.Value );
	}

	private static async Task<TerminalRasterPlaceholder> CreatePlaceholderAsync(
		TerminalRasterResource resource,
		ScriptedTransport transport,
		uint imageId,
		uint placementId
	) {
		int expectedWriteCount = transport.Writes.Count + 1;
		Task<TerminalControlResult<TerminalRasterPlaceholder>> creation =
			resource.CreatePlaceholderAsync(
				new TerminalRasterPlaceholderOptions {
					Columns = 2,
					Rows = 2
				}
			).AsTask();
		await transport.WaitForWriteCountAsync( expectedWriteCount );
		transport.Publish(
			Encoding.ASCII.GetBytes(
				$"\u001b_Gi={imageId},p={placementId};OK\u001b\\"
			)
		);
		TerminalControlResult<TerminalRasterPlaceholder> result = await creation;
		Assert.Equal( TerminalControlStatus.Available, result.Status );
		return Assert.IsType<TerminalRasterPlaceholder>( result.Value );
	}

	private static async Task<TerminalRasterResource> CreateResourceAsync(
		TerminalSession session,
		ScriptedTransport transport,
		uint imageId,
		uint imageNumber
	) {
		int expectedWriteCount = transport.Writes.Count + 1;
		Task<TerminalControlResult<TerminalRasterResource>> creation =
			session.CreateRasterResourceAsync(
				TerminalRasterImage.CreateRgb24(
					2,
					2,
					[
						1, 2, 3,
						4, 5, 6,
						7, 8, 9,
						10, 11, 12,
					]
				)
			).AsTask();
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
}
