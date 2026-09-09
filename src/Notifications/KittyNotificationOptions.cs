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
/// Configures one typed Kitty OSC 99 desktop-notification request.
/// </summary>
public sealed class KittyNotificationOptions {
	/// <summary>
	/// Gets or initializes the optional stable notification identifier used for update/close semantics.
	/// </summary>
	/// <remarks>
	/// Identifiers are limited to ASCII letters, digits, underscore, hyphen, plus sign, and period.
	/// The legacy special identifier <c>0</c> is rejected. Reusing an identifier requests replacement/update
	/// of the previously displayed notification with that identifier when the terminal supports replacement.
	/// </remarks>
	public string? Identifier {
		get;
		init;
	}

	/// <summary>
	/// Gets or initializes the optional application name used by terminal notification filtering/icon lookup.
	/// </summary>
	public string? ApplicationName {
		get;
		init;
	}

	/// <summary>
	/// Gets or initializes optional notification-type strings used by terminal filtering policy.
	/// </summary>
	public IReadOnlyList<string> NotificationTypes {
		get;
		init;
	} = Array.Empty<string>();

	/// <summary>
	/// Gets or initializes whether activation should retain Kitty's default focus-the-originating-window action.
	/// </summary>
	/// <remarks>
	/// The default is <see langword="true"/>. Setting this property to <see langword="false"/> emits
	/// <c>a=-focus</c>. Activation reporting is deliberately not exposed by this release because unsolicited
	/// OSC 99 reports require a separate session-event routing contract.
	/// </remarks>
	public bool FocusOnActivation {
		get;
		init;
	} = true;

	/// <summary>
	/// Gets or initializes when the terminal should honor the notification request.
	/// </summary>
	public KittyNotificationOccasion Occasion {
		get;
		init;
	} = KittyNotificationOccasion.Always;

	/// <summary>
	/// Gets or initializes the optional notification urgency.
	/// </summary>
	public KittyNotificationUrgency? Urgency {
		get;
		init;
	}

	/// <summary>
	/// Gets or initializes notification expiration policy.
	/// </summary>
	/// <remarks>
	/// <see langword="null"/> leaves expiration to platform policy. <see cref="TimeSpan.Zero"/> requests
	/// a notification that does not expire automatically. A positive value requests automatic closure after
	/// that interval, subject to terminal and operating-system behavior.
	/// </remarks>
	public TimeSpan? Expiration {
		get;
		init;
	}

	/// <summary>
	/// Gets or initializes the optional sound name. Kitty-defined names include <c>system</c> and <c>silent</c>.
	/// </summary>
	public string? SoundName {
		get;
		init;
	}

	/// <summary>
	/// Gets or initializes ordered icon names. The terminal uses the first locally available icon.
	/// </summary>
	public IReadOnlyList<string> IconNames {
		get;
		init;
	} = Array.Empty<string>();

	/// <summary>
	/// Gets or initializes optional PNG, JPEG, or GIF icon bytes for transmission to the terminal.
	/// </summary>
	/// <remarks>
	/// Supplying icon data also requires <see cref="IconDataIdentifier"/> so the terminal can associate/cache
	/// the transmitted image. The bytes are copied and validated before output commitment.
	/// </remarks>
	public byte[]? IconData {
		get;
		init;
	}

	/// <summary>
	/// Gets or initializes the optional Kitty icon-data cache identifier.
	/// </summary>
	/// <remarks>
	/// This identifier uses the same restricted grammar as <see cref="Identifier"/>. It may be supplied
	/// without <see cref="IconData"/> to reference icon data previously cached by the terminal.
	/// </remarks>
	public string? IconDataIdentifier {
		get;
		init;
	}
}
