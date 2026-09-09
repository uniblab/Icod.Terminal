/*
	Icod.Terminal.PackageRasterGraphicsSmoke
	Package smoke-test utility for Icod.Terminal release and compatibility contracts.
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
using System.Reflection;
using Icod.Terminal;

Func<TerminalSession, TerminalRasterImage, CancellationToken, ValueTask<TerminalControlMutationResult>> display =
	BindDisplayRaster;
_ = display;

RequireEnumValue(
	TerminalRasterPixelFormat.Rgb24,
	0
);
RequireEnumValue(
	TerminalRasterPixelFormat.Rgba32,
	1
);
RequireEnumValue(
	TerminalRasterPixelFormat.Indexed8,
	2
);

TerminalRasterColor defaultAlpha = new(
	10,
	20,
	30
);
Require(
	byte.MaxValue == defaultAlpha.Alpha,
	"TerminalRasterColor must default alpha to 255."
);
Require(
	10 == defaultAlpha.Red
		&& 20 == defaultAlpha.Green
		&& 30 == defaultAlpha.Blue,
	"TerminalRasterColor did not preserve RGB channel values."
);

byte[] rgbPixels = [
	1, 2, 3,
	4, 5, 6
];
TerminalRasterImage rgb = TerminalRasterImage.CreateRgb24(
	2,
	1,
	rgbPixels
);
rgbPixels[ 0 ] = byte.MaxValue;
Require(
	2 == rgb.Width
		&& 1 == rgb.Height
		&& 2 == rgb.PixelCount
		&& TerminalRasterPixelFormat.Rgb24 == rgb.PixelFormat,
	"RGB24 raster metadata is incorrect."
);
Require(
	new TerminalRasterColor( 1, 2, 3 ) == rgb.GetPixelColor( 0, 0 ),
	"RGB24 raster did not snapshot caller storage."
);

byte[] rgbaPixels = [ 7, 8, 9, 10 ];
TerminalRasterImage rgba = TerminalRasterImage.CreateRgba32(
	1,
	1,
	rgbaPixels
);
rgbaPixels[ 3 ] = byte.MaxValue;
Require(
	new TerminalRasterColor( 7, 8, 9, 10 ) == rgba.GetPixelColor( 0, 0 ),
	"RGBA32 raster did not preserve straight alpha or snapshot caller storage."
);

byte[] indices = [ 1 ];
TerminalRasterColor[] palette = [
	new TerminalRasterColor( 11, 12, 13 ),
	new TerminalRasterColor( 21, 22, 23, 24 )
];
TerminalRasterImage indexed = TerminalRasterImage.CreateIndexed8(
	1,
	1,
	indices,
	palette
);
indices[ 0 ] = 0;
palette[ 1 ] = new TerminalRasterColor( 31, 32, 33, 34 );
Require(
	TerminalRasterPixelFormat.Indexed8 == indexed.PixelFormat
		&& new TerminalRasterColor( 21, 22, 23, 24 ) == indexed.GetPixelColor( 0, 0 ),
	"Indexed8 raster did not snapshot index/palette storage."
);

string[] forbiddenSessionMethods = [
	"WriteDcsAsync",
	"WriteRawDcsAsync",
	"WriteSixelAsync",
	"WriteRawSixelAsync",
	"DisplaySixelAsync",
	"SetSixelPaletteAsync"
];
MethodInfo[] sessionMethods = typeof( TerminalSession ).GetMethods(
	BindingFlags.Public | BindingFlags.Instance
);
foreach ( string forbiddenName in forbiddenSessionMethods ) {
	Require(
		!sessionMethods.Any(
			method => string.Equals(
				method.Name,
				forbiddenName,
				StringComparison.Ordinal
			)
		),
		$"The shipped public surface exposes excluded protocol-specific method '{forbiddenName}'."
	);
}

Require(
	typeof( TerminalRasterImage ).GetProperty(
		"PixelBytes",
		BindingFlags.Public | BindingFlags.Instance
	) is null,
	"TerminalRasterImage must not expose its owned pixel backing memory publicly."
);
Require(
	typeof( TerminalRasterImage ).GetProperty(
		"Palette",
		BindingFlags.Public | BindingFlags.Instance
	) is null,
	"TerminalRasterImage must not expose its owned palette backing memory publicly."
);

static ValueTask<TerminalControlMutationResult> BindDisplayRaster(
	TerminalSession session,
	TerminalRasterImage image,
	CancellationToken cancellationToken
) {
	ArgumentNullException.ThrowIfNull( session );
	ArgumentNullException.ThrowIfNull( image );
	return session.DisplayRasterAsync(
		image,
		cancellationToken
	);
}

static void RequireEnumValue<TEnum>(
	TEnum value,
	int expected
) where TEnum : struct, Enum {
	int actual = Convert.ToInt32( value );
	Require(
		expected == actual,
		$"The enum value '{typeof( TEnum ).Name}.{value}' is {actual}; expected {expected}."
	);
}

static void Require(
	bool condition,
	string message
) {
	ArgumentException.ThrowIfNullOrWhiteSpace( message );
	if ( !condition ) {
		throw new InvalidOperationException( message );
	}
}
