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
namespace Icod.Terminal.Compatibility.Sample;

using System.Text;
using Icod.Terminal;

internal static class ClipboardCompatibilityScenarios {
	internal const string FixedTestValue = "Icod.Terminal compatibility clipboard value 1.26";

	internal static async ValueTask<CompatibilityLiveExecution> ExecuteAsync(
		TerminalSession session,
		CancellationToken cancellationToken
	) {
		ArgumentNullException.ThrowIfNull( session );
		await session.WriteClipboardAsync(
			TerminalClipboardSelection.Clipboard,
			FixedTestValue,
			cancellationToken
		).ConfigureAwait( false );
		byte[] payload = await session.ReadClipboardAsync(
			TerminalClipboardSelection.Clipboard,
			TimeSpan.FromSeconds( 2 ),
			cancellationToken
		).ConfigureAwait( false );
		bool matches = string.Equals(
			FixedTestValue,
			Encoding.UTF8.GetString( payload ),
			StringComparison.Ordinal
		);
		return new CompatibilityLiveExecution(
			matches,
			false,
			matches
				? "The fixed clipboard value completed a byte-for-byte round trip."
				: "The clipboard reply did not match the fixed test value.",
			ExplicitFailure: !matches
		);
	}
}
