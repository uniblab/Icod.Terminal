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
/// Provides one immutable snapshot of Icod.Terminal's local ownership certainty for a persistent raster handle.
/// </summary>
/// <remarks>
/// This snapshot reports local library knowledge. A <see cref="TerminalRasterOwnershipStatus.Current"/>
/// result is not authentication and is not authoritative proof that a terminal-side object still exists.
/// </remarks>
/// <param name="Status">The current local ownership status.</param>
/// <param name="LossReason">The semantic reason associated with a non-current status, or <see cref="TerminalRasterOwnershipLossReason.None"/>.</param>
public readonly record struct TerminalRasterOwnershipState(
	TerminalRasterOwnershipStatus Status,
	TerminalRasterOwnershipLossReason LossReason
);
