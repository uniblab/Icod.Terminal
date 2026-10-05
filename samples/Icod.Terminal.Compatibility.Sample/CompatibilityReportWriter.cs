/*
	Icod.Terminal.Compatibility.Sample
	Sample application demonstrating Icod.Terminal Compatibility features.
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
namespace Icod.Terminal.Compatibility.Sample;

using System.Text.Json;
using System.Text.Json.Serialization;

internal interface ICompatibilityReportStorage {
	bool FileExists( string path );

	bool DirectoryExists( string path );

	Stream CreateTemporaryFile( string path );

	ValueTask<string> ReadAllTextAsync(
		string path,
		CancellationToken cancellationToken
	);

	void Move(
		string source,
		string destination,
		bool overwrite
	);

	void Delete( string path );
}

internal sealed class CompatibilityReportFileStorage : ICompatibilityReportStorage {
	public bool FileExists(
		string path
	) => File.Exists( path );

	public bool DirectoryExists(
		string path
	) => Directory.Exists( path );

	public Stream CreateTemporaryFile(
		string path
	) => new FileStream(
		path,
		FileMode.CreateNew,
		FileAccess.Write,
		FileShare.None,
		4096,
		FileOptions.Asynchronous | FileOptions.WriteThrough
	);

	public ValueTask<string> ReadAllTextAsync(
		string path,
		CancellationToken cancellationToken
	) => new( File.ReadAllTextAsync( path, cancellationToken ) );

	public void Move(
		string source,
		string destination,
		bool overwrite
	) => File.Move( source, destination, overwrite );

	public void Delete(
		string path
	) => File.Delete( path );
}

internal static class CompatibilityReportWriter {
	internal static JsonSerializerOptions JsonOptions { get; } = CreateJsonOptions();

	internal static ValueTask WriteAsync(
		string outputPath,
		IReadOnlyList<CompatibilityEvidence> evidence,
		bool overwrite,
		CancellationToken cancellationToken
	) => WriteAsync(
		outputPath,
		evidence,
		overwrite,
		cancellationToken,
		new CompatibilityReportFileStorage()
	);

	internal static async ValueTask WriteAsync(
		string outputPath,
		IReadOnlyList<CompatibilityEvidence> evidence,
		bool overwrite,
		CancellationToken cancellationToken,
		ICompatibilityReportStorage storage
	) {
		ArgumentException.ThrowIfNullOrWhiteSpace( outputPath );
		ArgumentNullException.ThrowIfNull( evidence );
		ArgumentNullException.ThrowIfNull( storage );
		cancellationToken.ThrowIfCancellationRequested();
		if ( 0 == evidence.Count ) {
			throw new FormatException( "A compatibility report must contain at least one evidence item." );
		}
		foreach ( CompatibilityEvidence item in evidence ) {
			CompatibilityEvidenceValidator.Validate( item );
		}

		string destination = Path.GetFullPath( outputPath );
		string? directory = Path.GetDirectoryName( destination );
		if ( string.IsNullOrWhiteSpace( directory ) || !storage.DirectoryExists( directory ) ) {
			throw new DirectoryNotFoundException(
				"The compatibility report output directory does not exist."
			);
		}
		if ( !overwrite && storage.FileExists( destination ) ) {
			throw new IOException( "The compatibility report output file already exists." );
		}

		string temporary = Path.Combine(
			directory,
			$".{Path.GetFileName( destination )}.{Guid.NewGuid():N}.tmp"
		);
		bool moved = false;
		try {
			await using ( Stream stream = storage.CreateTemporaryFile( temporary ) ) {
				await JsonSerializer.SerializeAsync(
					stream,
					evidence,
					JsonOptions,
					cancellationToken
				).ConfigureAwait( false );
				await stream.FlushAsync( cancellationToken ).ConfigureAwait( false );
			}

			string serialized = await storage.ReadAllTextAsync(
				temporary,
				cancellationToken
			).ConfigureAwait( false );
			IReadOnlyList<CompatibilityEvidence>? roundTrip =
				JsonSerializer.Deserialize<IReadOnlyList<CompatibilityEvidence>>(
					serialized,
					JsonOptions
				);
			if ( null == roundTrip || roundTrip.Count != evidence.Count ) {
				throw new FormatException( "The temporary compatibility report did not round trip." );
			}
			foreach ( CompatibilityEvidence item in roundTrip ) {
				CompatibilityEvidenceValidator.Validate( item );
			}

			cancellationToken.ThrowIfCancellationRequested();
			storage.Move( temporary, destination, overwrite );
			moved = true;
		} finally {
			if ( !moved && storage.FileExists( temporary ) ) {
				storage.Delete( temporary );
			}
		}
	}

	private static JsonSerializerOptions CreateJsonOptions() {
		var options = new JsonSerializerOptions {
			PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
			PropertyNameCaseInsensitive = true,
			WriteIndented = true
		};
		options.Converters.Add( new JsonStringEnumConverter() );
		return options;
	}
}
