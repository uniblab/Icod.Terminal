/*
	Icod.Terminal.Tests
	Automated test suite for the Icod.Terminal library.
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
namespace Icod.Terminal.Tests.Samples;

using System.Text.Json;
using System.Text.Json.Serialization;
using Icod.Terminal.Compatibility.Sample;
using Xunit;

/// <summary>Freezes the bounded compatibility-evidence contract.</summary>
public sealed class TerminalCompatibilityEvidenceTests {
	[Fact]
	public void CompleteFixtureIsValid() {
		CompatibilityEvidence evidence = ReadFixture();

		CompatibilityEvidenceValidator.Validate( evidence );
	}

	[Fact]
	public void RequiredIdentityCannotBeMissing() {
		CompatibilityEvidence evidence = Valid() with { TerminalVersion = "" };

		Assert.Throws<FormatException>(
			() => CompatibilityEvidenceValidator.Validate( evidence )
		);
	}

	[Fact]
	public void ObservationTimeMustBeUtc() {
		CompatibilityEvidence evidence = Valid() with {
			ObservedAtUtc = new DateTimeOffset( 2026, 10, 4, 12, 0, 0, TimeSpan.FromHours( -4 ) )
		};

		Assert.Throws<FormatException>(
			() => CompatibilityEvidenceValidator.Validate( evidence )
		);
	}

	[Fact]
	public void OutcomeMustBeKnown() {
		CompatibilityEvidence evidence = Valid() with {
			Outcome = (CompatibilityOutcome)999
		};

		Assert.Throws<FormatException>(
			() => CompatibilityEvidenceValidator.Validate( evidence )
		);
	}

	[Fact]
	public void ScenarioRevisionMustBeKnown() {
		CompatibilityEvidence evidence = Valid() with { ScenarioRevision = 2 };

		Assert.Throws<FormatException>(
			() => CompatibilityEvidenceValidator.Validate( evidence )
		);
	}

	[Theory]
	[InlineData( "note\nwith newline" )]
	[InlineData( "note\u007fwith delete" )]
	[InlineData( "note\u0085with c1" )]
	public void ControlCharactersAreRejected(
		string note
	) {
		CompatibilityEvidence evidence = Valid() with { Note = note };

		Assert.Throws<FormatException>(
			() => CompatibilityEvidenceValidator.Validate( evidence )
		);
	}

	[Fact]
	public void OversizedNoteIsRejected() {
		CompatibilityEvidence evidence = Valid() with { Note = new string( 'x', 513 ) };

		Assert.Throws<FormatException>(
			() => CompatibilityEvidenceValidator.Validate( evidence )
		);
	}

	[Fact]
	public void TransportNameAndVersionArePaired() {
		CompatibilityEvidence evidence = Valid() with {
			Transport = "tmux",
			TransportVersion = null
		};

		Assert.Throws<FormatException>(
			() => CompatibilityEvidenceValidator.Validate( evidence )
		);
	}

	[Theory]
	[InlineData( "query.appearance", "Pass" )]
	[InlineData( "environment.appearance-reporting", "Pass" )]
	[InlineData( "environment.in-band-resize", "Unavailable" )]
	public void EnvironmentEvidenceAcceptsRevisionOne( string scenarioId, string outcome ) {
		CompatibilityEvidence evidence = Valid() with {
			TerminalPackageVersion = "1.27.0-alpha.1",
			ScenarioId = scenarioId,
			Outcome = Enum.Parse<CompatibilityOutcome>( outcome ),
			Note = "Bounded typed environment observation."
		};

		CompatibilityEvidenceValidator.Validate( evidence );
	}

	private static CompatibilityEvidence ReadFixture() {
		string root = FindRepositoryRoot();
		string json = File.ReadAllText(
			Path.Combine(
				root,
				"samples",
				"Icod.Terminal.Compatibility.Sample",
				"fixtures",
				"valid-evidence.json"
			)
		);
		var options = new JsonSerializerOptions {
			PropertyNameCaseInsensitive = true
		};
		options.Converters.Add( new JsonStringEnumConverter() );
		return JsonSerializer.Deserialize<CompatibilityEvidence>( json, options )!;
	}

	internal static CompatibilityEvidence Valid() => new(
		SchemaVersion: "1",
		TerminalPackageVersion: "1.26.0-alpha",
		SourceCommit: "0123456789abcdef0123456789abcdef01234567",
		TerminalId: "kitty",
		TerminalVersion: "0.32.2",
		OperatingSystem: "Ubuntu",
		OperatingSystemVersion: "24.04",
		Transport: "WSL",
		TransportVersion: "2",
		ScenarioId: "identity.session",
		ScenarioRevision: 1,
		ObservedAtUtc: new DateTimeOffset( 2026, 10, 4, 16, 0, 0, TimeSpan.Zero ),
		Outcome: CompatibilityOutcome.Pass,
		AutomatedObservation: true,
		OperatorObservation: null,
		Note: "Session opened and reported bounded identity data."
	);

	internal static string FindRepositoryRoot() {
		var directory = new DirectoryInfo( AppContext.BaseDirectory );
		while ( null != directory ) {
			if ( File.Exists( Path.Combine( directory.FullName, "Icod.Terminal.csproj" ) ) ) {
				return directory.FullName;
			}
			directory = directory.Parent;
		}
		throw new DirectoryNotFoundException( "The repository root was not found." );
	}
}
