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
	TerminalRasterResource,
	TerminalRasterPlacement,
	int,
	int,
	TerminalRasterPlacementOptions?,
	CancellationToken,
	ValueTask<TerminalControlResult<TerminalRasterPlacement>>
> createRelativePlacement = static (
	TerminalRasterResource resource,
	TerminalRasterPlacement parent,
	int columnOffset,
	int rowOffset,
	TerminalRasterPlacementOptions? options,
	CancellationToken cancellationToken
) => resource.CreateRelativePlacementAsync(
	parent,
	columnOffset,
	rowOffset,
	options,
	cancellationToken
);
Func<
	TerminalRasterResource,
	TerminalRasterPlaceholderOptions,
	CancellationToken,
	ValueTask<TerminalControlResult<TerminalRasterPlaceholder>>
> createPlaceholder = static (
	TerminalRasterResource resource,
	TerminalRasterPlaceholderOptions options,
	CancellationToken cancellationToken
) => resource.CreatePlaceholderAsync(
	options,
	cancellationToken
);
Func<
	TerminalRasterResource,
	TerminalRasterPlaceholder,
	int,
	int,
	TerminalRasterPlacementOptions?,
	CancellationToken,
	ValueTask<TerminalControlResult<TerminalRasterPlacement>>
> createRelativePlacementFromPlaceholder = static (
	TerminalRasterResource resource,
	TerminalRasterPlaceholder parent,
	int columnOffset,
	int rowOffset,
	TerminalRasterPlacementOptions? options,
	CancellationToken cancellationToken
) => resource.CreateRelativePlacementFromPlaceholderAsync(
	parent,
	columnOffset,
	rowOffset,
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
Func<
	TerminalRasterPlacement,
	int,
	int,
	TerminalRasterPlacementOptions?,
	CancellationToken,
	ValueTask<TerminalControlMutationResult>
> updateRelativePlacement = static (
	TerminalRasterPlacement placement,
	int columnOffset,
	int rowOffset,
	TerminalRasterPlacementOptions? options,
	CancellationToken cancellationToken
) => placement.UpdateRelativeAsync(
	columnOffset,
	rowOffset,
	options,
	cancellationToken
);
Func<
	TerminalRasterPlaceholder,
	int,
	int,
	TerminalRasterPlaceholderCell
> getPlaceholderCell = static (
	TerminalRasterPlaceholder placeholder,
	int row,
	int column
) => placeholder.GetCell(
	row,
	column
);
Func<
	TerminalSession,
	TerminalRasterPlaceholderCell,
	CancellationToken,
	ValueTask
> writePlaceholderCell = static (
	TerminalSession session,
	TerminalRasterPlaceholderCell cell,
	CancellationToken cancellationToken
) => session.WriteRasterPlaceholderCellAsync(
	cell,
	cancellationToken
);
Func<
	TerminalSession,
	ReadOnlyMemory<TerminalRasterPlaceholderCell>,
	CancellationToken,
	ValueTask
> writePlaceholderCells = static (
	TerminalSession session,
	ReadOnlyMemory<TerminalRasterPlaceholderCell> cells,
	CancellationToken cancellationToken
) => session.WriteRasterPlaceholderCellsAsync(
	cells,
	cancellationToken
);
_ = createResource;
_ = createPlacement;
_ = createRelativePlacement;
_ = createPlaceholder;
_ = createRelativePlacementFromPlaceholder;
_ = updatePlacement;
_ = updateRelativePlacement;
_ = getPlaceholderCell;
_ = writePlaceholderCell;
_ = writePlaceholderCells;

Require(
	9 == (int)TerminalCapability.PersistentRasterGraphics,
	"PersistentRasterGraphics must retain the reviewed additive enum value 9."
);
Require(
	10 == (int)TerminalCapability.UnicodeRasterPlaceholders,
	"UnicodeRasterPlaceholders must retain the frozen additive enum value 10."
);

TerminalRasterOwnershipState ownershipState = new(
	TerminalRasterOwnershipStatus.Current,
	TerminalRasterOwnershipLossReason.None
);
Require(
	TerminalRasterOwnershipStatus.Current == ownershipState.Status
		&& TerminalRasterOwnershipLossReason.None == ownershipState.LossReason,
	"TerminalRasterOwnershipState did not preserve the caller-supplied semantic snapshot."
);
Require(
	typeof( TerminalRasterResource ).GetProperty(
		nameof( TerminalRasterResource.OwnershipState ),
		BindingFlags.Instance | BindingFlags.Public
	)?.PropertyType == typeof( TerminalRasterOwnershipState ),
	"TerminalRasterResource must expose the 1.14 OwnershipState snapshot."
);
Require(
	typeof( TerminalRasterPlacement ).GetProperty(
		nameof( TerminalRasterPlacement.OwnershipState ),
		BindingFlags.Instance | BindingFlags.Public
	)?.PropertyType == typeof( TerminalRasterOwnershipState ),
	"TerminalRasterPlacement must expose the 1.14 OwnershipState snapshot."
);
Require(
	typeof( TerminalRasterPlaceholder ).GetProperty(
		nameof( TerminalRasterPlaceholder.OwnershipState ),
		BindingFlags.Instance | BindingFlags.Public
	)?.PropertyType == typeof( TerminalRasterOwnershipState ),
	"TerminalRasterPlaceholder must expose the shared OwnershipState snapshot."
);
string[] expectedOwnershipStatuses = [
	nameof( TerminalRasterOwnershipStatus.Current ),
	nameof( TerminalRasterOwnershipStatus.Stale ),
	nameof( TerminalRasterOwnershipStatus.Released ),
	nameof( TerminalRasterOwnershipStatus.Disposed )
];
Require(
	expectedOwnershipStatuses.SequenceEqual(
		Enum.GetNames<TerminalRasterOwnershipStatus>()
	),
	"TerminalRasterOwnershipStatus does not match the frozen semantic states."
);
string[] expectedOwnershipLossReasons = [
	nameof( TerminalRasterOwnershipLossReason.None ),
	nameof( TerminalRasterOwnershipLossReason.SessionStateLost ),
	nameof( TerminalRasterOwnershipLossReason.ResourceMissing ),
	nameof( TerminalRasterOwnershipLossReason.ParentPlacementLost ),
	nameof( TerminalRasterOwnershipLossReason.AncestorReleased ),
	nameof( TerminalRasterOwnershipLossReason.ResourceReleased ),
	nameof( TerminalRasterOwnershipLossReason.ExplicitDisposal )
];
Require(
	expectedOwnershipLossReasons.SequenceEqual(
		Enum.GetNames<TerminalRasterOwnershipLossReason>()
	),
	"TerminalRasterOwnershipLossReason does not match the frozen semantic reasons."
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

TerminalRasterPlaceholderOptions placeholderOptions = new() {
	Columns = 4,
	Rows = 3
};
Require(
	4 == placeholderOptions.Columns && 3 == placeholderOptions.Rows,
	"TerminalRasterPlaceholderOptions did not preserve semantic cell dimensions."
);
Require(
	typeof( TerminalRasterPlaceholder ).GetProperty(
		nameof( TerminalRasterPlaceholder.Columns ),
		BindingFlags.Instance | BindingFlags.Public
	)?.PropertyType == typeof( int )
		&& typeof( TerminalRasterPlaceholder ).GetProperty(
			nameof( TerminalRasterPlaceholder.Rows ),
			BindingFlags.Instance | BindingFlags.Public
		)?.PropertyType == typeof( int ),
	"TerminalRasterPlaceholder must expose semantic cell dimensions only."
);
Require(
	typeof( TerminalRasterPlaceholderCell ).GetProperty(
		nameof( TerminalRasterPlaceholderCell.Row ),
		BindingFlags.Instance | BindingFlags.Public
	)?.PropertyType == typeof( int )
		&& typeof( TerminalRasterPlaceholderCell ).GetProperty(
			nameof( TerminalRasterPlaceholderCell.Column ),
			BindingFlags.Instance | BindingFlags.Public
		)?.PropertyType == typeof( int ),
	"TerminalRasterPlaceholderCell must expose semantic row/column coordinates."
);
Require(
	0 == typeof( TerminalRasterPlaceholder ).GetConstructors(
		BindingFlags.Instance | BindingFlags.Public
	).Length,
	"TerminalRasterPlaceholder must not expose a public constructor."
);
Require(
	0 == typeof( TerminalRasterPlaceholderCell ).GetConstructors(
		BindingFlags.Instance | BindingFlags.Public
	).Length,
	"TerminalRasterPlaceholderCell must not expose a public constructor."
);
Require(
	typeof( IAsyncDisposable ).IsAssignableFrom( typeof( TerminalRasterResource ) ),
	"TerminalRasterResource must remain asynchronously disposable."
);
Require(
	typeof( IAsyncDisposable ).IsAssignableFrom( typeof( TerminalRasterPlacement ) ),
	"TerminalRasterPlacement must remain asynchronously disposable."
);
Require(
	typeof( IAsyncDisposable ).IsAssignableFrom( typeof( TerminalRasterPlaceholder ) ),
	"TerminalRasterPlaceholder must be asynchronously disposable."
);

AssertOpaquePublicSurface( typeof( TerminalRasterOwnershipState ) );
AssertOpaquePublicSurface( typeof( TerminalRasterResource ) );
AssertOpaquePublicSurface( typeof( TerminalRasterPlacement ) );
AssertOpaquePublicSurface( typeof( TerminalRasterPlaceholderOptions ) );
AssertOpaquePublicSurface( typeof( TerminalRasterPlaceholder ) );
AssertOpaquePublicSurface( typeof( TerminalRasterPlaceholderCell ) );
Require(
	typeof( TerminalRasterPlacement ).GetProperty(
		"Parent",
		BindingFlags.Instance | BindingFlags.Public
	) is null,
	"TerminalRasterPlacement must not expose parentage as mutable or protocol-facing public state."
);

static void AssertOpaquePublicSurface(
	Type type
) {
	ArgumentNullException.ThrowIfNull( type );
	string[] forbiddenFragments = [
		"ImageId",
		"ImageNumber",
		"PlacementId",
		"ParentId",
		"Generation",
		"Backend",
		"Kitty",
		"Apc",
		"Encoding"
	];
	MemberInfo[] members = type.GetMembers(
		BindingFlags.Instance | BindingFlags.Public
		| BindingFlags.Static
	);
	foreach ( string fragment in forbiddenFragments ) {
		Require(
			!members.Any(
				member => member.Name.Contains(
					fragment,
					StringComparison.OrdinalIgnoreCase
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
