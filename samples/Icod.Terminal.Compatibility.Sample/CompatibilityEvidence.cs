/*
	Icod.Terminal.Compatibility.Sample
	Sample application demonstrating Icod.Terminal Compatibility features.
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
namespace Icod.Terminal.Compatibility.Sample;

/// <summary>Immutable, bounded evidence for one scenario in one exact environment.</summary>
internal sealed record CompatibilityEvidence(
	string SchemaVersion,
	string TerminalPackageVersion,
	string SourceCommit,
	string TerminalId,
	string TerminalVersion,
	string OperatingSystem,
	string OperatingSystemVersion,
	string? Transport,
	string? TransportVersion,
	string ScenarioId,
	int ScenarioRevision,
	DateTimeOffset ObservedAtUtc,
	CompatibilityOutcome Outcome,
	bool? AutomatedObservation,
	bool? OperatorObservation,
	string Note
);
