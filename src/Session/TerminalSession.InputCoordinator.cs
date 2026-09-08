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
/// Session-owned single-reader input coordination shared by application input
/// and internal terminal query transactions.
/// </summary>
public sealed partial class TerminalSession {
	private readonly object inputCoordinatorSync = new();
	private TerminalInputCoordinator? inputCoordinator;

	internal TerminalInputCoordinator GetInputCoordinator() {
		lock ( this.inputCoordinatorSync ) {
			if ( this.inputCoordinator is not null ) {
				return this.inputCoordinator;
			}

			TerminalInputDecoderOptions decoderOptions = this.Options.InputDecoderOptions;
			TerminalInputDecoder decoder = this.inputDecoder ??= new TerminalInputDecoder(
				this.Input,
				this.Terminal,
				this.Options.MonotonicClock,
				decoderOptions.EscapeSequenceTimeout,
				decoderOptions.MaximumBufferedBytes,
				decoderOptions.PasteChunkBytes
			);
			this.inputCoordinator = new TerminalInputCoordinator(
				decoder,
				this.lifecycleStop.Token
			);
			return this.inputCoordinator;
		}
	}
}
