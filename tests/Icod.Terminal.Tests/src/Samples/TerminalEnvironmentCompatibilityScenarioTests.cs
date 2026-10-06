/*
	Icod.Terminal.Tests
	Automated test suite for the Icod.Terminal library.
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
namespace Icod.Terminal.Tests.Samples;

using System.Text;
using Icod.Terminal;
using Icod.TermInfo;
using Icod.Terminal.Compatibility.Sample;
using Icod.Terminal.Tests.Session;
using Xunit;

/// <summary>Detects missing consent, premature operator prompts and incorrect evidence outcomes.</summary>
public sealed class TerminalEnvironmentCompatibilityScenarioTests {
	[Theory]
	[InlineData("query.appearance", false)]
	[InlineData("environment.appearance-reporting", true)]
	[InlineData("environment.in-band-resize", true)]
	public void CatalogPublishesRevisionOneWithCorrectConsent(string id, bool sideEffects) {
		CompatibilityScenario scenario = Assert.IsType<CompatibilityScenario>(CompatibilityScenarioCatalog.Find(id));
		Assert.Equal(1, scenario.Revision); Assert.Equal(sideEffects, scenario.HasExternalSideEffects);
		Assert.False(string.IsNullOrWhiteSpace(scenario.Description)); Assert.False(string.IsNullOrWhiteSpace(scenario.SuccessCondition));
	}
	[Theory]
	[InlineData("environment.appearance-reporting")]
	[InlineData("environment.in-band-resize")]
	public async Task DecliningConsentNeverNegotiates(string id) {
		EnvironmentTestContext wire = new(); await using TerminalSession session = await wire.OpenAsync(false);
		PromptClient client = new(TerminalEvent.FromInput(TerminalInputEvent.FromText(new Rune('n'))));
		CompatibilityScenarioObservation result = await EnvironmentCompatibilityScenarios.RunAsync(session, client, id, CancellationToken.None);
		Assert.Equal(CompatibilityOutcome.NotRun, result.Outcome); Assert.Empty(wire.Writes); Assert.Single(client.Prompts);
	}
	[Theory]
	[InlineData("environment.appearance-reporting")]
	[InlineData("environment.in-band-resize")]
	public async Task ExplicitUnavailableDoesNotPromptForChange(string id) {
		EnvironmentTestContext wire = new() { AppearanceState = 0, ResizeState = 4 };
		await using TerminalSession session = await wire.OpenAsync(false);
		PromptClient client = new(Yes());
		CompatibilityScenarioObservation result = await EnvironmentCompatibilityScenarios.RunAsync(session, client, id, CancellationToken.None);
		Assert.Equal(CompatibilityOutcome.Unavailable, result.Outcome); Assert.Single(client.Prompts);
		Assert.Single(wire.Writes); Assert.Equal(new TerminalSize(80, 24), session.GetSize().GetRequiredValue());
	}
	[Fact]
	public async Task AppearanceReportRequiresObservationAndOperatorConfirmation() {
		EnvironmentTestContext wire = new(); await using TerminalSession session = await wire.OpenAsync(false);
		PromptClient client = new(Yes(), TerminalEvent.FromSemantic(TerminalSemanticEvent.FromAppearance(new(TerminalAppearance.Dark))), Yes());
		client.OnPrompt = prompt => { if (prompt.Contains("Change", StringComparison.Ordinal)) { Assert.Contains("\u001b[?2031h", wire.Writes); } };
		CompatibilityScenarioObservation result = await EnvironmentCompatibilityScenarios.RunAsync(session, client, "environment.appearance-reporting", CancellationToken.None);
		Assert.Equal(CompatibilityOutcome.Pass, result.Outcome); Assert.True(result.AutomatedObservation); Assert.True(result.OperatorObservation);
		Assert.Contains("\u001b[?2031l", wire.Writes); Assert.DoesNotContain('\u001b', result.Note);
	}
	[Fact]
	public async Task ResizeNeedsInitialAndChangedTypedDimensions() {
		EnvironmentTestContext wire = new(); await using TerminalSession session = await wire.OpenAsync(false);
		PromptClient client = new(Yes(), Resize(80, 24), Resize(80, 24), Resize(100, 30), Yes());
		client.OnPrompt = prompt => { if (prompt.StartsWith("Resize", StringComparison.Ordinal)) { Assert.Equal(2, client.ReadCount); Assert.Contains("\u001b[?2048h", wire.Writes); } };
		CompatibilityScenarioObservation result = await EnvironmentCompatibilityScenarios.RunAsync(session, client, "environment.in-band-resize", CancellationToken.None);
		Assert.Equal(CompatibilityOutcome.Pass, result.Outcome); Assert.Contains("\u001b[?2048l", wire.Writes);
		Assert.DoesNotContain('\u001b', result.Note); Assert.DoesNotContain("hostname", result.Note, StringComparison.OrdinalIgnoreCase);
	}
	[Fact]
	public async Task MissingInitialResizeIsInconclusiveAndCleansUpWithoutResizePrompt() {
		EnvironmentTestContext wire = new(); await using TerminalSession session = await wire.OpenAsync(false);
		PromptClient client = new(Yes(), TerminalEvent.TimedOut());
		CompatibilityScenarioObservation result = await EnvironmentCompatibilityScenarios.RunAsync(session, client, "environment.in-band-resize", CancellationToken.None);
		Assert.Equal(CompatibilityOutcome.Inconclusive, result.Outcome); Assert.Single(client.Prompts); Assert.Contains("\u001b[?2048l", wire.Writes);
	}
	[Fact]
	public async Task AppearanceQueryUsesPublicClientWithoutEnablingReporting() {
		EnvironmentTestContext wire = new(); await using TerminalSession session = await wire.OpenAsync(false);
		CompatibilityScenarioObservation result = await QueryCompatibilityScenarios.RunAsync("query.appearance", new QueryCompatibilityScenarios.TerminalSessionQueryClient(session), CancellationToken.None);
		Assert.Equal(CompatibilityOutcome.Pass, result.Outcome); Assert.Equal(new[] { "\u001b[?996n" }, wire.Writes);
	}
	private static TerminalEvent Yes() => TerminalEvent.FromInput(TerminalInputEvent.FromText(new Rune('y')));
	private static TerminalEvent Resize(int columns, int rows) => TerminalEvent.FromSemantic(TerminalSemanticEvent.FromInBandResize(new(new(columns, rows), null)));
	private sealed class PromptClient(params TerminalEvent[] events) : ICompatibilityLiveClient {
		private readonly Queue<TerminalEvent> events = new(events);
		internal List<string> Prompts { get; } = [];
		internal Action<string>? OnPrompt { get; set; }
		internal int ReadCount { get; private set; }
		public ValueTask WritePromptAsync(string prompt, CancellationToken token) { token.ThrowIfCancellationRequested(); this.Prompts.Add(prompt); this.OnPrompt?.Invoke(prompt); return ValueTask.CompletedTask; }
		public ValueTask<TerminalEvent> ReadEventAsync(TimeSpan timeout, CancellationToken token) { token.ThrowIfCancellationRequested(); ++this.ReadCount; return ValueTask.FromResult(this.events.Count > 0 ? this.events.Dequeue() : TerminalEvent.TimedOut()); }
		public ValueTask<CompatibilityLiveExecution> ExecuteAsync(string id, CancellationToken token) => throw new InvalidOperationException("Environment scenarios must use the public session APIs.");
	}
}
