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

/// <summary>Represents one opaque, side-effect-free terminal screen-operation plan.</summary>
public readonly record struct TerminalScreenOperationPlan {
	internal TerminalScreenOperationPlan(
		TerminalScreenPlanner owner,
		TerminalScreenOperationKind kind,
		IReadOnlyList<TerminalScreenOutputSegment> segments,
		int byteCount,
		int affectedLines
	) {
		ArgumentNullException.ThrowIfNull( owner );
		ArgumentNullException.ThrowIfNull( segments );
		if ( 0 > byteCount ) {
			throw new ArgumentOutOfRangeException( nameof( byteCount ) );
		}
		if ( 0 >= affectedLines ) {
			throw new ArgumentOutOfRangeException( nameof( affectedLines ) );
		}

		this.Owner = owner;
		this.Kind = kind;
		this.Segments = segments;
		this.ByteCount = byteCount;
		this.AffectedLines = affectedLines;
	}

	/// <summary>Gets the semantic purpose of this plan.</summary>
	public TerminalScreenOperationKind Kind {
		get;
	}

	/// <summary>Gets the resolved terminal-byte cost after capability expansion.</summary>
	public int ByteCount {
		get;
	}

	/// <summary>Gets the number of terminal lines affected by padding-sensitive emission.</summary>
	public int AffectedLines {
		get;
	}

	internal TerminalScreenPlanner? Owner {
		get;
	}

	internal IReadOnlyList<TerminalScreenOutputSegment>? Segments {
		get;
	}

	internal bool IsValid => this.Owner is not null && this.Segments is not null;
}

internal readonly record struct TerminalScreenOutputSegment(
	string Value,
	int AffectedLines
);
