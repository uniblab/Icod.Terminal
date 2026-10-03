/*
	Icod.Terminal
	Managed, cross-platform live-terminal session and terminal-control library for .NET.
	Copyright (C) 2026  Timothy J. Bruce <uniblab@hotmail.com>
*/

/*
	This program is free software: you can redistribute it and/or modify
	it under the terms of the GNU Lesser General Public License as published by
	the Free Software Foundation, either version 3 of the License, or
	(at your option) any later version.

	This program is distributed in the hope that it will be useful,
	but WITHOUT ANY WARRANTY; without even the implied warranty of
	MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
	GNU Lesser General Public License for more details.

	You should have received a copy of the GNU Lesser General Public License
	along with this program.  If not, see <https://www.gnu.org/licenses/>.
*/
namespace Icod.Terminal;

/// <summary>Projects focused persistent-raster operation evidence.</summary>
public sealed partial class TerminalSession {
	/// <summary>Inspects one focused raster operation without terminal I/O or probing.</summary>
	/// <exception cref="ArgumentOutOfRangeException">
	/// <paramref name="operation"/> is not recognized.
	/// </exception>
	public TerminalRasterOperationStatus InspectRasterOperation(
		TerminalRasterOperation operation
	) {
		TerminalSemanticOperation semanticOperation = MapRasterOperation( operation );
		TerminalCapabilityResolution resolution = this.GetSemanticCapabilityEvidence().Resolve(
			TerminalCapabilitySubject.ForSemanticOperation( semanticOperation )
		);
		TerminalCapabilitySupport support = MapSupport( resolution.State );
		bool endpointAvailable = this.OutputObservation.IsTerminal;
		return new TerminalRasterOperationStatus(
			operation,
			support,
			endpointAvailable
				? TerminalCapabilityEndpointAvailability.Available
				: TerminalCapabilityEndpointAvailability.Unavailable,
			MapEvidenceKind( resolution.EvidenceSource ),
			endpointAvailable && TerminalCapabilitySupport.Unsupported != support
		);
	}

	private static TerminalSemanticOperation MapRasterOperation(
		TerminalRasterOperation operation
	) {
		return operation switch {
			TerminalRasterOperation.FrameComposition
				=> TerminalSemanticOperation.RasterFrameComposition,
			TerminalRasterOperation.FrameRegionUpdateRgb24
				=> TerminalSemanticOperation.RasterFrameRegionUpdateRgb24,
			TerminalRasterOperation.FrameRegionUpdateRgba32
				=> TerminalSemanticOperation.RasterFrameRegionUpdateRgba32,
			_ => throw new ArgumentOutOfRangeException(
				nameof( operation ),
				operation,
				"The raster operation is not recognized."
			)
		};
	}
}
