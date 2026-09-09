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
/// Represents one correlated Kitty OSC 99 notification-capability observation.
/// </summary>
public sealed class KittyNotificationSupport {
	private readonly KittyNotificationOccasion[] occasions;
	private readonly KittyNotificationUrgency[] urgencies;
	private readonly string[] sounds;

	internal KittyNotificationSupport(
		bool supportsFocusAction,
		bool supportsActivationReports,
		bool supportsCloseEvents,
		bool supportsTitle,
		bool supportsBody,
		bool supportsClose,
		bool supportsIconData,
		bool supportsAliveQuery,
		bool supportsButtons,
		bool supportsAutoExpiration,
		IEnumerable<KittyNotificationOccasion> occasions,
		IEnumerable<KittyNotificationUrgency> urgencies,
		IEnumerable<string> sounds
	) {
		ArgumentNullException.ThrowIfNull( occasions );
		ArgumentNullException.ThrowIfNull( urgencies );
		ArgumentNullException.ThrowIfNull( sounds );
		this.SupportsFocusAction = supportsFocusAction;
		this.SupportsActivationReports = supportsActivationReports;
		this.SupportsCloseEvents = supportsCloseEvents;
		this.SupportsTitle = supportsTitle;
		this.SupportsBody = supportsBody;
		this.SupportsClose = supportsClose;
		this.SupportsIconData = supportsIconData;
		this.SupportsAliveQuery = supportsAliveQuery;
		this.SupportsButtons = supportsButtons;
		this.SupportsAutoExpiration = supportsAutoExpiration;
		this.occasions = occasions.Distinct().ToArray();
		this.urgencies = urgencies.Distinct().ToArray();
		this.sounds = sounds.Distinct( StringComparer.Ordinal ).ToArray();
	}

	/// <summary>
	/// Gets whether the terminal reports support for the <c>focus</c> activation action.
	/// </summary>
	public bool SupportsFocusAction {
		get;
	}

	/// <summary>
	/// Gets whether the terminal reports support for activation reports.
	/// </summary>
	public bool SupportsActivationReports {
		get;
	}

	/// <summary>
	/// Gets whether the terminal reports support for close-event reports.
	/// </summary>
	public bool SupportsCloseEvents {
		get;
	}

	/// <summary>
	/// Gets whether the terminal reports support for title payloads.
	/// </summary>
	public bool SupportsTitle {
		get;
	}

	/// <summary>
	/// Gets whether the terminal reports support for body payloads.
	/// </summary>
	public bool SupportsBody {
		get;
	}

	/// <summary>
	/// Gets whether the terminal reports support for explicit notification close requests.
	/// </summary>
	public bool SupportsClose {
		get;
	}

	/// <summary>
	/// Gets whether the terminal reports support for transmitted icon payloads.
	/// </summary>
	public bool SupportsIconData {
		get;
	}

	/// <summary>
	/// Gets whether the terminal reports support for alive-notification queries.
	/// </summary>
	public bool SupportsAliveQuery {
		get;
	}

	/// <summary>
	/// Gets whether the terminal reports support for button payloads.
	/// </summary>
	public bool SupportsButtons {
		get;
	}

	/// <summary>
	/// Gets whether the terminal reports support for automatic notification expiration.
	/// </summary>
	public bool SupportsAutoExpiration {
		get;
	}

	/// <summary>
	/// Gets the notification occasions reported by the terminal.
	/// </summary>
	public IReadOnlyList<KittyNotificationOccasion> Occasions {
		get {
			return this.occasions;
		}
	}

	/// <summary>
	/// Gets the urgency values reported by the terminal.
	/// </summary>
	public IReadOnlyList<KittyNotificationUrgency> Urgencies {
		get {
			return this.urgencies;
		}
	}

	/// <summary>
	/// Gets the sound names reported by the terminal.
	/// </summary>
	public IReadOnlyList<string> Sounds {
		get {
			return this.sounds;
		}
	}
}
