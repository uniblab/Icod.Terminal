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
/// Represents optional extended metadata for one OSC 133 command-output marker.
/// </summary>
/// <remarks>
/// The default value is valid and emits the portable bare OSC 133 <c>C</c> marker.
/// A non-null command line is published through <c>cmdline_url</c> using strict UTF-8
/// byte percent encoding. Publishing command-line metadata is explicit caller intent;
/// the library does not inspect shell history, capture process command lines, or redact secrets.
/// </remarks>
public readonly struct TerminalSemanticCommandOutputOptions {
	/// <summary>
	/// Initializes OSC 133 command-output metadata.
	/// </summary>
	/// <param name="commandLine">
	/// The command line to publish, or <see langword="null"/> to publish no command-line metadata.
	/// An empty string is distinct from <see langword="null"/> and publishes an explicitly empty
	/// <c>cmdline_url</c> value.
	/// </param>
	public TerminalSemanticCommandOutputOptions(
		string? commandLine = null
	) {
		this.CommandLine = commandLine;
	}

	/// <summary>
	/// Gets the exact caller-supplied command line to publish, or <see langword="null"/> when
	/// no command-line metadata should be emitted.
	/// </summary>
	/// <remarks>
	/// Command lines can contain credentials, tokens, private paths, host names, or other
	/// sensitive material. Percent encoding protects OSC framing; it does not provide confidentiality.
	/// </remarks>
	public string? CommandLine {
		get;
	}
}
