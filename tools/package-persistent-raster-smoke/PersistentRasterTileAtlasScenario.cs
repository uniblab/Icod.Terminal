/*
	Icod.Terminal.PackagePersistentRasterSmoke
	Package smoke-test utility for Icod.Terminal release and compatibility contracts.
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
using System.Diagnostics;
using System.Reflection;
using Icod.Terminal;

internal static class PersistentRasterTileAtlasScenario {
	private static readonly int[] Workloads = [ 1, 4, 16, 64 ];

	internal static void AssertPublicContract() {
		Func<
			TerminalSession,
			TimeSpan,
			CancellationToken,
			ValueTask<TerminalPixelDimensions>
		> queryTerminal = static ( session, timeout, cancellationToken ) =>
			session.QueryTerminalPixelDimensionsAsync( timeout, cancellationToken );
		Func<
			TerminalSession,
			TimeSpan,
			CancellationToken,
			ValueTask<TerminalPixelDimensions>
		> queryCell = static ( session, timeout, cancellationToken ) =>
			session.QueryCellPixelDimensionsAsync( timeout, cancellationToken );
		Func<TerminalSession, TerminalRasterPlanningSnapshot> inspectPlanning =
			static session => session.GetRasterPlanningSnapshot();
		Func<
			TerminalSession,
			TerminalRasterOperation,
			TerminalRasterOperationStatus
		> inspectOperation = static ( session, operation ) =>
			session.InspectRasterOperation( operation );
		_ = queryTerminal;
		_ = queryCell;
		_ = inspectPlanning;
		_ = inspectOperation;

		Require(
			TerminalPixelGeometry.TryDeriveCellDimensions(
				new TerminalDimensions( 80, 25 ),
				new TerminalPixelDimensions( 640, 400 ),
				out TerminalPixelDimensions cell
			) && cell == new TerminalPixelDimensions( 8, 16 ),
			"The package did not expose exact public cell-pixel derivation."
		);
		Require(
			0 == (int)TerminalRasterOperation.FrameComposition
				&& 1 == (int)TerminalRasterOperation.FrameRegionUpdateRgb24
				&& 2 == (int)TerminalRasterOperation.FrameRegionUpdateRgba32,
			"The focused raster-operation values changed."
		);
		Require(
			0 == typeof( TerminalRasterPlanningSnapshot ).GetConstructors(
				BindingFlags.Instance | BindingFlags.Public
			).Length
				&& 0 == typeof( TerminalRasterOperationStatus ).GetConstructors(
					BindingFlags.Instance | BindingFlags.Public
				).Length,
			"Planning and operation status snapshots must remain library constructed."
		);
	}

	internal static async Task RunAsync(
		TerminalSession session,
		TerminalRasterResource resource,
		TerminalRasterAnimationFrame secondFrame,
		PersistentRasterCompositionScenario.ScriptedTerminal transport,
		CancellationToken cancellationToken
	) {
		ArgumentNullException.ThrowIfNull( session );
		ArgumentNullException.ThrowIfNull( resource );
		ArgumentNullException.ThrowIfNull( secondFrame );
		ArgumentNullException.ThrowIfNull( transport );

		TerminalRasterPlanningSnapshot planning = session.GetRasterPlanningSnapshot();
		Require( 16 == resource.PixelWidth && 16 == resource.PixelHeight,
			"The package resource did not retain intrinsic atlas geometry." );
		Require( planning.OwnedResourceCount == 1,
			"The package planning snapshot did not observe the owned atlas." );
		Require( planning.AllocatedAnimationFrameCount == 2,
			"The package planning snapshot did not observe both known frames." );
		Require(
			session.InspectRasterOperation(
				TerminalRasterOperation.FrameRegionUpdateRgba32
			).Support == TerminalCapabilitySupport.Verified,
			"The acknowledged RGBA32 update did not publish focused evidence."
		);

		TerminalRasterAnimation animation = resource.Animation;
		TerminalRasterAnimationFrame front = animation.RootFrame;
		TerminalRasterAnimationFrame back = secondFrame;
		TerminalRasterImage patch = TerminalRasterImage.CreateRgb24(
			1,
			1,
			[ 48, 160, 224 ]
		);
		foreach ( int regionCount in Workloads ) {
			transport.BeginMeasurement();
			long allocatedBefore = GC.GetTotalAllocatedBytes( precise: true );
			using Process process = Process.GetCurrentProcess();
			TimeSpan cpuBefore = process.TotalProcessorTime;
			Stopwatch total = Stopwatch.StartNew();
			Stopwatch firstAcknowledgement = Stopwatch.StartNew();
			for ( int index = 0; index < regionCount; ++index ) {
				TerminalControlMutationResult update = await animation.UpdateFrameRegionAsync(
					back,
					patch,
					index % resource.PixelWidth,
					index / resource.PixelWidth,
					cancellationToken
				);
				Require( update.Succeeded, "A measured tile-region update failed." );
				if ( 0 == index ) firstAcknowledgement.Stop();
			}
			long encodedBytes = checked( transport.EndMeasurement( regionCount ) * regionCount );
			TerminalControlMutationResult selected = await animation.SelectFrameAsync(
				back,
				cancellationToken
			);
			Require( selected.Succeeded, "The measured completed back frame was not selected." );
			total.Stop();
			TimeSpan cpu = process.TotalProcessorTime - cpuBefore;
			long allocated = GC.GetTotalAllocatedBytes( precise: true ) - allocatedBefore;
			Console.WriteLine(
				FormattableString.Invariant(
					$"Tile-atlas workload regions={regionCount}; operations={regionCount + 1}; updatedPixels={regionCount}; encodedBytes={encodedBytes}; firstAckUs={firstAcknowledgement.Elapsed.TotalMicroseconds:F1}; totalMs={total.Elapsed.TotalMilliseconds:F3}; cpuMs={cpu.TotalMilliseconds:F3}; allocatedBytes={allocated}; scriptedAcknowledgement=true; physicalRenderingProven=false"
				)
			);
			(front, back) = (back, front);
		}
		Require(
			session.InspectRasterOperation(
				TerminalRasterOperation.FrameRegionUpdateRgb24
			).Support == TerminalCapabilitySupport.Verified,
			"The measured RGB24 updates did not publish focused evidence."
		);
	}

	private static void Require( bool condition, string message ) {
		if ( !condition ) throw new InvalidOperationException( message );
	}
}
