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

using Icod.Terminal.Compatibility.Sample;
using Xunit;

/// <summary>Freezes query observation-to-outcome mapping.</summary>
public sealed class TerminalCompatibilityQueryScenarioTests {
	[Theory]
	[InlineData( "query.dimensions" )]
	[InlineData( "query.device-attributes" )]
	[InlineData( "query.status-cursor" )]
	[InlineData( "query.decrqss" )]
	[InlineData( "query.xtgettcap" )]
	[InlineData( "query.color" )]
	[InlineData( "query.pointer-shape" )]
	public async Task SuccessfulQueriesPass(
		string scenarioId
	) {
		var client = new RecordingQueryClient( static _ => ValueTask.FromResult( true ) );

		CompatibilityScenarioObservation result = await QueryCompatibilityScenarios.RunAsync(
			scenarioId,
			client,
			CancellationToken.None
		);

		Assert.Equal( CompatibilityOutcome.Pass, result.Outcome );
		Assert.True( result.AutomatedObservation );
		Assert.Equal( scenarioId, Assert.Single( client.ScenarioIds ) );
	}

	[Fact]
	public async Task ExplicitUnsupportedResponseFailsTheScenario() {
		var client = new RecordingQueryClient( static _ => ValueTask.FromResult( false ) );

		CompatibilityScenarioObservation result = await QueryCompatibilityScenarios.RunAsync(
			"query.decrqss",
			client,
			CancellationToken.None
		);

		Assert.Equal( CompatibilityOutcome.Fail, result.Outcome );
		Assert.False( result.AutomatedObservation );
	}

	[Theory]
	[MemberData( nameof( InconclusiveErrors ) )]
	public async Task EnvironmentalFailuresAreInconclusive(
		Exception error
	) {
		var client = new RecordingQueryClient( _ => ValueTask.FromException<bool>( error ) );

		CompatibilityScenarioObservation result = await QueryCompatibilityScenarios.RunAsync(
			"query.dimensions",
			client,
			CancellationToken.None
		);

		Assert.Equal( CompatibilityOutcome.Inconclusive, result.Outcome );
		Assert.Null( result.AutomatedObservation );
	}

	[Fact]
	public async Task MalformedCorrelatedResponseFailsTheScenario() {
		var client = new RecordingQueryClient(
			static _ => ValueTask.FromException<bool>( new FormatException( "malformed" ) )
		);

		CompatibilityScenarioObservation result = await QueryCompatibilityScenarios.RunAsync(
			"query.dimensions",
			client,
			CancellationToken.None
		);

		Assert.Equal( CompatibilityOutcome.Fail, result.Outcome );
		Assert.False( result.AutomatedObservation );
	}

	[Fact]
	public async Task CallerCancellationDoesNotCreateEvidence() {
		using CancellationTokenSource cancellation = new();
		cancellation.Cancel();
		var client = new RecordingQueryClient(
			_ => ValueTask.FromException<bool>( new OperationCanceledException( cancellation.Token ) )
		);

		await Assert.ThrowsAnyAsync<OperationCanceledException>(
			() => QueryCompatibilityScenarios.RunAsync(
				"query.dimensions",
				client,
				cancellation.Token
			).AsTask()
		);
	}

	public static TheoryData<Exception> InconclusiveErrors => new() {
		new TimeoutException( "timeout" ),
		new InvalidOperationException( "endpoint unavailable" ),
		new IOException( "endpoint lost" ),
		new EndOfStreamException( "end of input" )
	};

	private sealed class RecordingQueryClient : ICompatibilityQueryClient {
		private readonly Func<CancellationToken, ValueTask<bool>> observe;

		internal RecordingQueryClient(
			Func<CancellationToken, ValueTask<bool>> observe
		) {
			this.observe = observe;
		}

		internal List<string> ScenarioIds {
			get;
		} = [];

		public ValueTask<bool> ObserveAsync(
			string scenarioId,
			CancellationToken cancellationToken
		) {
			this.ScenarioIds.Add( scenarioId );
			return this.observe( cancellationToken );
		}
	}
}
