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

using Icod.TermInfo;

/// <summary>Provides Terminal-owned screen dimensions and output transactions.</summary>
public sealed partial class TerminalSession {
	/// <summary>Queries current terminal dimensions through a Terminal-owned value.</summary>
	public TerminalControlResult<TerminalDimensions> GetDimensions() {
		TerminalControlResult<TerminalSize> size = this.GetSize();
		if ( size.IsAvailable ) {
			TerminalSize value = size.GetRequiredValue();
			return TerminalControlResult<TerminalDimensions>.Available(
				new TerminalDimensions( value.Columns, value.Rows )
			);
		}

		return size.Status switch {
			TerminalControlStatus.Unavailable =>
				TerminalControlResult<TerminalDimensions>.Unavailable( size.Message ),
			TerminalControlStatus.Unsupported =>
				TerminalControlResult<TerminalDimensions>.Unsupported( size.Message ),
			TerminalControlStatus.Failed =>
				TerminalControlResult<TerminalDimensions>.Failed(
					size.Message ?? "The terminal dimension query failed.",
					size.NativeErrorCode
				),
			_ => throw new InvalidOperationException( "The terminal dimension result is inconsistent." )
		};
	}

	/// <summary>Creates one bounded single-use screen-output transaction.</summary>
	public TerminalScreenOutputTransaction CreateScreenOutputTransaction(
		TerminalScreenOutputTransactionOptions? options = null
	) {
		return new TerminalScreenOutputTransaction(
			this,
			this.CaptureSessionOutputEpoch(),
			options ?? new TerminalScreenOutputTransactionOptions()
		);
	}
}
