/*
	Icod.Terminal.RasterAnimation.Sample
	Sample application demonstrating Icod.Terminal RasterAnimation features.
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
namespace Icod.Terminal.RasterAnimation.Sample;

using Icod.Terminal;

internal static class RasterAnimationCompositionExample {
	internal static ValueTask<TerminalControlMutationResult> ComposeAsync(
		TerminalRasterAnimation animation,
		TerminalRasterAnimationFrame destination,
		CancellationToken cancellationToken = default
	) {
		ArgumentNullException.ThrowIfNull( animation );
		ArgumentNullException.ThrowIfNull( destination );
		return animation.ComposeFrameAsync(
			animation.RootFrame,
			destination,
			new TerminalRasterSourceRectangle( 0, 0, 1, 1 ),
			1,
			1,
			TerminalRasterFrameCompositionMode.Replace,
			cancellationToken
		);
	}
}
