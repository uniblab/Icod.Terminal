/*
	Icod.Terminal.DCursesScreenContractsAcceptance
	Downstream Icod.DCurses acceptance utility for Icod.Terminal integration contracts.
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

TerminalDimensions dimensions = new( 120, 40 );
TerminalScreenPosition position = new( 12, 34 );
TerminalScreenRendition rendition = new(
	TerminalScreenColor.Indexed( 2 ),
	TerminalScreenColor.Rgb( 0x10, 0x20, 0x30 ),
	TerminalTextAttributes.Bold | TerminalTextAttributes.Underline
);

Require(
	120 == dimensions.Columns
		&& 40 == dimensions.Rows
		&& 12 == position.Row
		&& 34 == position.Column,
	"The Terminal-owned screen geometry contract produced unexpected values."
);
Require(
	TerminalScreenColorKind.Indexed == rendition.Foreground.Kind
		&& 2 == rendition.Foreground.Index
		&& TerminalScreenColorKind.Rgb == rendition.Background.Kind
		&& 0x10 == rendition.Background.Red
		&& 0x20 == rendition.Background.Green
		&& 0x30 == rendition.Background.Blue,
	"The Terminal-owned rendition contract produced unexpected values."
);

Console.WriteLine(
	"Future DCurses Terminal-owned screen-contract acceptance passed."
);

static void Require(
	bool condition,
	string message
) {
	ArgumentException.ThrowIfNullOrWhiteSpace( message );
	if ( !condition ) {
		throw new InvalidOperationException( message );
	}
}

internal static class FutureDcursesRenderer {
	internal static async ValueTask RefreshAsync(
		TerminalSession session,
		CancellationToken cancellationToken = default
	) {
		ArgumentNullException.ThrowIfNull( session );

		TerminalDimensions dimensions = session
			.GetDimensions()
			.GetRequiredValue();
		TerminalProfile profile = session.Profile;
		TerminalScreenPlanner planner = session.Screen;
		if ( !profile.Screen.SupportsAbsoluteCursorAddressing ) {
			throw new InvalidOperationException(
				"The selected terminal cannot address the retained screen."
			);
		}

		TerminalScreenPosition home = new( 0, 0 );
		TerminalScreenOperationPlan cursor = planner.PlanCursorMove(
			null,
			home
		) ?? throw new InvalidOperationException(
			"The selected terminal cannot position the retained-screen cursor."
		);
		TerminalScreenOperationPlan? renditionBaseline = planner.PlanRenditionBaseline();

		TerminalScreenRendition requested = new(
			TerminalScreenColor.Indexed( 2 ),
			TerminalScreenColor.Default,
			TerminalTextAttributes.Bold | TerminalTextAttributes.Underline
		);
		TerminalScreenRendition normalized = planner.NormalizeRendition(
			requested
		);
		TerminalScreenOperationPlan? rendition = planner.PlanRenditionTransition(
			TerminalScreenRendition.Default,
			normalized
		);
		TerminalScreenOperationPlan? erase = planner.PlanErase(
			TerminalScreenEraseKind.ToEndOfLine
		);
		TerminalScreenOperationPlan? insertCharacters = planner.PlanCharacterShift(
			TerminalScreenCharacterShiftKind.Insert,
			2
		);
		TerminalScreenOperationPlan? deleteLines = planner.PlanLineShift(
			TerminalScreenLineShiftKind.Delete,
			1,
			dimensions.Rows
		);
		TerminalScreenOperationPlan? scrollRegion = planner.PlanScrollRegion(
			0,
			dimensions.Rows - 1,
			dimensions.Rows
		);
		TerminalScreenOperationPlan? alert = planner.PlanAlert(
			TerminalAlertKind.Visual
		);
		var lineGlyph = planner.ResolveLineGlyph(
			TerminalLineGlyph.Horizontal
		);
		TerminalScreenOperationPlan? enterAlternateCharacterSet = lineGlyph.HasValue
			&& lineGlyph.Value.UsesAlternateCharacterSet
			? planner.PlanAlternateCharacterSet( enabled: true )
			: null;
		TerminalScreenOperationPlan? leaveAlternateCharacterSet = lineGlyph.HasValue
			&& lineGlyph.Value.UsesAlternateCharacterSet
			? planner.PlanAlternateCharacterSet( enabled: false )
			: null;

		TerminalScreenOutputTransaction output =
			session.CreateScreenOutputTransaction(
				new TerminalScreenOutputTransactionOptions {
					UseSynchronizedOutput = true
				}
			);
		output.Add( cursor );
		AddIfSupported( output, renditionBaseline );
		AddIfSupported( output, scrollRegion );
		AddIfSupported( output, rendition );
		AddIfSupported( output, erase );
		AddIfSupported( output, insertCharacters );
		AddIfSupported( output, deleteLines );
		AddIfSupported( output, enterAlternateCharacterSet );
		output.WriteText(
			lineGlyph?.Content
				?? "─"
		);
		AddIfSupported( output, leaveAlternateCharacterSet );
		output.WriteHyperlink(
			"screen contracts",
			"https://github.com/uniblab/Icod.Terminal"
		);
		AddIfSupported( output, alert );
		await output.CommitAsync( cancellationToken );
	}

	private static void AddIfSupported(
		TerminalScreenOutputTransaction output,
		TerminalScreenOperationPlan? plan
	) {
		ArgumentNullException.ThrowIfNull( output );
		if ( plan.HasValue ) {
			output.Add( plan.Value );
		}
	}
}
