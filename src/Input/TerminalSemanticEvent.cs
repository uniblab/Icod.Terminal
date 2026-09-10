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
/// Identifies one protocol-neutral semantic event family reported by a terminal.
/// </summary>
public enum TerminalSemanticEventKind {
	/// <summary>A desktop-notification interaction or state report is available.</summary>
	Notification
}

/// <summary>
/// Represents one reviewed semantic terminal event without exposing raw protocol framing.
/// </summary>
public sealed class TerminalSemanticEvent {
	private TerminalSemanticEvent(
		TerminalSemanticEventKind kind,
		TerminalNotificationEvent? notification
	) {
		this.Kind = kind;
		this.Notification = notification;
	}

	/// <summary>Gets the semantic event family.</summary>
	public TerminalSemanticEventKind Kind {
		get;
	}

	/// <summary>
	/// Gets the notification event when <see cref="Kind"/> is
	/// <see cref="TerminalSemanticEventKind.Notification"/>.
	/// </summary>
	public TerminalNotificationEvent? Notification {
		get;
	}

	internal static TerminalSemanticEvent FromNotification(
		TerminalNotificationEvent notification
	) {
		ArgumentNullException.ThrowIfNull( notification );

		return new TerminalSemanticEvent(
			TerminalSemanticEventKind.Notification,
			notification
		);
	}
}
