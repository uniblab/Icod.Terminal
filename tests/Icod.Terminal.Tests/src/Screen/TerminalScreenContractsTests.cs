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
namespace Icod.Terminal.Tests.Screen;

using System.Reflection;
using System.Text;
using Icod.Terminal;
using Icod.TermInfo;
using Xunit;

/// <summary>Verifies the first public 1.17 Terminal-owned screen contracts.</summary>
public sealed class TerminalScreenContractsTests {
	[Fact]
	public void DimensionsRequirePositiveColumnsAndRows() {
		TerminalDimensions dimensions = new( 120, 40 );

		Assert.Equal( 120, dimensions.Columns );
		Assert.Equal( 40, dimensions.Rows );
		Assert.Throws<ArgumentOutOfRangeException>(
			() => new TerminalDimensions( 0, 1 )
		);
		Assert.Throws<ArgumentOutOfRangeException>(
			() => new TerminalDimensions( 1, 0 )
		);
	}

	[Fact]
	public async Task SessionProjectsDimensionsAndSemanticProfile() {
		RecordingTerminalOutput output = new();
		await using TerminalSession session = await OpenSessionAsync( output );

		TerminalControlResult<TerminalDimensions> dimensions =
			session.GetDimensions();

		Assert.True( dimensions.IsAvailable );
		Assert.Equal( new TerminalDimensions( 100, 30 ), dimensions.GetRequiredValue() );
		Assert.Equal( "screen-contract", session.Profile.Name );
		Assert.True( session.Profile.Screen.SupportsAbsoluteCursorAddressing );
		Assert.True( session.Profile.Screen.SupportsBold );
		Assert.True( session.Profile.Screen.SupportsAlternateCharacterSet );
		Assert.DoesNotContain(
			GetPublicContractTypes( typeof( TerminalProfile ) ),
			static type => string.Equals(
				type.Namespace,
				"Icod.TermInfo",
				StringComparison.Ordinal
			)
		);
	}

	[Fact]
	public async Task PlannerSelectsShortestSafeCursorPlanWithoutOutput() {
		RecordingTerminalOutput output = new();
		await using TerminalSession session = await OpenSessionAsync( output );

		TerminalScreenOperationPlan? plan = session.Screen.PlanCursorMove(
			new TerminalScreenPosition( 0, 5 ),
			new TerminalScreenPosition( 0, 0 )
		);

		Assert.NotNull( plan );
		Assert.Equal( TerminalScreenOperationKind.CursorMove, plan.Value.Kind );
		Assert.Equal( Encoding.Latin1.GetByteCount( "<cr>" ), plan.Value.ByteCount );
		Assert.Empty( output.Bytes );
	}

	[Fact]
	public async Task TransactionCommitsPlanAndTextUnderOneFlushBoundary() {
		RecordingTerminalOutput output = new();
		await using TerminalSession session = await OpenSessionAsync( output );
		TerminalScreenOperationPlan plan = session.Screen.PlanCursorMove(
			null,
			new TerminalScreenPosition( 2, 3 )
		) ?? throw new InvalidOperationException();
		TerminalScreenOutputTransaction transaction =
			session.CreateScreenOutputTransaction();
		transaction.Add( plan );
		transaction.WriteText( "界" );

		await transaction.CommitAsync();

		Assert.Equal(
			Encoding.UTF8.GetBytes( "<cup:2,3>界" ),
			output.Bytes.ToArray()
		);
		Assert.Equal( 1, output.FlushCount );
	}

	[Fact]
	public async Task InterveningSessionOutputRejectsTransactionBeforeCommitment() {
		RecordingTerminalOutput output = new();
		await using TerminalSession session = await OpenSessionAsync( output );
		TerminalScreenOutputTransaction transaction =
			session.CreateScreenOutputTransaction();
		transaction.WriteText( "stale" );

		await session.WriteTextAsync( "prior" );
		await Assert.ThrowsAsync<InvalidOperationException>(
			() => transaction.CommitAsync().AsTask()
		);

		Assert.Equal( Encoding.UTF8.GetBytes( "prior" ), output.Bytes.ToArray() );
	}

	[Fact]
	public async Task TransactionRejectsPlanFromAnotherSession() {
		await using TerminalSession first = await OpenSessionAsync(
			new RecordingTerminalOutput()
		);
		await using TerminalSession second = await OpenSessionAsync(
			new RecordingTerminalOutput()
		);
		TerminalScreenOperationPlan plan = first.Screen.PlanAlert(
			TerminalAlertKind.Audible
		) ?? throw new InvalidOperationException();
		TerminalScreenOutputTransaction transaction =
			second.CreateScreenOutputTransaction();

		Assert.Throws<ArgumentException>(
			() => transaction.Add( plan )
		);
	}

	private static IReadOnlyCollection<Type> GetPublicContractTypes(
		Type root
	) {
		ArgumentNullException.ThrowIfNull( root );
		HashSet<Type> result = [];
		foreach ( ConstructorInfo constructor in root.GetConstructors() ) {
			foreach ( ParameterInfo parameter in constructor.GetParameters() ) {
				result.Add( parameter.ParameterType );
			}
		}
		foreach ( PropertyInfo property in root.GetProperties() ) {
			result.Add( property.PropertyType );
		}
		foreach ( MethodInfo method in root.GetMethods() ) {
			result.Add( method.ReturnType );
			foreach ( ParameterInfo parameter in method.GetParameters() ) {
				result.Add( parameter.ParameterType );
			}
		}
		return result;
	}

	private static ValueTask<TerminalSession> OpenSessionAsync(
		RecordingTerminalOutput output
	) {
		ArgumentNullException.ThrowIfNull( output );
		TerminalDescription terminal = new TerminalDescriptionBuilder( "screen-contract" )
			.SetString( StringCapability.CursorAddress, "<cup:%p1%d,%p2%d>" )
			.SetString( StringCapability.CarriageReturn, "<cr>" )
			.SetString( StringCapability.Bell, "<bell>" )
			.SetString( StringCapability.EnterBoldMode, "<bold>" )
			.SetString( StringCapability.ExitAttributeMode, "<sgr0>" )
			.SetString( StringCapability.AlternateCharacterSet, "q-x|" )
			.SetString( StringCapability.EnterAlternateCharacterSetMode, "<acs>" )
			.SetString( StringCapability.ExitAlternateCharacterSetMode, "</acs>" )
			.Build();

		return TerminalSession.OpenAsync(
			new TestTerminalControlProvider(),
			TerminalEndpoint.StandardInput,
			TerminalEndpoint.StandardOutput,
			new TestTerminalInput(),
			output,
			new TerminalSessionOptions {
				TerminalOverride = terminal,
				ObserveLifecycleEvents = false
			}
		);
	}

	private sealed class TestTerminalInput : ITerminalInput {
		public ValueTask<int> ReadAsync(
			Memory<byte> buffer,
			CancellationToken cancellationToken = default
		) {
			cancellationToken.ThrowIfCancellationRequested();
			return ValueTask.FromResult( 0 );
		}
	}

	private sealed class RecordingTerminalOutput : ITerminalOutput {
		internal List<byte> Bytes {
			get;
		} = [];

		internal int FlushCount {
			get;
			private set;
		}

		public ValueTask WriteAsync(
			ReadOnlyMemory<byte> buffer,
			CancellationToken cancellationToken = default
		) {
			cancellationToken.ThrowIfCancellationRequested();
			this.Bytes.AddRange( buffer.ToArray() );
			return ValueTask.CompletedTask;
		}

		public ValueTask FlushAsync(
			CancellationToken cancellationToken = default
		) {
			cancellationToken.ThrowIfCancellationRequested();
			++this.FlushCount;
			return ValueTask.CompletedTask;
		}
	}

	private sealed class TestTerminalControlProvider : ITerminalControlProvider {
		private readonly TerminalModeSnapshot baseline = TerminalModeSnapshot.CreatePosix(
			0,
			0,
			0,
			0x0002UL,
			new byte[ 32 ],
			0,
			32,
			0,
			new TerminalSpeed( 13, 9600 ),
			new TerminalSpeed( 13, 9600 )
		);

		public TerminalControlResult<TerminalEndpointObservation> Observe(
			TerminalEndpoint endpoint
		) {
			ArgumentNullException.ThrowIfNull( endpoint );
			return TerminalControlResult<TerminalEndpointObservation>.Available(
				new TerminalEndpointObservation(
					true,
					null,
					TerminalPlatformKind.PosixTermios,
					TerminalControlCapabilities.Attachment
						| TerminalControlCapabilities.ModeRead
						| TerminalControlCapabilities.ModeWrite
						| TerminalControlCapabilities.LiveSize
				)
			);
		}

		public TerminalControlResult<TerminalSize> GetSize(
			TerminalEndpoint endpoint
		) {
			ArgumentNullException.ThrowIfNull( endpoint );
			return TerminalControlResult<TerminalSize>.Available(
				new TerminalSize( 100, 30 )
			);
		}

		public TerminalControlResult<TerminalModeSnapshot> GetMode(
			TerminalEndpoint endpoint
		) {
			ArgumentNullException.ThrowIfNull( endpoint );
			return TerminalControlResult<TerminalModeSnapshot>.Available(
				this.baseline
			);
		}

		public TerminalControlMutationResult SetMode(
			TerminalEndpoint endpoint,
			TerminalModeSnapshot mode,
			TerminalModeApplyTiming timing
		) {
			ArgumentNullException.ThrowIfNull( endpoint );
			ArgumentNullException.ThrowIfNull( mode );
			if ( !Enum.IsDefined( timing ) ) {
				throw new ArgumentOutOfRangeException( nameof( timing ) );
			}
			return TerminalControlMutationResult.Success();
		}
	}
}
