/*
	Icod.Terminal.PackageCapabilityPlanningSmoke
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
using Icod.Terminal;

TerminalCapability[] capabilities = [
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

if ( 9 != capabilities.Length ) {
	throw new InvalidOperationException( "Unexpected semantic capability count." );
}
if ( TerminalCapabilitySupport.Unknown == TerminalCapabilitySupport.Verified ) {
	throw new InvalidOperationException( "Capability support enum is malformed." );
}
if ( TerminalCapabilityEndpointAvailability.Available
	== TerminalCapabilityEndpointAvailability.Unavailable ) {
	throw new InvalidOperationException( "Endpoint availability enum is malformed." );
}
if ( TerminalCapabilityEvidenceKind.StaticDescription
	== TerminalCapabilityEvidenceKind.LiveObservation ) {
	throw new InvalidOperationException( "Capability evidence enum is malformed." );
}

Type sessionType = typeof( TerminalSession );
Type statusType = typeof( TerminalCapabilityStatus );
if ( null == sessionType.GetMethod(
	nameof( TerminalSession.InspectCapability ),
	[ typeof( TerminalCapability ) ]
) ) {
	throw new InvalidOperationException( "InspectCapability is missing from the package." );
}
if ( null == sessionType.GetMethod(
	nameof( TerminalSession.VerifyCapabilityAsync ),
	[ typeof( TerminalCapability ), typeof( CancellationToken ) ]
) ) {
	throw new InvalidOperationException( "VerifyCapabilityAsync is missing from the package." );
}

foreach ( string propertyName in new[] {
	nameof( TerminalCapabilityStatus.Capability ),
	nameof( TerminalCapabilityStatus.Support ),
	nameof( TerminalCapabilityStatus.EndpointAvailability ),
	nameof( TerminalCapabilityStatus.EvidenceKind ),
	nameof( TerminalCapabilityStatus.IsUsable )
} ) {
	if ( null == statusType.GetProperty( propertyName ) ) {
		throw new InvalidOperationException(
			$"TerminalCapabilityStatus.{propertyName} is missing from the package."
		);
	}
}

Console.WriteLine(
	"Icod.Terminal 1.10 capability-planning package smoke passed."
);
