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

internal static class Program {
	private static readonly TerminalCapability[] Capabilities = [
		TerminalCapability.ClipboardRead,
		TerminalCapability.ClipboardWrite,
		TerminalCapability.CursorStyle,
		TerminalCapability.SynchronizedOutput,
		TerminalCapability.KeyboardReporting,
		TerminalCapability.MouseReporting,
		TerminalCapability.FocusReporting,
		TerminalCapability.BracketedPaste,
		TerminalCapability.RasterGraphics
	];

	private static async Task<int> Main(
		string[] args
	) {
		ArgumentNullException.ThrowIfNull( args );

		bool verify = args.Contains(
			"--verify",
			StringComparer.Ordinal
		);
		await using TerminalSession session = await TerminalSession.OpenAsync();

		Console.WriteLine( "Semantic capability planning:" );
		foreach ( TerminalCapability capability in Capabilities ) {
			TerminalCapabilityStatus status = session.InspectCapability( capability );
			Console.WriteLine( FormatStatus( status ) );
		}

		if ( verify ) {
			Console.WriteLine();
			Console.WriteLine( "Explicit bounded verification:" );
			foreach ( TerminalCapability capability in new[] {
				TerminalCapability.KeyboardReporting,
				TerminalCapability.RasterGraphics
			} ) {
				TerminalCapabilityStatus status = await session.VerifyCapabilityAsync(
					capability
				);
				Console.WriteLine( FormatStatus( status ) );
			}
		}

		TerminalCapabilityStatus raster = session.InspectCapability(
			TerminalCapability.RasterGraphics
		);
		Console.WriteLine();
		Console.WriteLine(
			raster.IsUsable
				? "Plan: raster presentation is presently usable."
				: "Plan: use a non-raster presentation path."
		);
		return 0;
	}

	private static string FormatStatus(
		TerminalCapabilityStatus status
	) {
		ArgumentNullException.ThrowIfNull( status );

		return string.Concat(
			status.Capability,
			": support=",
			status.Support,
			", endpoint=",
			status.EndpointAvailability,
			", evidence=",
			status.EvidenceKind,
			", usable=",
			status.IsUsable
		);
	}
}
