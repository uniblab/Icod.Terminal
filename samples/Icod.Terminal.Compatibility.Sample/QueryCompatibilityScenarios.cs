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

using Icod.Terminal;

internal interface ICompatibilityQueryClient {
	ValueTask<bool> ObserveAsync(
		string scenarioId,
		CancellationToken cancellationToken
	);
}

internal static class QueryCompatibilityScenarios {
	internal static readonly IReadOnlySet<string> ScenarioIds = new HashSet<string>(
		new[] {
			"query.dimensions",
			"query.device-attributes",
			"query.status-cursor",
			"query.decrqss",
			"query.xtgettcap",
			"query.color",
			"query.pointer-shape"
		},
		StringComparer.Ordinal
	);

	internal static async ValueTask<CompatibilityScenarioObservation> RunAsync(
		string scenarioId,
		ICompatibilityQueryClient client,
		CancellationToken cancellationToken
	) {
		ArgumentNullException.ThrowIfNull( scenarioId );
		ArgumentNullException.ThrowIfNull( client );
		if ( !ScenarioIds.Contains( scenarioId ) ) {
			throw new ArgumentOutOfRangeException( nameof( scenarioId ) );
		}
		cancellationToken.ThrowIfCancellationRequested();
		try {
			bool observed = await client.ObserveAsync(
				scenarioId,
				cancellationToken
			).ConfigureAwait( false );
			return observed
				? new CompatibilityScenarioObservation(
					CompatibilityOutcome.Pass,
					true,
					null,
					"The bounded query returned the scenario's requested typed observation."
				)
				: new CompatibilityScenarioObservation(
					CompatibilityOutcome.Fail,
					false,
					null,
					"The terminal returned an explicit unsupported result for the requested observation."
				);
		} catch ( OperationCanceledException ) when ( cancellationToken.IsCancellationRequested ) {
			throw;
		} catch ( FormatException ) {
			return new CompatibilityScenarioObservation(
				CompatibilityOutcome.Fail,
				false,
				null,
				"The terminal returned a correlated response that did not satisfy the typed response grammar."
			);
		} catch ( Exception exception ) when ( exception is TimeoutException
			or InvalidOperationException
			or IOException
			or EndOfStreamException
			or OperationCanceledException ) {
			return new CompatibilityScenarioObservation(
				CompatibilityOutcome.Inconclusive,
				null,
				null,
				"The environment, endpoint, or bounded response deadline prevented a conclusion."
			);
		}
	}

	internal sealed class TerminalSessionQueryClient : ICompatibilityQueryClient {
		private static readonly TimeSpan QueryTimeout = TimeSpan.FromSeconds( 2 );
		private readonly TerminalSession session;

		internal TerminalSessionQueryClient(
			TerminalSession session
		) {
			ArgumentNullException.ThrowIfNull( session );
			this.session = session;
		}

		public async ValueTask<bool> ObserveAsync(
			string scenarioId,
			CancellationToken cancellationToken
		) {
			cancellationToken.ThrowIfCancellationRequested();
			switch ( scenarioId ) {
				case "query.dimensions":
					_ = await this.session.QueryTerminalPixelDimensionsAsync( QueryTimeout, cancellationToken ).ConfigureAwait( false );
					_ = await this.session.QueryCellPixelDimensionsAsync( QueryTimeout, cancellationToken ).ConfigureAwait( false );
					return true;
				case "query.device-attributes":
					_ = await this.session.QueryPrimaryDeviceAttributesAsync( QueryTimeout, cancellationToken ).ConfigureAwait( false );
					_ = await this.session.QuerySecondaryDeviceAttributesAsync( QueryTimeout, cancellationToken ).ConfigureAwait( false );
					return true;
				case "query.status-cursor":
					_ = await this.session.QueryDeviceStatusAsync( QueryTimeout, cancellationToken ).ConfigureAwait( false );
					_ = await this.session.QueryCursorPositionAsync( QueryTimeout, cancellationToken ).ConfigureAwait( false );
					return true;
				case "query.decrqss":
					return ( await this.session.QueryStatusStringAsync(
						TerminalStatusStringKind.SelectGraphicRendition,
						QueryTimeout,
						cancellationToken
					).ConfigureAwait( false ) ).IsSupported;
				case "query.xtgettcap":
					return ( await this.session.QueryLiveCapabilityAsync(
						"TN",
						QueryTimeout,
						cancellationToken
					).ConfigureAwait( false ) ).IsSupported;
				case "query.color":
					_ = await this.session.QueryPaletteColorAsync( 0, QueryTimeout, cancellationToken ).ConfigureAwait( false );
					_ = await this.session.QueryDynamicColorAsync(
						TerminalDynamicColor.DefaultForeground,
						QueryTimeout,
						cancellationToken
					).ConfigureAwait( false );
					return true;
				case "query.pointer-shape":
					return await this.session.QueryPointerShapeSupportAsync(
						TerminalPointerShape.Text,
						QueryTimeout,
						cancellationToken
					).ConfigureAwait( false );
				default:
					throw new ArgumentOutOfRangeException( nameof( scenarioId ) );
			}
		}
	}
}
