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
/// Hardens the T156 mixed physical/virtual ownership graph.
/// </summary>
public sealed class TerminalRasterPlaceholderHardeningTests {
	[Fact]
	public async Task ReleasingPlaceholderOwningResourceReleasesCrossResourcePhysicalSubtree() {
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
		TerminalRasterPlacement child = await CreateVirtualChildAsync(
			childResource,
			parent,
			transport,
			imageId: 88u,
			placementId: 2u
		);
		int baselineWrites = transport.Writes.Count;

		await parentResource.DisposeAsync();

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
			childResource.OwnershipState
		);
		Assert.Contains(
			transport.Writes.Skip( baselineWrites ),
			static bytes => Encoding.ASCII.GetString( bytes )
				== "\u001b_Ga=d,d=i,i=88,p=2,q=2\u001b\\"
		);
	}

	[Fact]
	public async Task MissingPlaceholderResourceStalesCrossResourcePhysicalSubtree() {
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
		TerminalRasterPlacement child = await CreateVirtualChildAsync(
			childResource,
			parent,
			transport,
			imageId: 88u,
			placementId: 2u
		);

		int expectedWriteCount = transport.Writes.Count + 1;
		Task<TerminalControlResult<TerminalRasterPlaceholder>> secondCreation =
			parentResource.CreatePlaceholderAsync(
				new TerminalRasterPlaceholderOptions {
					Columns = 1,
					Rows = 1
				}
			).AsTask();
		await transport.WaitForWriteCountAsync( expectedWriteCount );
		transport.Publish(
			Encoding.ASCII.GetBytes(
				"\u001b_Gi=77,p=3;ENOENT:synthetic parent resource missing\u001b\\"
			)
		);
		TerminalControlResult<TerminalRasterPlaceholder> result = await secondCreation;

		Assert.Equal( TerminalControlStatus.Unavailable, result.Status );
		Assert.Equal(
			new TerminalRasterOwnershipState(
				TerminalRasterOwnershipStatus.Stale,
				TerminalRasterOwnershipLossReason.ResourceMissing
			),
			parent.OwnershipState
		);
		Assert.Equal(
			new TerminalRasterOwnershipState(
				TerminalRasterOwnershipStatus.Stale,
				TerminalRasterOwnershipLossReason.ParentPlacementLost
			),
			child.OwnershipState
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
	public async Task VirtualRootCountsAgainstPortableMaximumRelativeDepth() {
		ScriptedTransport transport = new();
		await using TerminalSession session = await OpenSessionAsync( transport );
		TerminalRasterResource resource = await CreateResourceAsync(
			session,
			transport,
			imageId: 77u,
			imageNumber: 1u
		);
		TerminalRasterPlaceholder placeholder = await CreatePlaceholderAsync(
			resource,
			transport,
			imageId: 77u,
			placementId: 1u
		);
		TerminalRasterPlacement current = await CreateVirtualChildAsync(
			resource,
			placeholder,
			transport,
			imageId: 77u,
			placementId: 2u
		);

		uint placementId = 3u;
		for ( int effectiveDepth = 2;
			effectiveDepth <= TerminalPersistentRasterRegistry.MaximumRelativeDepth;
			++effectiveDepth ) {
			current = await CreatePhysicalChildAsync(
				resource,
				current,
				transport,
				imageId: 77u,
				placementId: placementId++
			);
		}

		int baselineWrites = transport.Writes.Count;
		TerminalControlResult<TerminalRasterPlacement> unavailable =
			await resource.CreateRelativePlacementAsync(
				current,
				columnOffset: 0,
				rowOffset: 0
			);

		Assert.Equal( TerminalControlStatus.Unavailable, unavailable.Status );
		Assert.Null( unavailable.Value );
		Assert.Equal( baselineWrites, transport.Writes.Count );
	}

	private static async Task<TerminalRasterPlacement> CreatePhysicalChildAsync(
		TerminalRasterResource resource,
		TerminalRasterPlacement parent,
		ScriptedTransport transport,
		uint imageId,
		uint placementId
	) {
		int expectedWriteCount = transport.Writes.Count + 1;
		Task<TerminalControlResult<TerminalRasterPlacement>> creation =
			resource.CreateRelativePlacementAsync(
				parent,
				columnOffset: 0,
				rowOffset: 0
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

	private static async Task<TerminalRasterPlacement> CreateVirtualChildAsync(
		TerminalRasterResource childResource,
		TerminalRasterPlaceholder parent,
		ScriptedTransport transport,
		uint imageId,
		uint placementId
	) {
		int expectedWriteCount = transport.Writes.Count + 1;
		Task<TerminalControlResult<TerminalRasterPlacement>> creation =
			childResource.CreateRelativePlacementFromPlaceholderAsync(
				parent,
				columnOffset: 0,
				rowOffset: 0
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
