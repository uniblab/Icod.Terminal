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
using Xunit;

/// <summary>
/// Defines the T135 cascading relative-placement cleanup and teardown contract.
/// </summary>
public sealed class TerminalPersistentRasterRelativeCleanupTests {
	[Fact]
	public async Task ParentPlacementDisposeDeletesRelativeSubtreeDeepestFirst() {
		RecordingTransport transport = new();
		await using TerminalSession session = await OpenSessionAsync( transport );
		TerminalPersistentRasterRegistry registry = GetRegistry( session );
		RelativeGraph graph = CreateThreeResourceGraph(
			session,
			registry
		);

		await graph.RootPlacement.DisposeAsync();

		Assert.Equal( 3, transport.Writes.Count );
		Assert.Equal(
			DeletePlacementFrame(
				imageId: 99,
				placementId: 3
			),
			transport.Writes[ 0 ]
		);
		Assert.Equal(
			DeletePlacementFrame(
				imageId: 88,
				placementId: 2
			),
			transport.Writes[ 1 ]
		);
		Assert.Equal(
			DeletePlacementFrame(
				imageId: 77,
				placementId: 1
			),
			transport.Writes[ 2 ]
		);
		Assert.True( graph.RootPlacement.State.IsClosed );
		Assert.True( graph.ChildPlacement.State.IsClosed );
		Assert.True( graph.GrandchildPlacement.State.IsClosed );
		Assert.True( registry.IsResourceCurrent( graph.RootResource.State ) );
		Assert.True( registry.IsResourceCurrent( graph.ChildResource.State ) );
		Assert.True( registry.IsResourceCurrent( graph.GrandchildResource.State ) );
	}

	[Fact]
	public async Task RootResourceDisposeUsesOwningImageIdsAndKeepsDescendantResources() {
		RecordingTransport transport = new();
		await using TerminalSession session = await OpenSessionAsync( transport );
		TerminalPersistentRasterRegistry registry = GetRegistry( session );
		RelativeGraph graph = CreateThreeResourceGraph(
			session,
			registry
		);

		await graph.RootResource.DisposeAsync();

		Assert.Equal( 4, transport.Writes.Count );
		Assert.Equal(
			DeletePlacementFrame(
				imageId: 99,
				placementId: 3
			),
			transport.Writes[ 0 ]
		);
		Assert.Equal(
			DeletePlacementFrame(
				imageId: 88,
				placementId: 2
			),
			transport.Writes[ 1 ]
		);
		Assert.Equal(
			DeletePlacementFrame(
				imageId: 77,
				placementId: 1
			),
			transport.Writes[ 2 ]
		);
		Assert.Equal(
			DeleteResourceFrame( imageId: 77 ),
			transport.Writes[ 3 ]
		);

		Assert.False( registry.IsResourceCurrent( graph.RootResource.State ) );
		Assert.True( registry.IsResourceCurrent( graph.ChildResource.State ) );
		Assert.True( registry.IsResourceCurrent( graph.GrandchildResource.State ) );
		Assert.True(
			registry.TryReservePlacement(
				graph.ChildResource.State,
				out TerminalPersistentRasterPlacementState? replacement
			)
		);
		Assert.NotNull( replacement );
		Assert.Equal( 0, replacement.RelativeDepth );
	}

	[Fact]
	public async Task SessionDisposeDeletesPlacementsDeepestFirstBeforeResources() {
		RecordingTransport transport = new();
		TerminalSession session = await OpenSessionAsync( transport );
		TerminalPersistentRasterRegistry registry = GetRegistry( session );
		_ = CreateThreeResourceGraph(
			session,
			registry
		);

		await session.DisposeAsync();

		Assert.Equal( 6, transport.Writes.Count );
		Assert.Equal(
			DeletePlacementFrame(
				imageId: 99,
				placementId: 3
			),
			transport.Writes[ 0 ]
		);
		Assert.Equal(
			DeletePlacementFrame(
				imageId: 88,
				placementId: 2
			),
			transport.Writes[ 1 ]
		);
		Assert.Equal(
			DeletePlacementFrame(
				imageId: 77,
				placementId: 1
			),
			transport.Writes[ 2 ]
		);
		Assert.Equal(
			DeleteResourceFrame( imageId: 77 ),
			transport.Writes[ 3 ]
		);
		Assert.Equal(
			DeleteResourceFrame( imageId: 88 ),
			transport.Writes[ 4 ]
		);
		Assert.Equal(
			DeleteResourceFrame( imageId: 99 ),
			transport.Writes[ 5 ]
		);
	}

	[Fact]
	public async Task InvalidatedGraphDisposalEmitsNoStaleIdentityOutput() {
		RecordingTransport transport = new();
		await using TerminalSession session = await OpenSessionAsync( transport );
		TerminalPersistentRasterRegistry registry = GetRegistry( session );
		RelativeGraph graph = CreateThreeResourceGraph(
			session,
			registry
		);
		registry.Invalidate();

		await graph.RootPlacement.DisposeAsync();
		await graph.RootResource.DisposeAsync();
		await graph.ChildPlacement.DisposeAsync();
		await graph.ChildResource.DisposeAsync();

		Assert.Empty( transport.Writes );
	}

	private static RelativeGraph CreateThreeResourceGraph(
		TerminalSession session,
		TerminalPersistentRasterRegistry registry
	) {
		TerminalRasterResource rootResource = ReserveResource(
			session,
			registry,
			imageId: 77
		);
		TerminalRasterPlacement rootPlacement = ReserveOrdinaryPlacement(
			session,
			registry,
			rootResource.State
		);

		TerminalRasterResource childResource = ReserveResource(
			session,
			registry,
			imageId: 88
		);
		TerminalRasterPlacement childPlacement = ReserveRelativePlacement(
			session,
			registry,
			childResource.State,
			rootPlacement.State,
			columnOffset: 1,
			rowOffset: -1
		);

		TerminalRasterResource grandchildResource = ReserveResource(
			session,
			registry,
			imageId: 99
		);
		TerminalRasterPlacement grandchildPlacement = ReserveRelativePlacement(
			session,
			registry,
			grandchildResource.State,
			childPlacement.State,
			columnOffset: 2,
			rowOffset: -2
		);

		return new RelativeGraph(
			rootResource,
			rootPlacement,
			childResource,
			childPlacement,
			grandchildResource,
			grandchildPlacement
		);
	}

	private static TerminalRasterResource ReserveResource(
		TerminalSession session,
		TerminalPersistentRasterRegistry registry,
		uint imageId
	) {
		ArgumentNullException.ThrowIfNull( session );
		ArgumentNullException.ThrowIfNull( registry );
		if ( 0u == imageId ) {
			throw new ArgumentOutOfRangeException( nameof( imageId ) );
		}

		Assert.True(
			registry.TryReserveResource(
				sourceWidth: 4,
				sourceHeight: 3,
				out TerminalPersistentRasterResourceState? state
			)
		);
		Assert.NotNull( state );
		state.BindImageId( imageId );
		return new TerminalRasterResource(
			session,
			state
		);
	}

	private static TerminalRasterPlacement ReserveOrdinaryPlacement(
		TerminalSession session,
		TerminalPersistentRasterRegistry registry,
		TerminalPersistentRasterResourceState resource
	) {
		Assert.True(
			registry.TryReservePlacement(
				resource,
				out TerminalPersistentRasterPlacementState? state
			)
		);
		Assert.NotNull( state );
		return new TerminalRasterPlacement(
			session,
			state
		);
	}

	private static TerminalRasterPlacement ReserveRelativePlacement(
		TerminalSession session,
		TerminalPersistentRasterRegistry registry,
		TerminalPersistentRasterResourceState resource,
		TerminalPersistentRasterPlacementState parent,
		int columnOffset,
		int rowOffset
	) {
		Assert.True(
			registry.TryReserveRelativePlacement(
				resource,
				parent,
				columnOffset,
				rowOffset,
				out TerminalPersistentRasterPlacementState? state
			)
		);
		Assert.NotNull( state );
		return new TerminalRasterPlacement(
			session,
			state
		);
	}

	private static TerminalPersistentRasterRegistry GetRegistry(
		TerminalSession session
	) {
		FieldInfo field = typeof( TerminalSession ).GetField(
			"persistentRasterRegistry",
			BindingFlags.Instance | BindingFlags.NonPublic
		) ?? throw new InvalidOperationException(
			"The persistent raster registry field could not be resolved."
		);
		return Assert.IsType<TerminalPersistentRasterRegistry>(
			field.GetValue( session )
		);
	}

	private static byte[] DeletePlacementFrame(
		uint imageId,
		uint placementId
	) {
		return Encoding.ASCII.GetBytes(
			$"\u001b_Ga=d,d=i,i={imageId},p={placementId},q=2\u001b\\"
		);
	}

	private static byte[] DeleteResourceFrame(
		uint imageId
	) {
		return Encoding.ASCII.GetBytes(
			$"\u001b_Ga=d,d=I,i={imageId},q=2\u001b\\"
		);
	}

	private static async ValueTask<TerminalSession> OpenSessionAsync(
		RecordingTransport transport
	) {
		return await TerminalSession.OpenAsync(
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
	}

	private sealed record RelativeGraph(
		TerminalRasterResource RootResource,
		TerminalRasterPlacement RootPlacement,
		TerminalRasterResource ChildResource,
		TerminalRasterPlacement ChildPlacement,
		TerminalRasterResource GrandchildResource,
		TerminalRasterPlacement GrandchildPlacement
	);

	private sealed class RecordingTransport : ITerminalInput, ITerminalOutput {
		private readonly Channel<byte[]> input = Channel.CreateUnbounded<byte[]>(
			new UnboundedChannelOptions {
				SingleReader = true,
				SingleWriter = false,
				AllowSynchronousContinuations = false
			}
		);
		private readonly object synchronization = new();
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
			return ValueTask.CompletedTask;
		}

		public ValueTask FlushAsync(
			CancellationToken cancellationToken = default
		) {
			cancellationToken.ThrowIfCancellationRequested();
			return ValueTask.CompletedTask;
		}
	}
}
