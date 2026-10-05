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
using Icod.Terminal.Compatibility.Sample;
using Xunit;

public sealed class TerminalCompatibilityLiveScenarioTests {
	[Fact]
	public async Task DeclinedConsentDoesNotExecuteSideEffect() {
		var client = new RecordingLiveClient(
			TerminalEvent.FromInput( TerminalInputEvent.FromText( new Rune( 'n' ) ) )
		);

		CompatibilityScenarioObservation result = await LiveCompatibilityScenarios.RunAsync(
			"notifications",
			client,
			CancellationToken.None
		);

		Assert.Equal( CompatibilityOutcome.NotRun, result.Outcome );
		Assert.Empty( client.ExecutedScenarioIds );
	}

	[Fact]
	public async Task VisibleOutputNeedsOperatorObservationToPass() {
		var client = new RecordingLiveClient(
			TerminalEvent.FromInput( TerminalInputEvent.FromText( new Rune( 'y' ) ) ),
			TerminalEvent.TimedOut()
		) {
			Execution = new CompatibilityLiveExecution(
				true,
				true,
				"Typed notification requests completed."
			)
		};

		CompatibilityScenarioObservation result = await LiveCompatibilityScenarios.RunAsync(
			"notifications",
			client,
			CancellationToken.None
		);

		Assert.Equal( CompatibilityOutcome.Inconclusive, result.Outcome );
		Assert.True( result.AutomatedObservation );
		Assert.Null( result.OperatorObservation );
	}

	[Fact]
	public async Task OperatorCanConfirmVisibleOutput() {
		var client = new RecordingLiveClient(
			TerminalEvent.FromInput( TerminalInputEvent.FromText( new Rune( 'y' ) ) ),
			TerminalEvent.FromInput( TerminalInputEvent.FromText( new Rune( 'y' ) ) )
		) {
			Execution = new CompatibilityLiveExecution( true, true, "Visible operation completed." )
		};

		CompatibilityScenarioObservation result = await LiveCompatibilityScenarios.RunAsync(
			"presentation.cursor-sync",
			client,
			CancellationToken.None
		);

		Assert.Equal( CompatibilityOutcome.Pass, result.Outcome );
		Assert.True( result.AutomatedObservation );
		Assert.True( result.OperatorObservation );
	}

	[Theory]
	[InlineData( true )]
	[InlineData( false )]
	public async Task PermissionDenialAndTimeoutAreInconclusive(
		bool permissionDenied
	) {
		var client = new RecordingLiveClient(
			TerminalEvent.FromInput( TerminalInputEvent.FromText( new Rune( 'y' ) ) )
		) {
			ExecutionException = permissionDenied
				? new UnauthorizedAccessException( "denied" )
				: new TimeoutException( "timeout" )
		};

		CompatibilityScenarioObservation result = await LiveCompatibilityScenarios.RunAsync(
			"clipboard.osc52",
			client,
			CancellationToken.None
		);

		Assert.Equal( CompatibilityOutcome.Inconclusive, result.Outcome );
	}

	[Fact]
	public async Task CleanupFailureCannotBecomePass() {
		var client = new RecordingLiveClient(
			TerminalEvent.FromInput( TerminalInputEvent.FromText( new Rune( 'y' ) ) )
		) {
			ExecutionException = new CompatibilityCleanupException(
				"The scoped terminal state could not be restored.",
				new IOException( "restore failed" )
			)
		};

		CompatibilityScenarioObservation result = await LiveCompatibilityScenarios.RunAsync(
			"presentation.pointer-colors",
			client,
			CancellationToken.None
		);

		Assert.Equal( CompatibilityOutcome.Fail, result.Outcome );
		Assert.False( result.AutomatedObservation );
	}

	[Fact]
	public async Task ConfirmationIgnoresUnrelatedEventKindsAndUsesSessionInput() {
		var client = new RecordingLiveClient(
			TerminalEvent.FromLifecycle(
				new TerminalLifecycleEvent( TerminalLifecycleEventKind.Resize )
			),
			TerminalEvent.FromInput( TerminalInputEvent.FromText( new Rune( 'y' ) ) )
		);

		CompatibilityConfirmation result = await InteractiveConfirmation.RequestAsync(
			client,
			"Proceed?",
			CancellationToken.None
		);

		Assert.Equal( CompatibilityConfirmation.Yes, result );
		Assert.Equal( 2, client.ReadCount );
		Assert.Contains( "Proceed?", Assert.Single( client.Prompts ) );
	}

	[Fact]
	public async Task EndOfInputEndsConfirmationCleanly() {
		var client = new RecordingLiveClient(
			TerminalEvent.FromInput( TerminalInputEvent.EndOfInput() )
		);

		CompatibilityConfirmation result = await InteractiveConfirmation.RequestAsync(
			client,
			"Proceed?",
			CancellationToken.None
		);

		Assert.Equal( CompatibilityConfirmation.Unavailable, result );
	}

	[Fact]
	public async Task ClipboardEvidenceDoesNotContainPayload() {
		var client = new RecordingLiveClient(
			TerminalEvent.FromInput( TerminalInputEvent.FromText( new Rune( 'y' ) ) )
		) {
			Execution = new CompatibilityLiveExecution(
				true,
				false,
				"The fixed clipboard value completed a byte-for-byte round trip."
			)
		};

		CompatibilityScenarioObservation result = await LiveCompatibilityScenarios.RunAsync(
			"clipboard.osc52",
			client,
			CancellationToken.None
		);

		Assert.Equal( CompatibilityOutcome.Pass, result.Outcome );
		Assert.DoesNotContain(
			ClipboardCompatibilityScenarios.FixedTestValue,
			result.Note,
			StringComparison.Ordinal
		);
	}

	[Fact]
	public async Task ExplicitClipboardMismatchFails() {
		var client = new RecordingLiveClient(
			TerminalEvent.FromInput( TerminalInputEvent.FromText( new Rune( 'y' ) ) )
		) {
			Execution = new CompatibilityLiveExecution(
				false,
				false,
				"The clipboard reply did not match the fixed test value.",
				ExplicitFailure: true
			)
		};

		CompatibilityScenarioObservation result = await LiveCompatibilityScenarios.RunAsync(
			"clipboard.osc52",
			client,
			CancellationToken.None
		);

		Assert.Equal( CompatibilityOutcome.Fail, result.Outcome );
	}

	[Fact]
	public void InputObservationsRemainDistinct() {
		var observations = new InputCompatibilityScenarios.ObservationSet();

		observations.Observe( TerminalInputEvent.FromPaste( new TerminalPasteEvent( TerminalPastePhase.Begin ) ) );
		observations.Observe( TerminalInputEvent.FromPaste( new TerminalPasteEvent( TerminalPastePhase.Data, "x" ) ) );
		observations.Observe( TerminalInputEvent.FromPaste( new TerminalPasteEvent( TerminalPastePhase.End ) ) );
		observations.Observe( TerminalInputEvent.FromFocus( new TerminalFocusEvent( TerminalFocusState.Focused ) ) );
		observations.Observe( TerminalInputEvent.FromMouse( new TerminalMouseEvent(
			TerminalMouseAction.Press,
			TerminalMouseButton.Primary,
			0,
			0
		) ) );
		observations.Observe( TerminalInputEvent.FromKey(
			TerminalKey.Left,
			keyPhase: TerminalKeyEventPhase.Release
		) );

		Assert.True( observations.PasteBegin );
		Assert.True( observations.PasteData );
		Assert.True( observations.PasteEnd );
		Assert.True( observations.Focus );
		Assert.True( observations.Mouse );
		Assert.Contains( TerminalKeyEventPhase.Release, observations.KeyPhases );
	}

	private sealed class RecordingLiveClient : ICompatibilityLiveClient {
		private readonly Queue<TerminalEvent> events;

		internal RecordingLiveClient(
			params TerminalEvent[] events
		) {
			this.events = new Queue<TerminalEvent>( events );
		}

		internal List<string> Prompts { get; } = [];

		internal List<string> ExecutedScenarioIds { get; } = [];

		internal int ReadCount { get; private set; }

		internal CompatibilityLiveExecution Execution { get; set; } =
			new( true, false, "Automated observation completed." );

		internal Exception? ExecutionException { get; set; }

		public ValueTask WritePromptAsync(
			string prompt,
			CancellationToken cancellationToken
		) {
			cancellationToken.ThrowIfCancellationRequested();
			this.Prompts.Add( prompt );
			return ValueTask.CompletedTask;
		}

		public ValueTask<TerminalEvent> ReadEventAsync(
			TimeSpan timeout,
			CancellationToken cancellationToken
		) {
			cancellationToken.ThrowIfCancellationRequested();
			++this.ReadCount;
			return ValueTask.FromResult(
				0 < this.events.Count ? this.events.Dequeue() : TerminalEvent.TimedOut()
			);
		}

		public ValueTask<CompatibilityLiveExecution> ExecuteAsync(
			string scenarioId,
			CancellationToken cancellationToken
		) {
			cancellationToken.ThrowIfCancellationRequested();
			this.ExecutedScenarioIds.Add( scenarioId );
			return this.ExecutionException is null
				? ValueTask.FromResult( this.Execution )
				: ValueTask.FromException<CompatibilityLiveExecution>( this.ExecutionException );
		}
	}
}
