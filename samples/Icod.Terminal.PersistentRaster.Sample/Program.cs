/*
	Icod.Terminal.PersistentRaster.Sample
	Sample application demonstrating Icod.Terminal PersistentRaster features.
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

const int width = 48;
const int height = 24;

byte[] pixels = new byte[ width * height * 3 ];
for ( int row = 0; row < height; ++row ) {
	for ( int column = 0; column < width; ++column ) {
		int offset = ( ( row * width ) + column ) * 3;
		pixels[ offset ] = ScaleChannel( column, width - 1 );
		pixels[ offset + 1 ] = ScaleChannel( row, height - 1 );
		pixels[ offset + 2 ] = ScaleChannel(
			column + row,
			width + height - 2
		);
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
	"Icod.Terminal persistent backend-neutral raster sample.\r\n"
);
await session.WriteTextAsync(
	"The sample verifies the semantic capability without selecting a terminal brand or graphics backend.\r\n"
);

TerminalCapabilityStatus capability = await session.VerifyCapabilityAsync(
	TerminalCapability.PersistentRasterGraphics
);
if ( !capability.IsUsable ) {
	await session.WriteTextAsync(
		string.Concat(
			"Persistent raster graphics are not currently usable: ",
			capability.Support.ToString(),
			".\r\n"
		)
	);
	return 1;
}

TerminalControlResult<TerminalRasterResource> resourceResult =
	await session.CreateRasterResourceAsync( image );
if ( TerminalControlStatus.Available != resourceResult.Status
	|| resourceResult.Value is null ) {
	await session.WriteTextAsync(
		FormatFailure(
			"Persistent raster resource creation",
			resourceResult.Status,
			resourceResult.Message
		)
	);
	return 1;
}
await using TerminalRasterResource resource = resourceResult.Value;

TerminalRasterPlacementOptions placementOptions = new() {
	SourceRectangle = new TerminalRasterSourceRectangle(
		0,
		0,
		36,
		24
	),
	Columns = 24,
	ZIndex = -1
};
TerminalControlResult<TerminalRasterPlacement> placementResult =
	await resource.CreatePlacementAsync( placementOptions );
if ( TerminalControlStatus.Available != placementResult.Status
	|| placementResult.Value is null ) {
	await session.WriteTextAsync(
		FormatFailure(
			"Persistent raster placement creation",
			placementResult.Status,
			placementResult.Message
		)
	);
	return 1;
}
await using TerminalRasterPlacement placement = placementResult.Value;

await session.WriteTextAsync(
	"\r\nThe placement uses a source-pixel crop and relative z-order while the terminal-resident resource remains owned.\r\n"
);
TerminalControlMutationResult update = await placement.UpdateAsync(
	new TerminalRasterPlacementOptions {
		SourceRectangle = new TerminalRasterSourceRectangle(
			12,
			0,
			36,
			24
		),
		Columns = 16,
		ZIndex = 1
	}
);
if ( !update.Succeeded ) {
	await session.WriteTextAsync(
		FormatFailure(
			"Persistent raster placement update",
			update.Status,
			update.Message
		)
	);
	return 1;
}

await session.WriteTextAsync(
	"\r\nThe crop and relative stacking intent were updated; disposal will release placement and resource ownership.\r\n"
);
return 0;

static string FormatFailure(
	string operation,
	TerminalControlStatus status,
	string? message
) {
	ArgumentException.ThrowIfNullOrWhiteSpace( operation );
	return string.Concat(
		operation,
		" was not completed: ",
		status.ToString(),
		string.IsNullOrEmpty( message )
			? string.Empty
			: string.Concat( " — ", message ),
		"\r\n"
	);
}

static byte ScaleChannel(
	int value,
	int maximum
) {
	if ( 0 >= maximum ) {
		return 0;
	}

	return (byte)( ( value * byte.MaxValue ) / maximum );
}
