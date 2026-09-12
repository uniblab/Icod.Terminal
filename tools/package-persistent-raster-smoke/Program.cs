/*
	Icod.Terminal.PackagePersistentRasterSmoke
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

Func<
	TerminalSession,
	TerminalRasterImage,
	CancellationToken,
	ValueTask<TerminalControlResult<TerminalRasterResource>>
> createResource = static (
	TerminalSession session,
	TerminalRasterImage image,
	CancellationToken cancellationToken
) => session.CreateRasterResourceAsync(
	image,
	cancellationToken
);
Func<
	TerminalRasterResource,
	TerminalRasterPlacementOptions?,
	CancellationToken,
	ValueTask<TerminalControlResult<TerminalRasterPlacement>>
> createPlacement = static (
	TerminalRasterResource resource,
	TerminalRasterPlacementOptions? options,
	CancellationToken cancellationToken
) => resource.CreatePlacementAsync(
	options,
	cancellationToken
);
Func<
	TerminalRasterPlacement,
	TerminalRasterPlacementOptions?,
	CancellationToken,
	ValueTask<TerminalControlMutationResult>
> updatePlacement = static (
	TerminalRasterPlacement placement,
	TerminalRasterPlacementOptions? options,
	CancellationToken cancellationToken
) => placement.UpdateAsync(
	options,
	cancellationToken
);
_ = createResource;
_ = createPlacement;
_ = updatePlacement;

Require(
	9 == (int)TerminalCapability.PersistentRasterGraphics,
	"PersistentRasterGraphics must retain the reviewed additive enum value 9."
);

TerminalRasterSourceRectangle rectangle = new(
	0,
	0,
	1,
	1
);
TerminalRasterPlacementOptions options = new() {
	SourceRectangle = rectangle,
	Columns = 12,
	Rows = 6,
	ZIndex = -1
};
Require(
	12 == options.Columns && 6 == options.Rows,
	"TerminalRasterPlacementOptions did not preserve caller-supplied cell extents."
);
Require(
	options.SourceRectangle is TerminalRasterSourceRectangle storedRectangle
		&& 0 == storedRectangle.X
		&& 0 == storedRectangle.Y
		&& 1 == storedRectangle.Width
		&& 1 == storedRectangle.Height,
	"TerminalRasterPlacementOptions did not preserve the caller-supplied source rectangle."
);
Require(
	-1 == options.ZIndex,
	"TerminalRasterPlacementOptions did not preserve caller-supplied z-order."
);
Require(
	typeof( IAsyncDisposable ).IsAssignableFrom( typeof( TerminalRasterResource ) ),
	"TerminalRasterResource must remain asynchronously disposable."
);
Require(
	typeof( IAsyncDisposable ).IsAssignableFrom( typeof( TerminalRasterPlacement ) ),
	"TerminalRasterPlacement must remain asynchronously disposable."
);

AssertOpaquePublicSurface( typeof( TerminalRasterResource ) );
AssertOpaquePublicSurface( typeof( TerminalRasterPlacement ) );

static void AssertOpaquePublicSurface(
	Type type
) {
	ArgumentNullException.ThrowIfNull( type );
	string[] forbiddenFragments = [
		"ImageId",
		"ImageNumber",
		"PlacementId",
		"Backend",
		"Kitty"
	];
	MemberInfo[] members = type.GetMembers(
		BindingFlags.Instance | BindingFlags.Public
	);
	foreach ( string fragment in forbiddenFragments ) {
		Require(
			!members.Any(
				member => member.Name.Contains(
					fragment,
					StringComparison.Ordinal
				)
			),
			$"{type.Name} exposes forbidden protocol-specific public member text '{fragment}'."
		);
	}
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
