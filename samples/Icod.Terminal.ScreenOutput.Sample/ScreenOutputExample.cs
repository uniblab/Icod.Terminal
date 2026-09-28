/*
	Icod.Terminal.ScreenOutput.Sample
	Sample application demonstrating Icod.Terminal ScreenOutput features.
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
namespace Icod.Terminal.ScreenOutput.Sample;

/// <summary>Demonstrates one caller-owned frame through Terminal's semantic output boundary.</summary>
public static class ScreenOutputExample {
	/// <summary>Draws a frame, returning false without output if mandatory plans are unavailable.</summary>
	public static async ValueTask<bool> DrawFrameAsync(
		TerminalSession session, string text, CancellationToken cancellationToken = default
	) => await DrawFrameCoreAsync( session, text, false, cancellationToken );

	/// <summary>Draws one frame with a temporary hidden cursor and restores its lease owner.</summary>
	/// <returns>False if required plans or cursor capabilities are unavailable.</returns>
	public static async ValueTask<bool> DrawFrameWithHiddenCursorAsync(
		TerminalSession session, string text, CancellationToken cancellationToken = default
	) => await DrawFrameCoreAsync( session, text, true, cancellationToken );

	private static async ValueTask<bool> DrawFrameCoreAsync(
		TerminalSession session, string text, bool hideCursor,
		CancellationToken cancellationToken
	) {
		ArgumentNullException.ThrowIfNull( session );
		ArgumentNullException.ThrowIfNull( text );
		cancellationToken.ThrowIfCancellationRequested();
		if ( hideCursor && ( !session.Profile.Screen.SupportsCursorHidden
			|| ( !session.Profile.Screen.SupportsCursorNormal
				&& !session.Profile.Screen.SupportsCursorVeryVisible ) ) ) {
			return false;
		}
		// This example leaves wrapping/layout to its caller, including text width.
		TerminalScreenPlanner planner = session.Screen;
		TerminalScreenOperationPlan? baseline = planner.PlanRenditionBaseline();
		TerminalScreenOperationPlan? home = planner.PlanCursorMove( null, new TerminalScreenPosition( 0, 0 ) );
		TerminalScreenRendition rendition = planner.NormalizeRendition( new TerminalScreenRendition(
			TerminalScreenColor.Default, TerminalScreenColor.Default, TerminalTextAttributes.Bold
		) );
		TerminalScreenOperationPlan? enter = planner.PlanRenditionTransition( TerminalScreenRendition.Default, rendition );
		TerminalScreenOperationPlan? leave = planner.PlanRenditionReset( rendition );
		if ( !baseline.HasValue || !home.HasValue || !enter.HasValue || !leave.HasValue ) {
			return false;
		}
		TerminalScreenOutputTransaction frame = session.CreateScreenOutputTransaction();
		if ( hideCursor ) {
			frame.SetCursorVisibilityForCommit( TerminalCursorVisibility.Hidden );
		}
		frame.Add( baseline.Value );
		frame.Add( home.Value );
		TerminalScreenOperationPlan? erase = planner.PlanErase( TerminalScreenEraseKind.ToEndOfLine );
		if ( erase.HasValue ) {
			frame.Add( erase.Value );
		}
		frame.Add( enter.Value );
		frame.WriteText( text );
		frame.Add( leave.Value );
		await frame.CommitAsync( cancellationToken );
		return true;
	}

	/// <summary>Demonstrates rejection of deliberately stale work followed by one fresh frame.</summary>
	/// <remarks>
	/// This is a controlled example, not a general retry policy. Transport failures and
	/// cancellation propagate; only the expected stale commit is handled here.
	/// </remarks>
	public static async ValueTask<bool> DemonstrateStaleRecoveryAsync(
		TerminalSession session, CancellationToken cancellationToken = default
	) {
		ArgumentNullException.ThrowIfNull( session );
		cancellationToken.ThrowIfCancellationRequested();
		TerminalScreenOutputTransaction stale = session.CreateScreenOutputTransaction();
		stale.WriteText( "This stale frame must never appear." );
		// The caller intentionally invalidates its pending frame through coordinated output.
		await session.WriteTextAsync( "Intervening output.\r\n", cancellationToken );
		try {
			await stale.CommitAsync( cancellationToken );
		} catch ( InvalidOperationException ) {
			// No stale bytes were emitted. Discard the consumed builder and replan.
			return await DrawFrameAsync( session, "Fresh frame after stale rejection.", cancellationToken );
		}
		throw new InvalidOperationException( "The demonstration expected stale work to be rejected." );
	}

	/// <summary>Owns an alternate-screen scope; r refreshes the frame, q/Escape/EOF exit.</summary>
	/// <returns>False if presentation or required screen operations are unavailable.</returns>
	public static async ValueTask<bool> RunInteractiveAsync(
		TerminalSession session, bool demonstrateRecovery = false,
		CancellationToken cancellationToken = default
	) {
		ArgumentNullException.ThrowIfNull( session );
		TerminalControlResult<TerminalPresentationLease> result = await session.AcquirePresentationAsync(
			new TerminalPresentationOptions { AlternateScreen = true }, cancellationToken
		);
		if ( !result.IsAvailable ) {
			return false;
		}
		await using TerminalPresentationLease presentation = result.GetRequiredValue();
		bool drawn = demonstrateRecovery
			? await DemonstrateStaleRecoveryAsync( session, cancellationToken )
			: await DrawFrameAsync( session, "Terminal-owned screen output", cancellationToken );
		if ( !drawn ) {
			return false;
		}
		await session.WriteTextAsync( "\r\nPress r to refresh, q or Escape to exit.\r\n", cancellationToken );
		while ( true ) {
			TerminalEvent terminalEvent = await session.ReadEventAsync( cancellationToken );
			// Event waits report cancellation as an event, rather than throwing.
			if ( terminalEvent.Kind == TerminalEventKind.Cancelled ) {
				throw new OperationCanceledException( cancellationToken );
			}
			if ( terminalEvent.Kind != TerminalEventKind.Input || terminalEvent.Input is not { } input ) {
				continue;
			}
			if ( input.Kind == TerminalInputEventKind.EndOfInput
				|| ( input.Kind == TerminalInputEventKind.Key && input.Key == TerminalKey.Escape )
				|| ( ( input.Kind is TerminalInputEventKind.Text or TerminalInputEventKind.Key )
					&& input.Character is { Value: 'q' or 'Q' } ) ) {
				return true;
			}
			if ( ( input.Kind is TerminalInputEventKind.Text or TerminalInputEventKind.Key )
				&& input.Character is { Value: 'r' or 'R' }
				&& input.KeyPhase is not TerminalKeyEventPhase.Release ) {
				// The application owns its frame content and fallback policy. A scoped
				// visibility request leaves any long-lived presentation lease intact.
				if ( !await DrawFrameWithHiddenCursorAsync(
					session, "Input-driven frame", cancellationToken
				) ) {
					await DrawFrameAsync( session, "Input-driven frame", cancellationToken );
				}
			}
		}
	}
}
