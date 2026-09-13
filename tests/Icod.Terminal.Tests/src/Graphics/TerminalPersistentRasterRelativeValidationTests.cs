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
using System.Threading.Channels;
using Icod.Terminal;
using Icod.TermInfo;
using Xunit;

/// <summary>
/// Defines the T133 pre-output validation contract for relative persistent raster placement.
/// </summary>
public sealed class TerminalPersistentRasterRelativeValidationTests {
	[Fact]
	public async Task NullParentIsRejectedBeforeOutput() {
		RecordingTransport transport = new();
		await using TerminalSession session = await OpenSessionAsync( transport );
		TerminalPersistentRasterRegistry registry = GetRegistry( session );
		TerminalRasterResource resource = ReserveResource(
			session,
			registry
		);
		int baselineWrites = transport.Writes.Count;

		await Assert.ThrowsAsync<ArgumentNullException>(
			async () => await resource.CreateRelativePlacementAsync(
				null!,
				columnOffset: 0,
				rowOffset: 0
			)
		);

		Assert.Equal( baselineWrites, transport.Writes.Count );
	}

	[Fact]
	public async Task DisposedParentIsRejectedBeforeOutput() {
		RecordingTransport transport = new();
		await using TerminalSession session = await OpenSessionAsync( transport );
		TerminalPersistentRasterRegistry registry = GetRegistry( session );
		TerminalRasterResource parentResource = ReserveResource(
			session,
			registry
		);
		TerminalRasterResource childResource = ReserveResource(
			session,
			registry
		);
		TerminalRasterPlacement parent = ReserveOrdinaryPlacement(
			session,
			registry,
			parentResource.State
		);
		await parent.DisposeAsync();
		int baselineWrites = transport.Writes.Count;

		await Assert.ThrowsAsync<ObjectDisposedException>(
			async () => await childResource.CreateRelativePlacementAsync(
				parent,
				columnOffset: 1,
				rowOffset: -1
			)
		);

		Assert.Equal( baselineWrites, transport.Writes.Count );
	}

	[Fact]
	public async Task CrossSessionParentIsRejectedBeforeOutput() {
		RecordingTransport firstTransport = new();
		RecordingTransport secondTransport = new();
		await using TerminalSession firstSession = await OpenSessionAsync( firstTransport );
		await using TerminalSession secondSession = await OpenSessionAsync( secondTransport );
		TerminalPersistentRasterRegistry firstRegistry = GetRegistry( firstSession );
		TerminalPersistentRasterRegistry secondRegistry = GetRegistry( secondSession );
		TerminalRasterResource childResource = ReserveResource(
			firstSession,
			firstRegistry
		);
		TerminalRasterResource parentResource = ReserveResource(
			secondSession,
			secondRegistry
		);
		TerminalRasterPlacement parent = ReserveOrdinaryPlacement(
			secondSession,
			secondRegistry,
			parentResource.State
		);
		int firstBaseline = firstTransport.Writes.Count;
		int secondBaseline = secondTransport.Writes.Count;

		await Assert.ThrowsAsync<ArgumentException>(
			async () => await childResource.CreateRelativePlacementAsync(
				parent,
				columnOffset: 1,
				rowOffset: 1
			)
		);

		Assert.Equal( firstBaseline, firstTransport.Writes.Count );
		Assert.Equal( secondBaseline, secondTransport.Writes.Count );
	}

	[Fact]
	public async Task StaleParentIsUnavailableForParentReasonBeforeOutput() {
		RecordingTransport transport = new();
		await using TerminalSession session = await OpenSessionAsync( transport );
		TerminalPersistentRasterRegistry registry = GetRegistry( session );
		TerminalRasterResource parentResource = ReserveResource(
			session,
			registry
		);
		TerminalRasterResource childResource = ReserveResource(
			session,
			registry
		);
		TerminalRasterPlacement parent = ReserveOrdinaryPlacement(
			session,
			registry,
			parentResource.State
		);
		Assert.True(
			registry.TryReleasePlacement(
				parent.State
			)
		);
		int baselineWrites = transport.Writes.Count;

		TerminalControlResult<TerminalRasterPlacement> result =
			await childResource.CreateRelativePlacementAsync(
				parent,
				columnOffset: 2,
				rowOffset: -3
			);

		Assert.Equal( TerminalControlStatus.Unavailable, result.Status );
		Assert.Null( result.Value );
		Assert.Contains(
			"parent",
			result.Message ?? string.Empty,
			StringComparison.OrdinalIgnoreCase
		);
		Assert.Contains(
			"current",
			result.Message ?? string.Empty,
			StringComparison.OrdinalIgnoreCase
		);
		Assert.Equal( baselineWrites, transport.Writes.Count );
	}

	[Fact]
	public async Task StaleChildResourceIsUnavailableForResourceReasonBeforeOutput() {
		RecordingTransport transport = new();
		await using TerminalSession session = await OpenSessionAsync( transport );
		TerminalPersistentRasterRegistry registry = GetRegistry( session );
		TerminalRasterResource parentResource = ReserveResource(
			session,
			registry
		);
		TerminalRasterResource childResource = ReserveResource(
			session,
			registry
		);
		TerminalRasterPlacement parent = ReserveOrdinaryPlacement(
			session,
			registry,
			parentResource.State
		);
		Assert.True(
			registry.InvalidateResource(
				childResource.State
			)
		);
		int baselineWrites = transport.Writes.Count;

		TerminalControlResult<TerminalRasterPlacement> result =
			await childResource.CreateRelativePlacementAsync(
				parent,
				columnOffset: 2,
				rowOffset: -3
			);

		Assert.Equal( TerminalControlStatus.Unavailable, result.Status );
		Assert.Null( result.Value );
		Assert.Contains(
			"resource",
			result.Message ?? string.Empty,
			StringComparison.OrdinalIgnoreCase
		);
		Assert.Contains(
			"current",
			result.Message ?? string.Empty,
			StringComparison.OrdinalIgnoreCase
		);
		Assert.Equal( baselineWrites, transport.Writes.Count );
	}

	[Fact]
	public async Task DepthNineCreationIsUnavailableBeforeOutput() {
		RecordingTransport transport = new();
		await using TerminalSession session = await OpenSessionAsync( transport );
		TerminalPersistentRasterRegistry registry = GetRegistry( session );
		TerminalRasterResource resource = ReserveResource(
			session,
			registry
		);
		TerminalRasterPlacement parent = ReserveOrdinaryPlacement(
			session,
			registry,
			resource.State
		);

		for ( int depth = 1; depth <= TerminalPersistentRasterRegistry.MaximumRelativeDepth; ++depth ) {
			Assert.True(
				registry.TryReserveRelativePlacement(
					resource.State,
					parent.State,
					columnOffset: depth,
					rowOffset: -depth,
					out TerminalPersistentRasterPlacementState? childState
				)
			);
			Assert.NotNull( childState );
			parent = new TerminalRasterPlacement(
				session,
				childState
			);
		}
		Assert.Equal(
			TerminalPersistentRasterRegistry.MaximumRelativeDepth,
			parent.State.RelativeDepth
		);
		int baselineWrites = transport.Writes.Count;

		TerminalControlResult<TerminalRasterPlacement> result =
			await resource.CreateRelativePlacementAsync(
				parent,
				columnOffset: 0,
				rowOffset: 0
			);

		Assert.Equal( TerminalControlStatus.Unavailable, result.Status );
		Assert.Null( result.Value );
		Assert.Contains(
			"depth",
			result.Message ?? string.Empty,
			StringComparison.OrdinalIgnoreCase
		);
		Assert.Contains(
			TerminalPersistentRasterRegistry.MaximumRelativeDepth.ToString(),
			result.Message ?? string.Empty,
			StringComparison.Ordinal
		);
		Assert.Equal( baselineWrites, transport.Writes.Count );
	}

	[Fact]
	public async Task OrdinaryPlacementRejectsRelativeUpdateBeforeOutput() {
		RecordingTransport transport = new();
		await using TerminalSession session = await OpenSessionAsync( transport );
		TerminalPersistentRasterRegistry registry = GetRegistry( session );
		TerminalRasterResource resource = ReserveResource(
			session,
			registry
		);
		TerminalRasterPlacement placement = ReserveOrdinaryPlacement(
			session,
			registry,
			resource.State
		);
		int baselineWrites = transport.Writes.Count;

		await Assert.ThrowsAsync<InvalidOperationException>(
			async () => await placement.UpdateRelativeAsync(
				columnOffset: 1,
				rowOffset: -1
			)
		);

		Assert.Equal( baselineWrites, transport.Writes.Count );
	}

	[Fact]
	public async Task RelativeCommonUpdatePreservesParentAndOffsetsBeforeTransportIntegration() {
		RecordingTransport transport = new();
		await using TerminalSession session = await OpenSessionAsync( transport );
		TerminalPersistentRasterRegistry registry = GetRegistry( session );
		TerminalRasterResource resource = ReserveResource(
			session,
			registry
		);
		TerminalRasterPlacement parent = ReserveOrdinaryPlacement(
			session,
			registry,
			resource.State
		);
		Assert.True(
			registry.TryReserveRelativePlacement(
				resource.State,
				parent.State,
				columnOffset: -4,
				rowOffset: 5,
				out TerminalPersistentRasterPlacementState? childState
			)
		);
		Assert.NotNull( childState );
		TerminalRasterPlacement child = new(
			session,
			childState
		);
		int baselineWrites = transport.Writes.Count;

		TerminalControlMutationResult result = await child.UpdateAsync(
			new TerminalRasterPlacementOptions {
				Columns = 3,
				Rows = 2,
				ZIndex = -1
			}
		);

		Assert.Equal( TerminalControlStatus.Unavailable, result.Status );
		Assert.Same( parent.State, child.State.Parent );
		Assert.Equal( -4, child.State.ColumnOffset );
		Assert.Equal( 5, child.State.RowOffset );
		Assert.Equal( baselineWrites, transport.Writes.Count );
	}

	[Fact]
	public async Task SignedOffsetExtremaReachTransportBoundaryWithoutMutationOrOutput() {
		RecordingTransport transport = new();
		await using TerminalSession session = await OpenSessionAsync( transport );
		TerminalPersistentRasterRegistry registry = GetRegistry( session );
		TerminalRasterResource resource = ReserveResource(
			session,
			registry
		);
		TerminalRasterPlacement parent = ReserveOrdinaryPlacement(
			session,
			registry,
			resource.State
		);
		int baselineWrites = transport.Writes.Count;

		TerminalControlResult<TerminalRasterPlacement> result =
			await resource.CreateRelativePlacementAsync(
				parent,
				int.MinValue,
				int.MaxValue
			);

		Assert.Equal( TerminalControlStatus.Unavailable, result.Status );
		Assert.Contains(
			"transport",
			result.Message ?? string.Empty,
			StringComparison.OrdinalIgnoreCase
		);
		Assert.Equal( baselineWrites, transport.Writes.Count );
	}

	[Fact]
	public async Task PreCanceledRelativeCreationIsRejectedBeforeOutput() {
		RecordingTransport transport = new();
		await using TerminalSession session = await OpenSessionAsync( transport );
		TerminalPersistentRasterRegistry registry = GetRegistry( session );
		TerminalRasterResource resource = ReserveResource(
			session,
			registry
		);
		TerminalRasterPlacement parent = ReserveOrdinaryPlacement(
			session,
			registry,
			resource.State
		);
		using CancellationTokenSource cancellation = new();
		cancellation.Cancel();
		int baselineWrites = transport.Writes.Count;

		await Assert.ThrowsAnyAsync<OperationCanceledException>(
			async () => await resource.CreateRelativePlacementAsync(
				parent,
				columnOffset: 0,
				rowOffset: 0,
				options: null,
				cancellation.Token
			)
		);

		Assert.Equal( baselineWrites, transport.Writes.Count );
	}

	[Fact]
	public async Task InvalidSourceRectangleIsRejectedBeforeOutput() {
		RecordingTransport transport = new();
		await using TerminalSession session = await OpenSessionAsync( transport );
		TerminalPersistentRasterRegistry registry = GetRegistry( session );
		TerminalRasterResource resource = ReserveResource(
			session,
			registry
		);
		TerminalRasterPlacement parent = ReserveOrdinaryPlacement(
			session,
			registry,
			resource.State
		);
		int baselineWrites = transport.Writes.Count;

		await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
			async () => await resource.CreateRelativePlacementAsync(
				parent,
				columnOffset: 0,
				rowOffset: 0,
				new TerminalRasterPlacementOptions {
					SourceRectangle = default( TerminalRasterSourceRectangle )
				}
			)
		);

		Assert.Equal( baselineWrites, transport.Writes.Count );
	}

	private static TerminalPersistentRasterRegistry GetRegistry(
		TerminalSession session
	) {
		ArgumentNullException.ThrowIfNull( session );
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

	private static TerminalRasterResource ReserveResource(
		TerminalSession session,
		TerminalPersistentRasterRegistry registry
	) {
		ArgumentNullException.ThrowIfNull( session );
		ArgumentNullException.ThrowIfNull( registry );
		Assert.True(
			registry.TryReserveResource(
				sourceWidth: 4,
				sourceHeight: 3,
				out TerminalPersistentRasterResourceState? state
			)
		);
		Assert.NotNull( state );
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
		ArgumentNullException.ThrowIfNull( session );
		ArgumentNullException.ThrowIfNull( registry );
		ArgumentNullException.ThrowIfNull( resource );
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

	private static async ValueTask<TerminalSession> OpenSessionAsync(
		RecordingTransport transport
	) {
		ArgumentNullException.ThrowIfNull( transport );
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
