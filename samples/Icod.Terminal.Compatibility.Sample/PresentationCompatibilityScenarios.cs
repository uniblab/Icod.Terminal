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

internal static class PresentationCompatibilityScenarios {
	private static readonly TimeSpan QueryTimeout = TimeSpan.FromSeconds( 2 );

	internal static ValueTask<CompatibilityLiveExecution> ExecuteAsync(
		TerminalSession session,
		string scenarioId,
		CancellationToken cancellationToken
	) {
		ArgumentNullException.ThrowIfNull( session );
		return scenarioId switch {
			"presentation.cursor-sync" => RunCursorAndSynchronizedOutputAsync(
				session,
				cancellationToken
			),
			"presentation.pointer-colors" => RunPointerAndColorsAsync(
				session,
				cancellationToken
			),
			_ => throw new ArgumentException( "Unknown presentation scenario.", nameof( scenarioId ) )
		};
	}

	private static async ValueTask<CompatibilityLiveExecution> RunCursorAndSynchronizedOutputAsync(
		TerminalSession session,
		CancellationToken cancellationToken
	) {
		var leases = new List<IAsyncDisposable>();
		try {
			TerminalCursorStyleLease cursor = await session.AcquireCursorStyleAsync(
				TerminalCursorStyle.SteadyBar,
				QueryTimeout,
				cancellationToken
			).ConfigureAwait( false );
			leases.Add( cursor );
			TerminalSynchronizedOutputLease synchronized =
				await session.AcquireSynchronizedOutputAsync(
					cancellationToken
				).ConfigureAwait( false );
			leases.Add( synchronized );
			await session.WriteTextAsync(
				"Scoped steady-bar cursor and synchronized output are active.\r\n",
				cancellationToken
			).ConfigureAwait( false );
			await Task.Delay( 750, cancellationToken ).ConfigureAwait( false );
		} finally {
			await DisposeLeasesAsync( leases, "cursor and synchronized-output" ).ConfigureAwait( false );
		}
		return new CompatibilityLiveExecution(
			true,
			true,
			"Cursor-style and synchronized-output leases completed and were released."
		);
	}

	private static async ValueTask<CompatibilityLiveExecution> RunPointerAndColorsAsync(
		TerminalSession session,
		CancellationToken cancellationToken
	) {
		var leases = new List<IAsyncDisposable>();
		try {
			TerminalPointerShapeLease pointer = await session.AcquirePointerShapeAsync(
				TerminalPointerShape.Crosshair,
				cancellationToken
			).ConfigureAwait( false );
			leases.Add( pointer );
			TerminalPaletteColorLease palette = await session.AcquirePaletteColorAsync(
				1,
				TerminalColor.FromRgb8( 255, 96, 96 ),
				QueryTimeout,
				cancellationToken
			).ConfigureAwait( false );
			leases.Add( palette );
			TerminalDynamicColorLease foreground = await session.AcquireDynamicColorAsync(
				TerminalDynamicColor.DefaultForeground,
				TerminalColor.FromRgb8( 96, 255, 96 ),
				QueryTimeout,
				cancellationToken
			).ConfigureAwait( false );
			leases.Add( foreground );
			await session.WriteTextAsync(
				"Scoped crosshair pointer, palette color, and foreground color are active.\r\n",
				cancellationToken
			).ConfigureAwait( false );
			await Task.Delay( 750, cancellationToken ).ConfigureAwait( false );
		} finally {
			await DisposeLeasesAsync( leases, "pointer and color" ).ConfigureAwait( false );
		}
		return new CompatibilityLiveExecution(
			true,
			true,
			"Pointer and color leases completed and were released."
		);
	}

	private static async ValueTask DisposeLeasesAsync(
		IReadOnlyList<IAsyncDisposable> leases,
		string name
	) {
		List<Exception>? failures = null;
		for ( int index = leases.Count - 1; 0 <= index; --index ) {
			try {
				await leases[ index ].DisposeAsync().ConfigureAwait( false );
			} catch ( Exception exception ) {
				( failures ??= [] ).Add( exception );
			}
		}
		if ( failures is not null ) {
			Exception inner = 1 == failures.Count
				? failures[ 0 ]
				: new AggregateException( failures );
			throw new CompatibilityCleanupException(
				$"The {name} leases could not restore terminal state.",
				inner
			);
		}
	}
}
