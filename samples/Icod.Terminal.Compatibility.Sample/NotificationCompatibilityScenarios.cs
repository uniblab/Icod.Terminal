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

internal static class NotificationCompatibilityScenarios {
	internal static ValueTask<CompatibilityLiveExecution> ExecuteAsync(
		TerminalSession session,
		string scenarioId,
		CancellationToken cancellationToken
	) {
		ArgumentNullException.ThrowIfNull( session );
		return scenarioId switch {
			"notifications" => RunNotificationsAsync( session, cancellationToken ),
			"progress" => RunProgressAsync( session, cancellationToken ),
			_ => throw new ArgumentException( "Unknown notification scenario.", nameof( scenarioId ) )
		};
	}

	private static async ValueTask<CompatibilityLiveExecution> RunNotificationsAsync(
		TerminalSession session,
		CancellationToken cancellationToken
	) {
		const string title = "Icod.Terminal compatibility";
		const string message = "Fixed compatibility notification";
		await session.SendNotificationAsync( message, cancellationToken ).ConfigureAwait( false );
		await session.SendTitledNotificationAsync( title, message, cancellationToken ).ConfigureAwait( false );
		await session.SendKittyNotificationAsync(
			title,
			message,
			new KittyNotificationOptions {
				Identifier = "icod-terminal-compatibility",
				ApplicationName = "Icod.Terminal.Compatibility.Sample",
				NotificationTypes = [ "compatibility" ]
			},
			cancellationToken
		).ConfigureAwait( false );
		await session.WriteTextAsync(
			"Requested fixed OSC 9, OSC 777, and OSC 99 notifications.\r\n",
			cancellationToken
		).ConfigureAwait( false );
		return new CompatibilityLiveExecution(
			true,
			true,
			"Typed OSC 9, OSC 777, and OSC 99 notification requests completed."
		);
	}

	private static async ValueTask<CompatibilityLiveExecution> RunProgressAsync(
		TerminalSession session,
		CancellationToken cancellationToken
	) {
		TerminalProgressLease progress = await session.AcquireProgressAsync(
			cancellationToken
		).ConfigureAwait( false );
		try {
			await progress.ReportAsync( 1, 3, cancellationToken ).ConfigureAwait( false );
			await Task.Delay( 200, cancellationToken ).ConfigureAwait( false );
			await progress.ReportAsync( 2, 3, cancellationToken ).ConfigureAwait( false );
			await Task.Delay( 200, cancellationToken ).ConfigureAwait( false );
			await progress.ReportAsync(
				TerminalProgressState.Attention,
				3,
				3,
				cancellationToken
			).ConfigureAwait( false );
			await Task.Delay( 400, cancellationToken ).ConfigureAwait( false );
		} finally {
			try {
				await progress.DisposeAsync().ConfigureAwait( false );
			} catch ( Exception exception ) {
				throw new CompatibilityCleanupException(
					"The progress lease could not restore terminal progress state.",
					exception
				);
			}
		}
		return new CompatibilityLiveExecution(
			true,
			true,
			"The typed progress lifecycle completed and its lease was released."
		);
	}
}
