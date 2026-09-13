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

TerminalControlResult<TerminalRasterResource> parentResourceResult =
	await session.CreateRasterResourceAsync( image );
if ( TerminalControlStatus.Available != parentResourceResult.Status
	|| parentResourceResult.Value is null ) {
	await session.WriteTextAsync(
		FormatFailure(
			"Parent raster resource creation",
			parentResourceResult.Status,
			parentResourceResult.Message
		)
	);
	return 1;
}
await using TerminalRasterResource parentResource = parentResourceResult.Value;

TerminalRasterPlacementOptions parentOptions = new() {
	SourceRectangle = new TerminalRasterSourceRectangle(
		0,
		0,
		36,
		24
	),
	Columns = 24,
	ZIndex = -1
};
TerminalControlResult<TerminalRasterPlacement> parentPlacementResult =
	await parentResource.CreatePlacementAsync( parentOptions );
if ( TerminalControlStatus.Available != parentPlacementResult.Status
	|| parentPlacementResult.Value is null ) {
	await session.WriteTextAsync(
		FormatFailure(
			"Parent raster placement creation",
			parentPlacementResult.Status,
			parentPlacementResult.Message
		)
	);
	return 1;
}
await using TerminalRasterPlacement parentPlacement = parentPlacementResult.Value;

TerminalControlResult<TerminalRasterResource> childResourceResult =
	await session.CreateRasterResourceAsync( image );
if ( TerminalControlStatus.Available != childResourceResult.Status
	|| childResourceResult.Value is null ) {
	await session.WriteTextAsync(
		FormatFailure(
			"Child raster resource creation",
			childResourceResult.Status,
			childResourceResult.Message
		)
	);
	return 1;
}
await using TerminalRasterResource childResource = childResourceResult.Value;

TerminalControlResult<TerminalRasterPlacement> childPlacementResult =
	await childResource.CreateRelativePlacementAsync(
		parentPlacement,
		columnOffset: 2,
		rowOffset: -1,
		new TerminalRasterPlacementOptions {
			SourceRectangle = new TerminalRasterSourceRectangle(
				12,
				0,
				24,
				24
			),
			Columns = 12,
			Rows = 6,
			ZIndex = 1
		}
	);
if ( TerminalControlStatus.Available != childPlacementResult.Status
	|| childPlacementResult.Value is null ) {
	await session.WriteTextAsync(
		FormatFailure(
			"Relative raster placement creation",
			childPlacementResult.Status,
			childPlacementResult.Message
		)
	);
	return 1;
}
await using TerminalRasterPlacement childPlacement = childPlacementResult.Value;

await session.WriteTextAsync(
	"\r\nResource B now owns a placement positioned by signed cell offsets from Resource A's placement.\r\n"
);

TerminalControlMutationResult geometryUpdate = await childPlacement.UpdateAsync(
	new TerminalRasterPlacementOptions {
		SourceRectangle = new TerminalRasterSourceRectangle(
			18,
			0,
			24,
			24
		),
		Columns = 11,
		Rows = 5,
		ZIndex = 2
	}
);
if ( !geometryUpdate.Succeeded ) {
	await session.WriteTextAsync(
		FormatFailure(
			"Relative raster common-geometry update",
			geometryUpdate.Status,
			geometryUpdate.Message
		)
	);
	return 1;
}

await session.WriteTextAsync(
	"UpdateAsync changed the relative placement's common geometry while preserving its immutable parent and acknowledged offsets.\r\n"
);

TerminalControlMutationResult relativeUpdate = await childPlacement.UpdateRelativeAsync(
	columnOffset: -3,
	rowOffset: 2,
	new TerminalRasterPlacementOptions {
		SourceRectangle = new TerminalRasterSourceRectangle(
			18,
			0,
			24,
			24
		),
		Columns = 10,
		Rows = 5,
		ZIndex = 3
	}
);
if ( !relativeUpdate.Succeeded ) {
	await session.WriteTextAsync(
		FormatFailure(
			"Relative raster offset update",
			relativeUpdate.Status,
			relativeUpdate.Message
		)
	);
	return 1;
}

await session.WriteTextAsync(
	"UpdateRelativeAsync changed the signed offsets and common geometry without changing parentage.\r\n"
);
await parentPlacement.DisposeAsync();
await session.WriteTextAsync(
	"Disposing the parent placement cascaded descendant-placement cleanup; Resource B remains independently owned.\r\n"
);

TerminalControlResult<TerminalRasterPlacement> survivingPlacementResult =
	await childResource.CreatePlacementAsync(
		new TerminalRasterPlacementOptions {
			SourceRectangle = new TerminalRasterSourceRectangle(
				0,
				0,
				24,
				24
			),
			Columns = 12,
			Rows = 6,
			ZIndex = 0
		}
	);
if ( TerminalControlStatus.Available != survivingPlacementResult.Status
	|| survivingPlacementResult.Value is null ) {
	await session.WriteTextAsync(
		FormatFailure(
			"Independent child-resource placement creation after parent cascade",
			survivingPlacementResult.Status,
			survivingPlacementResult.Message
		)
	);
	return 1;
}
await using TerminalRasterPlacement survivingPlacement = survivingPlacementResult.Value;

await session.WriteTextAsync(
	"Resource B created a fresh ordinary placement after the parent cascade, proving resource ownership is independent from relative-placement lifetime.\r\n"
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
