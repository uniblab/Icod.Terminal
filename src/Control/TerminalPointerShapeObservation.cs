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
/// Represents one explicit OSC 22 pointer-shape observation.
/// </summary>
/// <remarks>
/// <see cref="HasShape"/> is false when the terminal explicitly reports that no
/// application pointer shape is currently set. This is distinct from the semantic
/// CSS <see cref="TerminalPointerShape.Default"/> shape.
/// </remarks>
public sealed class TerminalPointerShapeObservation {
	internal TerminalPointerShapeObservation(
		TerminalPointerShape? shape
	) {
		this.Shape = shape;
	}

	/// <summary>
	/// Gets whether the terminal explicitly reported a semantic pointer shape.
	/// </summary>
	public bool HasShape {
		get {
			return this.Shape.HasValue;
		}
	}

	/// <summary>
	/// Gets the reported semantic pointer shape, or <see langword="null"/> when the
	/// terminal explicitly reports that no application pointer shape is set.
	/// </summary>
	public TerminalPointerShape? Shape {
		get;
	}
}
