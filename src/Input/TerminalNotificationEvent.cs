/*
	Icod.Terminal
	Managed, cross-platform live-terminal session and terminal-control library for .NET.
	Copyright (C) 2026  Timothy J. Bruce <uniblab@hotmail.com>
*/

/*
	This program is free software: you can redistribute it and/or modify
	it under the terms of the GNU Lesser General Public License as published by
	the Free Software Foundation, either version 3 of the License, or
	(at your option) any later version.

	This program is distributed in the hope that it will be useful,
	but WITHOUT ANY WARRANTY; without even the implied warranty of
	MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
	GNU Lesser General Public License for more details.

	You should have received a copy of the GNU Lesser General Public License
	along with this program.  If not, see <https://www.gnu.org/licenses/>.
*/
namespace Icod.Terminal;

/// <summary>
/// Identifies one semantic desktop-notification report from the terminal.
/// </summary>
public enum TerminalNotificationEventKind {
	/// <summary>The notification was activated without a specific button.</summary>
	Activated,

	/// <summary>One notification button was activated.</summary>
	ButtonActivated,

	/// <summary>The terminal reports that the notification was closed.</summary>
	Closed,

	/// <summary>The terminal reports that reliable close tracking is unavailable.</summary>
	CloseTrackingUnavailable
}

/// <summary>
/// Represents one typed desktop-notification interaction or state report.
/// </summary>
/// <remarks>
/// Notification reports are terminal-controlled input. The identifier is correlation data,
/// not authentication that the event originated from a notification sent by this process.
/// </remarks>
public sealed class TerminalNotificationEvent {
	private TerminalNotificationEvent(
		TerminalNotificationEventKind kind,
		string identifier,
		int? buttonNumber
	) {
		ArgumentNullException.ThrowIfNull( identifier );
		if ( 0 == identifier.Length ) {
			throw new ArgumentException(
				"A terminal notification event identifier cannot be empty.",
				nameof( identifier )
			);
		}
		if ( TerminalNotificationEventKind.ButtonActivated == kind ) {
			if ( !buttonNumber.HasValue || 1 > buttonNumber.Value ) {
				throw new ArgumentOutOfRangeException(
					nameof( buttonNumber ),
					buttonNumber,
					"A notification button number must be one-based and positive."
				);
			}
		} else if ( buttonNumber.HasValue ) {
			throw new ArgumentException(
				"Only a button-activation event may carry a button number.",
				nameof( buttonNumber )
			);
		}

		this.Kind = kind;
		this.Identifier = identifier;
		this.ButtonNumber = buttonNumber;
	}

	/// <summary>Gets the notification event kind.</summary>
	public TerminalNotificationEventKind Kind {
		get;
	}

	/// <summary>Gets the terminal-reported notification identifier.</summary>
	public string Identifier {
		get;
	}

	/// <summary>
	/// Gets the one-based activated button number when <see cref="Kind"/> is
	/// <see cref="TerminalNotificationEventKind.ButtonActivated"/>.
	/// </summary>
	public int? ButtonNumber {
		get;
	}

	internal static TerminalNotificationEvent Activated(
		string identifier
	) {
		return new TerminalNotificationEvent(
			TerminalNotificationEventKind.Activated,
			identifier,
			buttonNumber: null
		);
	}

	internal static TerminalNotificationEvent ButtonActivated(
		string identifier,
		int buttonNumber
	) {
		return new TerminalNotificationEvent(
			TerminalNotificationEventKind.ButtonActivated,
			identifier,
			buttonNumber
		);
	}

	internal static TerminalNotificationEvent Closed(
		string identifier
	) {
		return new TerminalNotificationEvent(
			TerminalNotificationEventKind.Closed,
			identifier,
			buttonNumber: null
		);
	}

	internal static TerminalNotificationEvent CloseTrackingUnavailable(
		string identifier
	) {
		return new TerminalNotificationEvent(
			TerminalNotificationEventKind.CloseTrackingUnavailable,
			identifier,
			buttonNumber: null
		);
	}
}
