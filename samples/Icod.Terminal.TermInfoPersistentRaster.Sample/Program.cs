/*
	Icod.Terminal.TermInfoPersistentRaster.Sample
	Sample application demonstrating Icod.Terminal TermInfoPersistentRaster features.
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
using Icod.TermInfo.Inspection;
using Icod.Terminal;

const int width = 48;
const int height = 24;
const string liveEvidenceSource = "Icod.Terminal.live-capability-verification";

PersistentRasterLifecycleRequest request = new(
	uploadResource: true,
	placementCount: 1,
	updatePlacement: true,
	deletePlacement: true,
	deleteResource: true,
	requireAcknowledgedUpload: true
);

await using TerminalSession session = await TerminalSession.OpenAsync(
	new TerminalSessionOptions {
		InputMode = TerminalInputMode.CBreak,
		EchoInput = false
	}
);

PersistentRasterLifecycleProfile profile =
	PersistentRasterLifecycleInspector.Inspect( session.Terminal );
PersistentRasterLifecyclePlan plan =
	PersistentRasterLifecyclePlanner.Plan( profile, request );

await session.WriteTextAsync(
	$"Static TermInfo persistent-raster plan: {plan.Status}.\r\n"
);

if ( plan.RequiresRuntimeVerification ) {
	TerminalCapabilityStatus beforeVerification = session.InspectCapability(
		TerminalCapability.PersistentRasterGraphics
	);
	if (
		TerminalCapabilityEndpointAvailability.Available
			!= beforeVerification.EndpointAvailability
	) {
		await session.WriteTextAsync(
			"The static plan needs live verification, but the required interactive terminal endpoint is unavailable.\r\n"
		);
		await WritePlanIssuesAsync( session, plan );
		return 2;
	}

	TerminalCapabilityStatus verified = await session.VerifyCapabilityAsync(
		TerminalCapability.PersistentRasterGraphics
	);
	IReadOnlyList<PersistentRasterLifecycleEvidence> liveEvidence =
		CreatePersistentRasterEvidence(
			verified,
			GetNextSourceOrdinal( profile.Evidence )
		);
	if ( 0 == liveEvidence.Count ) {
		await session.WriteTextAsync(
			"Terminal verification did not produce conclusive persistent-raster support evidence.\r\n"
		);
		return 2;
	}

	List<PersistentRasterLifecycleEvidence> strengthenedEvidence =
		profile.Evidence.ToList();
	strengthenedEvidence.AddRange( liveEvidence );
	profile = PersistentRasterLifecycleClassifier.Classify( strengthenedEvidence );
	plan = PersistentRasterLifecyclePlanner.Plan( profile, request );

	await session.WriteTextAsync(
		$"Plan after live Terminal verification: {plan.Status}.\r\n"
	);
}

if ( PersistentRasterLifecyclePlanStatus.Success != plan.Status ) {
	await session.WriteTextAsync(
		$"Persistent-raster lifecycle execution is not admissible: {plan.Status}.\r\n"
	);
	await WritePlanIssuesAsync( session, plan );
	return 2;
}

TerminalCapabilityStatus executionCapability = session.InspectCapability(
	TerminalCapability.PersistentRasterGraphics
);
if ( !executionCapability.IsUsable ) {
	await session.WriteTextAsync(
		"TermInfo planning permits the requested lifecycle, but Icod.Terminal does not currently have a usable live persistent-raster route for this endpoint.\r\n"
	);
	return 2;
}

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

TerminalControlResult<TerminalRasterPlacement> placementResult =
	await resource.CreatePlacementAsync(
		new TerminalRasterPlacementOptions {
			Columns = 24
		}
	);
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

TerminalControlMutationResult update = await placement.UpdateAsync(
	new TerminalRasterPlacementOptions {
		Columns = 16
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
	"Persistent-raster lifecycle execution succeeded; disposal now releases placement and resource ownership.\r\n"
);
return 0;

static IReadOnlyList<PersistentRasterLifecycleEvidence> CreatePersistentRasterEvidence(
	TerminalCapabilityStatus status,
	int sourceOrdinal
) {
	if ( 0 > sourceOrdinal ) {
		throw new ArgumentOutOfRangeException( nameof( sourceOrdinal ) );
	}
	if ( TerminalCapability.PersistentRasterGraphics != status.Capability
		|| TerminalCapabilityEvidenceKind.LiveObservation != status.EvidenceKind ) {
		return Array.Empty<PersistentRasterLifecycleEvidence>();
	}

	bool? isPositive = status.Support switch {
		TerminalCapabilitySupport.Verified => true,
		TerminalCapabilitySupport.Unsupported => false,
		_ => null
	};
	if ( !isPositive.HasValue ) {
		return Array.Empty<PersistentRasterLifecycleEvidence>();
	}

	PersistentRasterLifecycleEvidenceSubject[] subjects = [
		PersistentRasterLifecycleEvidenceSubject.PersistentUpload,
		PersistentRasterLifecycleEvidenceSubject.AcknowledgedUpload,
		PersistentRasterLifecycleEvidenceSubject.PlacementCreation,
		PersistentRasterLifecycleEvidenceSubject.MultiplePlacements,
		PersistentRasterLifecycleEvidenceSubject.PlacementUpdate,
		PersistentRasterLifecycleEvidenceSubject.PlacementDeletion,
		PersistentRasterLifecycleEvidenceSubject.ResourceDeletion
	];
	if ( sourceOrdinal > int.MaxValue - ( subjects.Length - 1 ) ) {
		throw new ArgumentOutOfRangeException( nameof( sourceOrdinal ) );
	}

	PersistentRasterLifecycleEvidence[] result =
		new PersistentRasterLifecycleEvidence[ subjects.Length ];
	for ( int index = 0; index < subjects.Length; ++index ) {
		result[ index ] = new PersistentRasterLifecycleEvidence(
			subjects[ index ],
			isPositive.Value,
			PersistentRasterLifecycleEvidenceKind.Verified,
			liveEvidenceSource,
			sourceOrdinal + index
		);
	}
	return result;
}

static int GetNextSourceOrdinal(
	IReadOnlyList<PersistentRasterLifecycleEvidence> evidence
) {
	ArgumentNullException.ThrowIfNull( evidence );
	return 0 == evidence.Count
		? 0
		: checked( evidence.Max( item => item.SourceOrdinal ) + 1 );
}

static async ValueTask WritePlanIssuesAsync(
	TerminalSession session,
	PersistentRasterLifecyclePlan plan
) {
	ArgumentNullException.ThrowIfNull( session );
	ArgumentNullException.ThrowIfNull( plan );
	foreach ( PersistentRasterLifecyclePlanIssue issue in plan.Issues ) {
		await session.WriteTextAsync(
			string.Concat(
				"  ",
				issue.Operation.ToString(),
				": ",
				issue.Subject.ToString(),
				" is ",
				issue.SupportStatus.ToString(),
				issue.RequiresRuntimeVerification
					? " (runtime verification may strengthen this)."
					: ".",
				"\r\n"
			)
		);
	}
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

static byte ScaleChannel(
	int value,
	int maximum
) {
	if ( 0 >= maximum ) {
		return 0;
	}
	return (byte)( ( value * byte.MaxValue ) / maximum );
}
