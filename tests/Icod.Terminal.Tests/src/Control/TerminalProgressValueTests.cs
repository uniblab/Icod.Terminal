namespace Icod.Terminal.Tests.Control;

using Icod.Terminal;
using Xunit;

/// <summary>
/// Verifies the T102 semantic terminal-progress value and stage conversion contract.
/// </summary>
public sealed class TerminalProgressValueTests {
	[Theory]
	[InlineData( 0L, 10L, 0 )]
	[InlineData( 1L, 10L, 10 )]
	[InlineData( 2L, 10L, 20 )]
	[InlineData( 1L, 3L, 33 )]
	[InlineData( 2L, 3L, 67 )]
	[InlineData( 3L, 3L, 100 )]
	[InlineData( 1L, 8L, 13 )]
	[InlineData( 3L, 8L, 38 )]
	[InlineData( 5L, 8L, 63 )]
	[InlineData( 7L, 8L, 88 )]
	public void ConvertsCompletedAndTotalToRoundedPercentage(
		long completed,
		long total,
		int expectedPercentage
	) {
		TerminalProgressValue value = TerminalProgressValue.CreateDeterminate(
			TerminalProgressState.Normal,
			completed,
			total
		);

		Assert.False( value.IsIndeterminate );
		Assert.Equal( TerminalProgressState.Normal, value.State );
		Assert.Equal( completed, value.Completed );
		Assert.Equal( total, value.Total );
		Assert.Equal( expectedPercentage, value.Percentage );
		Assert.Equal( Osc9ProgressState.Normal, value.GetWireState() );
	}

	[Fact]
	public void ExactHalfRoundsUp() {
		TerminalProgressValue value = TerminalProgressValue.CreateDeterminate(
			TerminalProgressState.Normal,
			1,
			200
		);

		Assert.Equal( 1, value.Percentage );
	}

	[Fact]
	public void SupportsMaximumLongWorkloadWithoutOverflow() {
		TerminalProgressValue complete = TerminalProgressValue.CreateDeterminate(
			TerminalProgressState.Normal,
			long.MaxValue,
			long.MaxValue
		);
		TerminalProgressValue halfway = TerminalProgressValue.CreateDeterminate(
			TerminalProgressState.Normal,
			long.MaxValue / 2,
			long.MaxValue
		);

		Assert.Equal( 100, complete.Percentage );
		Assert.Equal( 50, halfway.Percentage );
	}

	[Theory]
	[InlineData( 0 )]
	[InlineData( -1 )]
	public void RejectsNonPositiveTotal(
		long total
	) {
		Assert.Throws<ArgumentOutOfRangeException>(
			() => TerminalProgressValue.CreateDeterminate(
				TerminalProgressState.Normal,
				0,
				total
			)
		);
	}

	[Fact]
	public void RejectsNegativeCompleted() {
		Assert.Throws<ArgumentOutOfRangeException>(
			() => TerminalProgressValue.CreateDeterminate(
				TerminalProgressState.Normal,
				-1,
				10
			)
		);
	}

	[Fact]
	public void RejectsCompletedBeyondTotal() {
		Assert.Throws<ArgumentOutOfRangeException>(
			() => TerminalProgressValue.CreateDeterminate(
				TerminalProgressState.Normal,
				11,
				10
			)
		);
	}

	[Fact]
	public void RejectsUnknownSemanticState() {
		Assert.Throws<ArgumentOutOfRangeException>(
			() => TerminalProgressValue.CreateDeterminate(
				(TerminalProgressState)3,
				1,
				10
			)
		);
	}

	[Theory]
	[InlineData( TerminalProgressState.Normal, Osc9ProgressState.Normal )]
	[InlineData( TerminalProgressState.Error, Osc9ProgressState.Error )]
	[InlineData( TerminalProgressState.Attention, Osc9ProgressState.Attention )]
	public void MapsSemanticStatesToWireStates(
		TerminalProgressState state,
		Osc9ProgressState expectedWireState
	) {
		TerminalProgressValue value = TerminalProgressValue.CreateDeterminate(
			state,
			1,
			2
		);

		Assert.Equal( expectedWireState, value.GetWireState() );
	}

	[Fact]
	public void CreatesCanonicalIndeterminateValue() {
		TerminalProgressValue value = TerminalProgressValue.CreateIndeterminate();

		Assert.True( value.IsIndeterminate );
		Assert.Null( value.State );
		Assert.Equal( 0, value.Completed );
		Assert.Equal( 0, value.Total );
		Assert.Equal( 0, value.Percentage );
		Assert.Equal( Osc9ProgressState.Indeterminate, value.GetWireState() );
	}
}
