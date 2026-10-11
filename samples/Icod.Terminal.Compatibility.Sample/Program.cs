/*
	Icod.Terminal.Compatibility.Sample
	Sample application demonstrating Icod.Terminal Compatibility features.
	Copyright (C) 2026  Timothy J. Bruce <uniblab@hotmail.com>
*/

/*
	This program is free software: you can redistribute it and/or modify
	it under the terms of the GNU General Public License as published by
	the Free Software Foundation, either version 3 of the License, or
	(at your option) any later version.

	This program is distributed in the hope that it will be useful,
	but WITHOUT ANY WARRANTY; without even the implied warranty of
	MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
	GNU General Public License for more details.

	You should have received a copy of the GNU General Public License
	along with this program.  If not, see <https://www.gnu.org/licenses/>.
*/
using Icod.Terminal;
using Icod.Terminal.Compatibility.Sample;

using CancellationTokenSource cancellation = new();
Console.CancelKeyPress += ( _, eventArgs ) => {
	eventArgs.Cancel = true;
	cancellation.Cancel();
};

return await CompatibilityCommandLine.RunAsync(
	args,
	Console.Out,
	Console.Error,
	cancellationToken => TerminalSession.OpenAsync(
		new TerminalSessionOptions {
			ClipboardOptions = args.Contains( "clipboard.osc52.large", StringComparer.Ordinal )
				|| args.Contains( "--run-all", StringComparer.Ordinal )
				? new() { MaximumReadBytes = 131_072, MaximumWriteBytes = 131_072 }
				: new()
		},
		cancellationToken: cancellationToken
	),
	cancellation.Token
);
