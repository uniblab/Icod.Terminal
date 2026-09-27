/*
	Icod.Terminal.CapabilityPlanning.Sample
	Sample application demonstrating Icod.Terminal CapabilityPlanning features.
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
namespace Icod.Terminal.CapabilityPlanning.Sample;

using Icod.Terminal;

internal static class CapabilityPlanningExample {
	internal static async ValueTask<int> RunAsync(
		string[] args, TextWriter output, TextWriter error, CancellationToken cancellationToken = default
	) {
		ArgumentNullException.ThrowIfNull( args );
		foreach ( string argument in args ) {
			if ( argument is not ("--help" or "-h" or "--verify") ) {
				await error.WriteLineAsync( $"Unknown argument: {argument}. Use --help." );
				return 2;
			}
		}
		if ( args.Contains( "--help" ) || args.Contains( "-h" ) ) {
			await output.WriteLineAsync( "Usage: CapabilityPlanning.Sample [--verify] [--help|-h]\nDefault: static profile and capability inspection; no support probes.\n--verify: existing keyboard, raster, and persistent-raster support queries.\nInteractive terminal required except for help. Ctrl+C cancels and restores the session." );
			return 0;
		}
		try {
			await using TerminalSession session = await TerminalSession.OpenAsync( cancellationToken: cancellationToken );
			await WriteReportAsync( session, output, args.Contains( "--verify" ), cancellationToken );
			return 0;
		} catch ( OperationCanceledException ) when ( cancellationToken.IsCancellationRequested ) {
			await error.WriteLineAsync( "Cancelled; terminal session cleanup completed." );
			return 130;
		} catch ( Exception exception ) {
			await error.WriteLineAsync( $"Capability report failed: {exception.Message}" );
			return 1;
		}
	}

	internal static async ValueTask WriteReportAsync(
		TerminalSession session, TextWriter report, bool verify, CancellationToken cancellationToken = default
	) {
		ArgumentNullException.ThrowIfNull( session );
		ArgumentNullException.ThrowIfNull( report );
		cancellationToken.ThrowIfCancellationRequested();
		TerminalScreenCapabilities screen = session.Profile.Screen;
		await report.WriteLineAsync( "Static screen advertisement (presence, not a planning guarantee):" );
		await report.WriteLineAsync( $"Cursor: absolute={screen.SupportsAbsoluteCursorAddressing}, home={screen.AdvertisesCursorHome}, row={screen.AdvertisesCursorRowAddressing}, column={screen.AdvertisesCursorColumnAddressing}, carriage-return={screen.AdvertisesCarriageReturn}" );
		await report.WriteLineAsync( $"Directions: up={screen.AdvertisesCursorUp}, down={screen.AdvertisesCursorDown}, left={screen.AdvertisesCursorLeft}, right={screen.AdvertisesCursorRight}; scroll-region={screen.AdvertisesScrollRegion}" );
		foreach ( TerminalScreenEraseKind kind in Enum.GetValues<TerminalScreenEraseKind>() ) {
			await report.WriteLineAsync( $"Erase {kind}: advertised={screen.AdvertisesErase( kind )}" );
		}
		foreach ( TerminalScreenCharacterShiftKind kind in Enum.GetValues<TerminalScreenCharacterShiftKind>() ) {
			await report.WriteLineAsync( $"Character shift {kind}: advertised={screen.AdvertisesCharacterShift( kind )}" );
		}
		foreach ( TerminalScreenLineShiftKind kind in Enum.GetValues<TerminalScreenLineShiftKind>() ) {
			await report.WriteLineAsync( $"Line shift {kind}: advertised={screen.AdvertisesLineShift( kind )}" );
		}
		// Ask the planner even when absolute addressing is absent. No demonstration plan is committed.
		await DescribePlanAsync( "Cursor to (2,3), current unknown", () => session.Screen.PlanCursorMove( null, new TerminalScreenPosition( 2, 3 ) ) );
		await DescribePlanAsync( "Erase to end of line", () => session.Screen.PlanErase( TerminalScreenEraseKind.ToEndOfLine ) );
		await DescribePlanAsync( "Insert one character", () => session.Screen.PlanCharacterShift( TerminalScreenCharacterShiftKind.Insert, 1 ) );
		await DescribePlanAsync( "Insert one line", () => session.Screen.PlanLineShift( TerminalScreenLineShiftKind.Insert, 1, 1 ) );
		await DescribePlanAsync( "Scroll region rows 0..3", () => session.Screen.PlanScrollRegion( 0, 3, 4 ) );
		await report.WriteLineAsync( "Current capability snapshots:" );
		await WriteStatusesAsync();
		if ( verify ) {
			await report.WriteLineAsync( "Explicit verification: existing per-query deadlines; no end-to-end one-second promise." );
			foreach ( TerminalCapability capability in new[] {
				TerminalCapability.KeyboardReporting, TerminalCapability.RasterGraphics, TerminalCapability.PersistentRasterGraphics
			} ) {
				cancellationToken.ThrowIfCancellationRequested();
				TerminalCapabilityStatus status = await session.VerifyCapabilityAsync( capability, cancellationToken );
				await report.WriteLineAsync( "Verified result: " + FormatStatus( status ) );
			}
			await report.WriteLineAsync( "Re-inspected current snapshots:" );
			await WriteStatusesAsync();
		}
		await report.WriteLineAsync( session.InspectCapability( TerminalCapability.RasterGraphics ).IsUsable
			? "Fallback decision: raster is presently usable; plan the requested operation separately."
			: "Fallback decision: use text or another non-raster presentation path." );

		async ValueTask WriteStatusesAsync() {
			foreach ( TerminalCapability capability in Enum.GetValues<TerminalCapability>() ) {
				cancellationToken.ThrowIfCancellationRequested();
				await report.WriteLineAsync( FormatStatus( session.InspectCapability( capability ) ) );
			}
		}
		async ValueTask DescribePlanAsync( string label, Func<TerminalScreenOperationPlan?> create ) {
			try {
				TerminalScreenOperationPlan? plan = create();
				await report.WriteLineAsync( $"Concrete plan — {label}: " + (plan.HasValue ? $"available, {plan.Value.ByteCount} bytes" : "unavailable") );
			} catch ( FormatException exception ) {
				await report.WriteLineAsync( $"Concrete plan — {label}: rejected ({exception.Message})" );
			}
		}
	}

	private static string FormatStatus( TerminalCapabilityStatus status ) =>
		$"{status.Capability}: support={status.Support}, endpoint={status.EndpointAvailability}, evidence={status.EvidenceKind}, usable={status.IsUsable}";
}