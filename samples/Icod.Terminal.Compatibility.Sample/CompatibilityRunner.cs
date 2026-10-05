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

using System.Reflection;
using Icod.Terminal;

internal static class CompatibilityRunner {
	internal static async ValueTask<IReadOnlyList<CompatibilityEvidence>> RunAsync(
		TerminalSession session,
		IEnumerable<CompatibilityScenario> scenarios,
		CompatibilityRunIdentity identity,
		TextWriter output,
		CancellationToken cancellationToken
	) {
		ArgumentNullException.ThrowIfNull( session );
		ArgumentNullException.ThrowIfNull( scenarios );
		ArgumentNullException.ThrowIfNull( identity );
		ArgumentNullException.ThrowIfNull( output );
		var context = new CompatibilityExecutionContext(
			session,
			new QueryCompatibilityScenarios.TerminalSessionQueryClient( session ),
			new TerminalSessionCompatibilityLiveClient( session ),
			identity,
			output
		);
		var evidence = new List<CompatibilityEvidence>();
		foreach ( CompatibilityScenario scenario in scenarios ) {
			cancellationToken.ThrowIfCancellationRequested();
			await output.WriteLineAsync( $"{scenario.Id}/v{scenario.Revision}: {scenario.Description}" );
			await output.WriteLineAsync( $"Success condition: {scenario.SuccessCondition}" );
			CompatibilityScenarioObservation observation = await scenario.ExecuteAsync(
				context,
				cancellationToken
			).ConfigureAwait( false );
			CompatibilityEvidence item = new(
				"1",
				GetPackageVersion(),
				identity.SourceCommit,
				identity.TerminalId,
				identity.TerminalVersion,
				identity.OperatingSystem,
				identity.OperatingSystemVersion,
				identity.Transport,
				identity.TransportVersion,
				scenario.Id,
				scenario.Revision,
				DateTimeOffset.UtcNow,
				observation.Outcome,
				observation.AutomatedObservation,
				observation.OperatorObservation,
				observation.Note
			);
			CompatibilityEvidenceValidator.Validate( item );
			evidence.Add( item );
			await output.WriteLineAsync( $"Outcome: {observation.Outcome}" );
		}
		return evidence.AsReadOnly();
	}

	private static string GetPackageVersion() {
		Assembly assembly = typeof( TerminalSession ).Assembly;
		string? informational = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()
			?.InformationalVersion;
		if ( !string.IsNullOrWhiteSpace( informational ) ) {
			return informational.Split( '+', 2 )[ 0 ];
		}
		return assembly.GetName().Version?.ToString( 3 )
			?? throw new InvalidOperationException( "The Icod.Terminal assembly version is unavailable." );
	}
}
