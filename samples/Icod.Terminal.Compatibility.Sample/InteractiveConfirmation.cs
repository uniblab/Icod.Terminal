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

internal enum CompatibilityConfirmation {
	Yes,
	No,
	Unavailable
}

internal sealed record CompatibilityLiveExecution(
	bool AutomatedObservation,
	bool RequiresOperatorObservation,
	string Note,
	bool ExplicitFailure = false
);

internal interface ICompatibilityLiveClient {
	ValueTask WritePromptAsync(
		string prompt,
		CancellationToken cancellationToken
	);

	ValueTask<TerminalEvent> ReadEventAsync(
		TimeSpan timeout,
		CancellationToken cancellationToken
	);

	ValueTask<CompatibilityLiveExecution> ExecuteAsync(
		string scenarioId,
		CancellationToken cancellationToken
	);
}

internal sealed class CompatibilityCleanupException : Exception {
	internal CompatibilityCleanupException(
		string message,
		Exception innerException
	) : base( message, innerException ) { }
}

internal static class InteractiveConfirmation {
	private static readonly TimeSpan ConfirmationTimeout = TimeSpan.FromMinutes( 2 );

	internal static async ValueTask<CompatibilityConfirmation> RequestAsync(
		ICompatibilityLiveClient client,
		string prompt,
		CancellationToken cancellationToken
	) {
		ArgumentNullException.ThrowIfNull( client );
		ArgumentException.ThrowIfNullOrEmpty( prompt );
		await client.WritePromptAsync(
			$"{prompt} [y/N]\r\n",
			cancellationToken
		).ConfigureAwait( false );

		while ( true ) {
			TerminalEvent terminalEvent = await client.ReadEventAsync(
				ConfirmationTimeout,
				cancellationToken
			).ConfigureAwait( false );
			if ( terminalEvent.Kind is TerminalEventKind.Timeout or TerminalEventKind.Cancelled ) {
				return CompatibilityConfirmation.Unavailable;
			}
			if ( terminalEvent.Kind != TerminalEventKind.Input
				|| terminalEvent.Input is not { } input ) {
				continue;
			}
			if ( input.Kind == TerminalInputEventKind.EndOfInput ) {
				return CompatibilityConfirmation.Unavailable;
			}
			if ( input.Kind is not ( TerminalInputEventKind.Text or TerminalInputEventKind.Key )
				|| !input.Character.HasValue ) {
				if ( input.Kind == TerminalInputEventKind.Key && input.Key == TerminalKey.Enter ) {
					return CompatibilityConfirmation.No;
				}
				continue;
			}

			char value = char.ToLowerInvariant( (char)input.Character.Value.Value );
			if ( 'y' == value ) {
				return CompatibilityConfirmation.Yes;
			}
			if ( 'n' == value || '\r' == value || '\n' == value ) {
				return CompatibilityConfirmation.No;
			}
		}
	}
}

internal static class LiveCompatibilityScenarios {
	internal static async ValueTask<CompatibilityScenarioObservation> RunAsync(
		string scenarioId,
		ICompatibilityLiveClient client,
		CancellationToken cancellationToken
	) {
		ArgumentException.ThrowIfNullOrEmpty( scenarioId );
		ArgumentNullException.ThrowIfNull( client );

		CompatibilityConfirmation consent = await InteractiveConfirmation.RequestAsync(
			client,
			$"Run {scenarioId}? This may change terminal state or wait for live input.",
			cancellationToken
		).ConfigureAwait( false );
		if ( CompatibilityConfirmation.No == consent ) {
			return new CompatibilityScenarioObservation(
				CompatibilityOutcome.NotRun,
				null,
				null,
				"The operator declined this live scenario."
			);
		}
		if ( CompatibilityConfirmation.Unavailable == consent ) {
			return new CompatibilityScenarioObservation(
				CompatibilityOutcome.Inconclusive,
				null,
				null,
				"Consent was not received before input ended or timed out."
			);
		}

		CompatibilityLiveExecution execution;
		try {
			execution = await client.ExecuteAsync(
				scenarioId,
				cancellationToken
			).ConfigureAwait( false );
		} catch ( CompatibilityCleanupException exception ) {
			return new CompatibilityScenarioObservation(
				CompatibilityOutcome.Fail,
				false,
				null,
				$"Cleanup failed: {exception.Message}"
			);
		} catch ( FormatException exception ) {
			return new CompatibilityScenarioObservation(
				CompatibilityOutcome.Fail,
				false,
				null,
				$"The terminal returned a malformed correlated response: {exception.GetType().Name}."
			);
		} catch ( Exception exception ) when (
			exception is TimeoutException
				or UnauthorizedAccessException
				or NotSupportedException
				or InvalidOperationException
				or IOException
				or EndOfStreamException
		) {
			return new CompatibilityScenarioObservation(
				CompatibilityOutcome.Inconclusive,
				false,
				null,
				$"The live observation was unavailable: {exception.GetType().Name}."
			);
		}

		if ( !execution.RequiresOperatorObservation ) {
			return new CompatibilityScenarioObservation(
				execution.ExplicitFailure
					? CompatibilityOutcome.Fail
					: execution.AutomatedObservation
					? CompatibilityOutcome.Pass
					: CompatibilityOutcome.Inconclusive,
				execution.AutomatedObservation,
				null,
				execution.Note
			);
		}

		CompatibilityConfirmation observed = await InteractiveConfirmation.RequestAsync(
			client,
			"Did you observe the announced terminal behavior?",
			cancellationToken
		).ConfigureAwait( false );
		return observed switch {
			CompatibilityConfirmation.Yes => new CompatibilityScenarioObservation(
				CompatibilityOutcome.Pass,
				execution.AutomatedObservation,
				true,
				execution.Note
			),
			CompatibilityConfirmation.No => new CompatibilityScenarioObservation(
				CompatibilityOutcome.Fail,
				execution.AutomatedObservation,
				false,
				execution.Note
			),
			_ => new CompatibilityScenarioObservation(
				CompatibilityOutcome.Inconclusive,
				execution.AutomatedObservation,
				null,
				execution.Note
			)
		};
	}
}

internal sealed class TerminalSessionCompatibilityLiveClient : ICompatibilityLiveClient {
	private readonly TerminalSession session;

	internal TerminalSessionCompatibilityLiveClient(
		TerminalSession session
	) {
		ArgumentNullException.ThrowIfNull( session );
		this.session = session;
	}

	public ValueTask WritePromptAsync(
		string prompt,
		CancellationToken cancellationToken
	) => this.session.WriteTextAsync( prompt, cancellationToken );

	public ValueTask<TerminalEvent> ReadEventAsync(
		TimeSpan timeout,
		CancellationToken cancellationToken
	) => this.session.ReadEventAsync( timeout, cancellationToken );

	public ValueTask<CompatibilityLiveExecution> ExecuteAsync(
		string scenarioId,
		CancellationToken cancellationToken
	) => scenarioId switch {
		"metadata.titles-location" or
		"semantic.hyperlinks-regions" or
		"semantic.vscode-shell" or
		"semantic.iterm2-shell" => MetadataCompatibilityScenarios.ExecuteAsync(
			this.session, scenarioId, cancellationToken
		),
		"notifications" or "progress" => NotificationCompatibilityScenarios.ExecuteAsync(
			this.session, scenarioId, cancellationToken
		),
		"clipboard.osc52" => ClipboardCompatibilityScenarios.ExecuteAsync(
			this.session, cancellationToken
		),
		"input.text-key" or
		"input.paste-focus-mouse" or
		"input.modern-keyboard" or
		"lifecycle.resize-suspend" => InputCompatibilityScenarios.ExecuteAsync(
			this.session, scenarioId, cancellationToken
		),
		"presentation.cursor-sync" or
		"presentation.pointer-colors" => PresentationCompatibilityScenarios.ExecuteAsync(
			this.session, scenarioId, cancellationToken
		),
		_ => throw new ArgumentException( "Unknown live scenario.", nameof( scenarioId ) )
	};
}
