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
	) {
		ArgumentNullException.ThrowIfNull( session );
		ArgumentNullException.ThrowIfNull( text );
		cancellationToken.ThrowIfCancellationRequested();
		TerminalDimensions dimensions = session.GetDimensions().GetRequiredValue();
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
}
