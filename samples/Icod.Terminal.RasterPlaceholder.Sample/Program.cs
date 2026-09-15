/*
	Icod.Terminal.RasterPlaceholder.Sample
	Sample application demonstrating Icod.Terminal RasterPlaceholder features.
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
using Icod.TermInfo;

const int imageWidth = 4;
const int imageHeight = 2;
const int placeholderColumns = 4;
const int placeholderRows = 2;
const int baseRow = 4;
const int baseColumn = 8;

TerminalRasterImage image = TerminalRasterImage.CreateRgb24(
	imageWidth,
	imageHeight,
	[
		255, 64, 64,
		255, 192, 64,
		192, 255, 64,
		64, 255, 128,
		64, 192, 255,
		96, 96, 255,
		192, 96, 255,
		255, 96, 192,
	]
);

await using TerminalSession session = await TerminalSession.OpenAsync(
	new TerminalSessionOptions {
		InputMode = TerminalInputMode.CBreak,
		EchoInput = false
	}
);

await session.WriteTextAsync(
	"Icod.Terminal semantic raster-placeholder sample.\r\n"
);
await session.WriteTextAsync(
	"The application owns cursor position and redraw order; Terminal owns the opaque placeholder identity and cell encoding.\r\n"
);

TerminalCapabilityStatus persistentCapability = await session.VerifyCapabilityAsync(
	TerminalCapability.PersistentRasterGraphics
);
if ( !persistentCapability.IsUsable ) {
	await session.WriteTextAsync(
		string.Concat(
			"Persistent raster graphics are not currently usable: ",
			persistentCapability.Support.ToString(),
			".\r\n"
		)
	);
	return 1;
}

TerminalCapabilityStatus placeholderCapability = session.InspectCapability(
	TerminalCapability.UnicodeRasterPlaceholders
);
if ( TerminalCapabilitySupport.Unsupported == placeholderCapability.Support ) {
	await session.WriteTextAsync(
		"Raster placeholders are known to be unsupported by the current semantic route.\r\n"
	);
	return 1;
}

if ( session.Terminal.GetString( StringCapability.CursorAddress ) is null ) {
	await session.WriteTextAsync(
		"This sample requires the terminal's ordinary CursorAddress capability so the caller can position placeholder cells.\r\n"
	);
	return 1;
}

TerminalControlResult<TerminalRasterResource> resourceResult =
	await session.CreateRasterResourceAsync( image );
if ( TerminalControlStatus.Available != resourceResult.Status
	|| resourceResult.Value is null ) {
	await session.WriteTextAsync(
		FormatFailure(
			"Raster resource creation",
			resourceResult.Status,
			resourceResult.Message
		)
	);
	return 1;
}
await using TerminalRasterResource resource = resourceResult.Value;

TerminalControlResult<TerminalRasterPlaceholder> placeholderResult =
	await resource.CreatePlaceholderAsync(
		new TerminalRasterPlaceholderOptions {
			Columns = placeholderColumns,
			Rows = placeholderRows
		}
	);
if ( TerminalControlStatus.Available != placeholderResult.Status
	|| placeholderResult.Value is null ) {
	await session.WriteTextAsync(
		FormatFailure(
			"Raster placeholder creation",
			placeholderResult.Status,
			placeholderResult.Message
		)
	);
	return 1;
}
await using TerminalRasterPlaceholder placeholder = placeholderResult.Value;

TerminalCapabilityStatus observedPlaceholderCapability = session.InspectCapability(
	TerminalCapability.UnicodeRasterPlaceholders
);
if ( !observedPlaceholderCapability.IsUsable ) {
	await session.WriteTextAsync(
		"Successful placeholder creation did not publish usable semantic capability evidence.\r\n"
	);
	return 1;
}

for ( int row = 0; row < placeholder.Rows; ++row ) {
	TerminalRasterPlaceholderCell[] cells = new TerminalRasterPlaceholderCell[
		placeholder.Columns
	];
	for ( int column = 0; column < cells.Length; ++column ) {
		cells[ column ] = placeholder.GetCell(
			row,
			column
		);
	}

	await MoveCursorAsync(
		session,
		baseRow + row,
		baseColumn
	);
	await session.WriteRasterPlaceholderCellsAsync( cells );
}

await MoveCursorAsync(
	session,
	baseRow + 1,
	baseColumn + 2
);
await session.WriteRasterPlaceholderCellAsync(
	placeholder.GetCell(
		row: 1,
		column: 2
	)
);

TerminalRasterPlaceholderCell[] reordered = [
	placeholder.GetCell( 1, 3 ),
	placeholder.GetCell( 0, 1 ),
	placeholder.GetCell( 1, 0 ),
];
await MoveCursorAsync(
	session,
	baseRow + placeholder.Rows + 1,
	baseColumn
);
await session.WriteRasterPlaceholderCellsAsync( reordered );

TerminalControlResult<TerminalRasterPlacement> childResult =
	await resource.CreateRelativePlacementFromPlaceholderAsync(
		placeholder,
		columnOffset: placeholder.Columns + 1,
		rowOffset: 0,
		new TerminalRasterPlacementOptions {
			Columns = 2,
			Rows = 1,
			ZIndex = 1
		}
	);
if ( TerminalControlStatus.Available != childResult.Status
	|| childResult.Value is null ) {
	await session.WriteTextAsync(
		FormatFailure(
			"Physical child placement relative to the virtual placeholder",
			childResult.Status,
			childResult.Message
		)
	);
	return 1;
}
await using TerminalRasterPlacement child = childResult.Value;

await MoveCursorAsync(
	session,
	baseRow + placeholder.Rows + 3,
	0
);
await session.WriteTextAsync(
	"Rendered a complete 4x2 grid, redrew one cell sparsely, emitted three cells in caller-selected order, and created a physical child relative to the virtual placeholder.\r\n"
);
await session.WriteTextAsync(
	"All remaining placeholder, placement, and resource ownership is released deterministically.\r\n"
);
return 0;

static async ValueTask MoveCursorAsync(
	TerminalSession session,
	int row,
	int column
) {
	ArgumentNullException.ThrowIfNull( session );
	string motion = session.Terminal.Expand(
		StringCapability.CursorAddress,
		row,
		column
	);
	await session.WriteTerminalStringAsync( motion );
}

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
