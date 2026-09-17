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

using System.Collections.ObjectModel;
using Icod.TermInfo;

/// <summary>Provides an immutable Terminal-owned semantic view of the selected terminal profile.</summary>
public sealed class TerminalProfile {
	private TerminalProfile(
		string name,
		string? description,
		IReadOnlyList<string> aliases,
		TerminalScreenCapabilities screen
	) {
		this.Name = name;
		this.Description = description;
		this.Aliases = aliases;
		this.Screen = screen;
	}

	/// <summary>Gets the selected terminal profile name.</summary>
	public string Name {
		get;
	}

	/// <summary>Gets the optional human-readable profile description.</summary>
	public string? Description {
		get;
	}

	/// <summary>Gets immutable alternative names for the selected profile.</summary>
	public IReadOnlyList<string> Aliases {
		get;
	}

	/// <summary>Gets semantic screen capabilities derived from the selected profile.</summary>
	public TerminalScreenCapabilities Screen {
		get;
	}

	internal static TerminalProfile Create(
		TerminalDescription terminal
	) {
		ArgumentNullException.ThrowIfNull( terminal );
		bool alternateCharacterSet =
			null != terminal.GetString( StringCapability.AlternateCharacterSet )
			&& null != terminal.GetString( StringCapability.EnterAlternateCharacterSetMode )
			&& null != terminal.GetString( StringCapability.ExitAlternateCharacterSetMode );

		return new TerminalProfile(
			terminal.Name,
			terminal.Description,
			new ReadOnlyCollection<string>( terminal.Aliases.ToArray() ),
			new TerminalScreenCapabilities(
				null != terminal.GetString( StringCapability.CursorAddress ),
				null != terminal.GetString( StringCapability.EnterBoldMode ),
				alternateCharacterSet
			)
		);
	}
}
