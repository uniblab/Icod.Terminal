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
	/// An explicit identifier is required when <see cref="ReportActivation"/> or <see cref="ReportClose"/>
	/// is enabled because received reports use this value only as correlation data.
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
	/// the Kitty <c>-focus</c> action. This option composes independently with
	/// <see cref="ReportActivation"/>.
	/// </remarks>
	public bool FocusOnActivation {
		get;
		init;
	} = true;

	/// <summary>
	/// Gets or initializes whether activation and button interaction reports are requested.
	/// </summary>
	/// <remarks>
	/// The default is <see langword="false"/>. Enabling reporting requires an explicit
	/// <see cref="Identifier"/>. A successful send only proves request emission; received reports remain
	/// untrusted terminal-controlled input.
	/// </remarks>
	public bool ReportActivation {
		get;
		init;
	}

	/// <summary>
	/// Gets or initializes whether notification-close reports are requested.
	/// </summary>
	/// <remarks>
	/// The default is <see langword="false"/>. Enabling close reporting requires an explicit
	/// <see cref="Identifier"/>.
	/// </remarks>
	public bool ReportClose {
		get;
		init;
	}

	/// <summary>
	/// Gets or initializes the ordered button labels shown by a supporting terminal.
	/// </summary>
	/// <remarks>
	/// Button labels are encoded as one bounded UTF-8 Kitty <c>p=buttons</c> payload. Applications that
	/// need button-activation events should also enable <see cref="ReportActivation"/> and provide an
	/// explicit <see cref="Identifier"/>.
	/// </remarks>
	public IReadOnlyList<string> Buttons {
		get;
		init;
	} = Array.Empty<string>();

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
	/// Icon data can be transmitted without a cache identifier. Supplying <see cref="IconDataIdentifier"/>
	/// additionally requests Kitty's <c>g</c>-key cache semantics so the same image may be referenced by a
	/// later notification. The bytes are copied and validated before output commitment.
	/// </remarks>
	public byte[]? IconData {
		get;
		init;
	}

	/// <summary>
	/// Gets or initializes the optional Kitty icon-data cache identifier.
	/// </summary>
	/// <remarks>
	/// This identifier uses the same restricted grammar as <see cref="Identifier"/>. When supplied with
	/// <see cref="IconData"/>, it requests that the terminal cache the transmitted icon under this identity.
	/// It may also be supplied without <see cref="IconData"/> to reference icon data previously cached by
	/// the terminal. Applications are responsible for choosing a suitably unique identifier.
	/// </remarks>
	public string? IconDataIdentifier {
		get;
		init;
	}
}
