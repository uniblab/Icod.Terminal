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
using Icod.Terminal;

TerminalRasterImage rootImage = CreateFrame(
	255, 72, 72,
	255, 196, 72,
	196, 255, 72,
	72, 255, 160
);
TerminalRasterImage secondImage = CreateFrame(
	72, 196, 255,
	96, 96, 255,
	196, 96, 255,
	255, 96, 196
);
TerminalRasterImage thirdImage = CreateFrame(
	255, 96, 196,
	196, 96, 255,
	96, 96, 255,
	72, 196, 255
);
TerminalRasterImage streamedImage = CreateFrame(
	72, 255, 160,
	196, 255, 72,
	255, 196, 72,
	255, 72, 72
);

await using TerminalSession session = await TerminalSession.OpenAsync(
	new TerminalSessionOptions {
		InputMode = TerminalInputMode.CBreak,
		EchoInput = false
	}
);

await session.WriteTextAsync(
	"Icod.Terminal semantic persistent-raster animation sample.\r\n"
);
await session.WriteTextAsync(
	"The application supplies frames and presentation intent; Terminal owns opaque frame identity, sequence certainty, acknowledged control, and cleanup.\r\n"
);

TerminalCapabilityStatus capability = await session.VerifyCapabilityAsync(
	TerminalCapability.PersistentRasterAnimation
);
if ( !capability.IsUsable ) {
	await session.WriteTextAsync(
		string.Concat(
			"Persistent raster animation is not currently usable: ",
			capability.Support.ToString(),
			".\r\n"
		)
	);
	return 1;
}

TerminalControlResult<TerminalRasterResource> resourceResult =
	await session.CreateRasterResourceAsync( rootImage );
if ( TerminalControlStatus.Available != resourceResult.Status
	|| resourceResult.Value is null ) {
	await ReportFailureAsync(
		session,
		"Raster resource creation",
		resourceResult.Status,
		resourceResult.Message
	);
	return 1;
}
await using TerminalRasterResource resource = resourceResult.Value;
TerminalRasterAnimation animation = resource.Animation;
TerminalRasterAnimationFrame rootFrame = animation.RootFrame;

if ( !await RequireSuccessAsync(
	session,
	"Root-frame timing",
	await animation.SetFrameDurationAsync(
		rootFrame,
		TimeSpan.FromMilliseconds( 180 )
	)
) ) {
	return 1;
}

TerminalControlResult<TerminalRasterAnimationFrame> secondResult =
	await animation.AddFrameAsync(
		secondImage,
		TimeSpan.FromMilliseconds( 180 )
	);
if ( TerminalControlStatus.Available != secondResult.Status
	|| secondResult.Value is null ) {
	await ReportFailureAsync(
		session,
		"Second-frame append",
		secondResult.Status,
		secondResult.Message
	);
	return 1;
}
TerminalRasterAnimationFrame secondFrame = secondResult.Value;

TerminalControlResult<TerminalRasterAnimationFrame> thirdResult =
	await animation.AddFrameAsync(
		thirdImage,
		TimeSpan.FromMilliseconds( 180 )
	);
if ( TerminalControlStatus.Available != thirdResult.Status
	|| thirdResult.Value is null ) {
	await ReportFailureAsync(
		session,
		"Third-frame append",
		thirdResult.Status,
		thirdResult.Message
	);
	return 1;
}

TerminalControlResult<TerminalRasterPlacement> placementResult =
	await resource.CreatePlacementAsync(
		new TerminalRasterPlacementOptions {
			Columns = 8,
			Rows = 4
		}
	);
if ( TerminalControlStatus.Available != placementResult.Status
	|| placementResult.Value is null ) {
	await ReportFailureAsync(
		session,
		"Raster placement creation",
		placementResult.Status,
		placementResult.Message
	);
	return 1;
}
await using TerminalRasterPlacement placement = placementResult.Value;

if ( !await RequireSuccessAsync(
	session,
	"Loading-mode playback",
	await animation.RunLoadingAsync()
) ) {
	return 1;
}

TerminalControlResult<TerminalRasterAnimationFrame> streamedResult =
	await animation.AddFrameAsync(
		streamedImage,
		TimeSpan.FromMilliseconds( 180 )
	);
if ( TerminalControlStatus.Available != streamedResult.Status
	|| streamedResult.Value is null ) {
	await ReportFailureAsync(
		session,
		"Loading-mode frame append",
		streamedResult.Status,
		streamedResult.Message
	);
	return 1;
}

if ( !await RequireSuccessAsync(
	session,
	"Loading-mode stop",
	await animation.StopAsync()
) ) {
	return 1;
}
if ( !await RequireSuccessAsync(
	session,
	"Explicit frame selection",
	await animation.SelectFrameAsync( secondFrame )
) ) {
	return 1;
}

if ( !await RequireSuccessAsync(
	session,
	"Finite normal playback",
	await animation.RunAsync(
		new TerminalRasterAnimationPlaybackOptions {
			RepeatCount = 1
		}
	)
) ) {
	return 1;
}
await Task.Delay( TimeSpan.FromMilliseconds( 900 ) );
if ( !await RequireSuccessAsync(
	session,
	"Finite playback stop",
	await animation.StopAsync()
) ) {
	return 1;
}

if ( !await RequireSuccessAsync(
	session,
	"Indefinite normal playback",
	await animation.RunAsync(
		new TerminalRasterAnimationPlaybackOptions {
			RepeatCount = null
		}
	)
) ) {
	return 1;
}
await Task.Delay( TimeSpan.FromMilliseconds( 900 ) );
if ( !await RequireSuccessAsync(
	session,
	"Indefinite playback stop",
	await animation.StopAsync()
) ) {
	return 1;
}

await session.WriteTextAsync(
	"Completed loading-mode append, explicit selection, finite playback, indefinite playback, and deterministic resource-owned cleanup.\r\n"
);
return 0;

static TerminalRasterImage CreateFrame(
	params byte[] pixels
) {
	ArgumentNullException.ThrowIfNull( pixels );
	return TerminalRasterImage.CreateRgb24(
		2,
		2,
		pixels
	);
}

static async ValueTask<bool> RequireSuccessAsync(
	TerminalSession session,
	string operation,
	TerminalControlMutationResult result
) {
	ArgumentNullException.ThrowIfNull( session );
	ArgumentException.ThrowIfNullOrWhiteSpace( operation );
	if ( result.Succeeded ) {
		return true;
	}

	await ReportFailureAsync(
		session,
		operation,
		result.Status,
		result.Message
	);
	return false;
}

static ValueTask ReportFailureAsync(
	TerminalSession session,
	string operation,
	TerminalControlStatus status,
	string? message
) {
	ArgumentNullException.ThrowIfNull( session );
	ArgumentException.ThrowIfNullOrWhiteSpace( operation );
	return session.WriteTextAsync(
		string.Concat(
			operation,
			" was not completed: ",
			status.ToString(),
			string.IsNullOrEmpty( message )
				? string.Empty
				: string.Concat( " — ", message ),
			".\r\n"
		)
	);
}
