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
/// Describes the strongest completion evidence observed for a successful
/// terminal-state mutation.
/// </summary>
public enum TerminalControlMutationConfirmation {
	/// <summary>The mutation does not classify its completion evidence.</summary>
	Unspecified = 0,

	/// <summary>
	/// The complete command crossed the serialized write and flush boundary,
	/// without a correlated protocol acknowledgement.
	/// </summary>
	OutputCommitted = 1,

	/// <summary>
	/// A correlated protocol success response acknowledged the mutation.
	/// </summary>
	ProtocolAcknowledged = 2
}
