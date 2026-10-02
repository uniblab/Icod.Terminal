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
using Icod.Terminal;
using Icod.TermInfo;
using Xunit;

/// <summary>
/// Freezes the additive bounded raster-planning snapshot contract.
/// </summary>
public sealed class TerminalRasterPlanningSnapshotTests {
	[Fact]
	public async Task FreshSessionPublishesExactCeilingsAndZeroCounts() {
		await using TerminalSession session = await OpenSessionAsync();

		TerminalRasterPlanningSnapshot snapshot = session.GetRasterPlanningSnapshot();

		Assert.Equal( 16_384, snapshot.MaximumImageDimension );
		Assert.Equal( 16 * 1024 * 1024, snapshot.MaximumPixelCount );
		Assert.Equal( 64 * 1024 * 1024, snapshot.MaximumOwnedPixelBytes );
		Assert.Equal( 256, snapshot.MaximumPaletteEntries );
		Assert.Equal( 256, snapshot.MaximumResources );
		Assert.Equal( 4096, snapshot.MaximumPlacements );
		Assert.Equal( 8, snapshot.MaximumRelativePlacementDepth );
		Assert.Equal( 256, snapshot.MaximumPlaceholderExtent );
		Assert.Equal( 4096, snapshot.MaximumAnimationFrames );
		Assert.Equal( 0, snapshot.OwnedResourceCount );
		Assert.Equal( 0, snapshot.OwnedPlacementCount );
		Assert.Equal( 0, snapshot.AllocatedAnimationFrameCount );
	}

	[Fact]
	public void SnapshotConstructionRemainsLibraryOwned() {
		Assert.Empty(
			typeof( TerminalRasterPlanningSnapshot ).GetConstructors(
				BindingFlags.Instance | BindingFlags.Public
			)
		);
		MethodInfo? method = typeof( TerminalSession ).GetMethod(
			nameof( TerminalSession.GetRasterPlanningSnapshot ),
			BindingFlags.Instance | BindingFlags.Public,
			binder: null,
			types: Type.EmptyTypes,
			modifiers: null
		);
		Assert.NotNull( method );
		Assert.Equal( typeof( TerminalRasterPlanningSnapshot ), method.ReturnType );
	}

	[Fact]
	public async Task ConcurrentSnapshotsRemainBoundedAndPerformNoOutput() {
		CountingTerminalOutput output = new();
		await using TerminalSession session = await OpenSessionAsync( output );

		Task[] readers = Enumerable.Range( 0, 8 ).Select(
			_ => Task.Run(
				() => {
					for ( int iteration = 0; iteration < 10_000; ++iteration ) {
						TerminalRasterPlanningSnapshot snapshot =
							session.GetRasterPlanningSnapshot();
						Assert.InRange( snapshot.OwnedResourceCount, 0, snapshot.MaximumResources );
						Assert.InRange( snapshot.OwnedPlacementCount, 0, snapshot.MaximumPlacements );
						Assert.InRange(
							snapshot.AllocatedAnimationFrameCount,
							0,
							snapshot.MaximumAnimationFrames
						);
					}
				}
			)
		).ToArray();

		await Task.WhenAll( readers );
		Assert.Equal( 0, output.WriteCount );
	}

	[Fact]
	public async Task ConcurrentRegistryPlanningCountsRemainCoherentDuringReservationAndRelease() {
		TerminalPersistentRasterRegistry registry = new();
		TaskCompletionSource start = new( TaskCreationOptions.RunContinuationsAsynchronously );
		Task writer = Task.Run(
			async () => {
				await start.Task.ConfigureAwait( false );
				for ( int iteration = 0; iteration < 5_000; ++iteration ) {
					Assert.True( registry.TryReserveResource(
						out TerminalPersistentRasterResourceState? resource
					) );
					Assert.NotNull( resource );
					Assert.True( registry.TryReservePlacement( resource, out _ ) );
					Assert.True( registry.TryReservePlaceholder( resource, 1, 1, out _ ) );
					Assert.True( registry.TryReleaseResource( resource ) );
				}
			}
		);
		Task[] readers = Enumerable.Range( 0, 8 ).Select(
			_ => Task.Run(
				async () => {
					await start.Task.ConfigureAwait( false );
					for ( int iteration = 0; iteration < 20_000; ++iteration ) {
						(int resources, int placements) = registry.CapturePlanningCounts();
						Assert.InRange( resources, 0, 1 );
						Assert.InRange( placements, 0, 2 );
						if ( 0 == resources ) {
							Assert.Equal( 0, placements );
						}
					}
				}
			)
		).ToArray();

		start.SetResult();
		await Task.WhenAll( readers.Append( writer ) );
		Assert.Equal( ( 0, 0 ), registry.CapturePlanningCounts() );
	}

	private static ValueTask<TerminalSession> OpenSessionAsync(
		ITerminalOutput? output = null
	) {
		return TerminalSession.OpenAsync(
			new RecordingTerminalControlProvider(),
			TerminalEndpoint.StandardInput,
			TerminalEndpoint.StandardOutput,
			new EmptyTerminalInput(),
			output ?? new NullTerminalOutput(),
			new TerminalSessionOptions {
				TerminalOverride = TerminalProfiles.Dumb,
				ConfigureOutput = false,
				ObserveLifecycleEvents = false,
				RequireInteractiveOutput = false
			}
		);
	}

	private sealed class CountingTerminalOutput : ITerminalOutput {
		private int writeCount;

		internal int WriteCount => Volatile.Read( ref this.writeCount );

		public ValueTask WriteAsync(
			ReadOnlyMemory<byte> buffer,
			CancellationToken cancellationToken = default
		) {
			cancellationToken.ThrowIfCancellationRequested();
			Interlocked.Increment( ref this.writeCount );
			return ValueTask.CompletedTask;
		}

		public ValueTask FlushAsync( CancellationToken cancellationToken = default ) {
			cancellationToken.ThrowIfCancellationRequested();
			return ValueTask.CompletedTask;
		}
	}

	private sealed class EmptyTerminalInput : ITerminalInput {
		public ValueTask<int> ReadAsync(
			Memory<byte> buffer,
			CancellationToken cancellationToken = default
		) {
			cancellationToken.ThrowIfCancellationRequested();
			return ValueTask.FromResult( 0 );
		}
	}

	private sealed class NullTerminalOutput : ITerminalOutput {
		public ValueTask WriteAsync(
			ReadOnlyMemory<byte> buffer,
			CancellationToken cancellationToken = default
		) {
			cancellationToken.ThrowIfCancellationRequested();
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
}
