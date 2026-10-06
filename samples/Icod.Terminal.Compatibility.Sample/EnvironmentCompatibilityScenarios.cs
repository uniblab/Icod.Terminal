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

/// <summary>Consented public-only appearance and resize observations with bounded cleanup.</summary>
internal static class EnvironmentCompatibilityScenarios {
	private static readonly TimeSpan QueryTimeout = TimeSpan.FromSeconds(2);
	private static readonly TimeSpan ObservationTimeout = TimeSpan.FromSeconds(45);
	internal static async ValueTask<CompatibilityScenarioObservation> RunAsync(
		TerminalSession session, ICompatibilityLiveClient client, string scenarioId, CancellationToken cancellationToken
	) {
		ArgumentNullException.ThrowIfNull(session); ArgumentNullException.ThrowIfNull(client);
		if (scenarioId is not ("environment.appearance-reporting" or "environment.in-band-resize")) { throw new ArgumentOutOfRangeException(nameof(scenarioId)); }
		using CancellationTokenSource duration = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
		duration.CancelAfter(TimeSpan.FromMinutes(2));
		try {
			CompatibilityConfirmation consent = await InteractiveConfirmation.RequestAsync(client,
				$"Run {scenarioId}? This temporarily acquires terminal reporting and waits for an operator change.", duration.Token).ConfigureAwait(false);
			if (consent != CompatibilityConfirmation.Yes) { return new(consent == CompatibilityConfirmation.No ? CompatibilityOutcome.NotRun : CompatibilityOutcome.Inconclusive, null, null, "Reporting was not acquired because consent was declined or unavailable."); }
			return scenarioId == "environment.appearance-reporting"
				? await AppearanceAsync(session, client, duration.Token).ConfigureAwait(false)
				: await ResizeAsync(session, client, duration.Token).ConfigureAwait(false);
		}
		catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
		catch (CompatibilityCleanupException) { return new(CompatibilityOutcome.Fail, false, null, "Environment-reporting cleanup failed; remote restoration is uncertain."); }
		catch (FormatException) { return new(CompatibilityOutcome.Fail, false, null, "A correlated response failed typed environment grammar validation."); }
		catch (Exception exception) when (exception is TimeoutException or InvalidOperationException or IOException or OperationCanceledException) {
			return new(CompatibilityOutcome.Inconclusive, null, null, "The endpoint or bounded observation deadline prevented a conclusion.");
		}
	}
	private static async ValueTask<CompatibilityScenarioObservation> AppearanceAsync(TerminalSession session, ICompatibilityLiveClient client, CancellationToken token) {
		var result = await session.AcquireAppearanceReportingAsync(QueryTimeout, token).ConfigureAwait(false);
		if (!result.IsAvailable) { return Unavailable(); }
		TerminalAppearanceReportingLease lease = result.GetRequiredValue();
		try {
			await client.WritePromptAsync("Change the terminal's dark/light appearance or palette now. Waiting up to 45 seconds.\r\n", token).ConfigureAwait(false);
			TerminalSemanticEvent? report = await ObserveAsync(client, static semantic => semantic.Kind == TerminalSemanticEventKind.Appearance, token).ConfigureAwait(false);
			if (report is null) { return Missing("No appearance report was observed after the change prompt."); }
			return await ConfirmAsync(client, $"Typed appearance observation: {report.Appearance!.Appearance}; repeated values remain palette observations.", token).ConfigureAwait(false);
		}
		finally { await CleanupAsync(lease).ConfigureAwait(false); }
	}
	private static async ValueTask<CompatibilityScenarioObservation> ResizeAsync(TerminalSession session, ICompatibilityLiveClient client, CancellationToken token) {
		var result = await session.AcquireInBandResizeReportingAsync(QueryTimeout, token).ConfigureAwait(false);
		if (!result.IsAvailable) { return Unavailable(); }
		TerminalInBandResizeReportingLease lease = result.GetRequiredValue();
		try {
			TerminalInBandResizeEvent? initial = (await ObserveAsync(client, static semantic => semantic.Kind == TerminalSemanticEventKind.InBandResize, token).ConfigureAwait(false))?.InBandResize;
			if (initial is null) { return Missing("The required initial in-band resize report was not observed."); }
			await client.WritePromptAsync("Resize the terminal now. Waiting up to 45 seconds for changed character or pixel dimensions.\r\n", token).ConfigureAwait(false);
			TerminalSemanticEvent? changed = await ObserveAsync(client, semantic => semantic.InBandResize is { } resize
				&& (resize.Dimensions != initial.Dimensions || !Nullable.Equals(resize.PixelDimensions, initial.PixelDimensions)), token).ConfigureAwait(false);
			if (changed is null) { return Missing("An initial in-band report arrived, but changed dimensions were not observed."); }
			return await ConfirmAsync(client, "Initial and changed typed in-band dimensions were observed; native geometry retains its separate provenance.", token).ConfigureAwait(false);
		}
		finally { await CleanupAsync(lease).ConfigureAwait(false); }
	}
	private static async ValueTask<TerminalSemanticEvent?> ObserveAsync(ICompatibilityLiveClient client, Func<TerminalSemanticEvent, bool> matches, CancellationToken token) {
		long started = System.Diagnostics.Stopwatch.GetTimestamp();
		while (true) {
			TimeSpan remaining = ObservationTimeout - System.Diagnostics.Stopwatch.GetElapsedTime(started);
			if (remaining <= TimeSpan.Zero) { return null; }
			TerminalEvent item = await client.ReadEventAsync(remaining, token).ConfigureAwait(false);
			if (item.Kind is TerminalEventKind.Timeout or TerminalEventKind.Cancelled || item.Input?.Kind == TerminalInputEventKind.EndOfInput) { return null; }
			if (item.Input?.Key == TerminalKey.Escape || item.Input?.Character?.Value is 'q' or 'Q') { return null; }
			if (item.Semantic is { } semantic && matches(semantic)) { return semantic; }
		}
	}
	private static async ValueTask<CompatibilityScenarioObservation> ConfirmAsync(ICompatibilityLiveClient client, string note, CancellationToken token) {
		CompatibilityConfirmation observed = await InteractiveConfirmation.RequestAsync(client, "Did you perform and observe the announced terminal change?", token).ConfigureAwait(false);
		return new(observed == CompatibilityConfirmation.Yes ? CompatibilityOutcome.Pass : observed == CompatibilityConfirmation.No ? CompatibilityOutcome.Fail : CompatibilityOutcome.Inconclusive,
			true, observed == CompatibilityConfirmation.Unavailable ? null : observed == CompatibilityConfirmation.Yes, note);
	}
	private static CompatibilityScenarioObservation Unavailable() => new(CompatibilityOutcome.Unavailable, false, null, "The terminal explicitly reported mode state 0 or 4; native input and geometry retain their existing behavior.");
	private static CompatibilityScenarioObservation Missing(string note) => new(CompatibilityOutcome.Inconclusive, false, null, note);
	private static async ValueTask CleanupAsync(IAsyncDisposable lease) {
		try { await lease.DisposeAsync().ConfigureAwait(false); }
		catch (Exception exception) { throw new CompatibilityCleanupException("Environment reporting could not be restored.", exception); }
	}
}
