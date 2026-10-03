/*
	Icod.Terminal.RasterGraphics.Sample
	Sample application demonstrating Icod.Terminal RasterGraphics features.
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

const int width = 96;
const int height = 48;

byte[] pixels = new byte[ width * height * 3 ];
for ( int row = 0; row < height; row++ ) {
	for ( int column = 0; column < width; column++ ) {
		int offset = ( ( row * width ) + column ) * 3;
		pixels[ offset ] = ScaleChannel( column, width - 1 );
		pixels[ offset + 1 ] = ScaleChannel( row, height - 1 );
		pixels[ offset + 2 ] = ScaleChannel( column + row, width + height - 2 );
	}
}

TerminalRasterImage image = TerminalRasterImage.CreateRgb24(
	width,
	height,
	pixels
);

await using TerminalSession session = await TerminalSession.OpenAsync(
	new TerminalSessionOptions {
		InputMode = TerminalInputMode.CBreak,
		EchoInput = false
	}
);

await session.WriteTextAsync(
	"Icod.Terminal backend-neutral raster sample.\r\n"
);
await session.WriteTextAsync(
	"One complete frame is submitted through verified Kitty Graphics or Sixel.\r\n"
);

TerminalCapabilityStatus capability = await session.VerifyCapabilityAsync(
	TerminalCapability.RasterGraphics
);
if ( capability.IsUsable ) {
	TerminalScreenOutputTransaction transaction = session.CreateScreenOutputTransaction();
	TerminalScreenOperationPlan? origin = session.Screen.PlanCursorMove(
		null,
		new TerminalScreenPosition( 2, 0 )
	);
	if ( origin.HasValue ) {
		transaction.Add( origin.Value );
	}
	transaction.WriteRaster( image );
	await transaction.CommitAsync();
	return 0;
}

await session.WriteTextAsync(
	string.Concat(
		"\r\nNo verified raster graphics backend: ",
		capability.Support.ToString(),
		"\r\n"
	)
);
return 1;

static byte ScaleChannel(
	int value,
	int maximum
) {
	if ( 0 >= maximum ) {
		return 0;
	}

	return (byte)( ( value * byte.MaxValue ) / maximum );
}
