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

	[Theory]
	[InlineData( TerminalControlStatus.Unavailable, 25 )]
	[InlineData( TerminalControlStatus.Failed, 5 )]
	[InlineData( TerminalControlStatus.Unsupported, null )]
	public async Task DimensionsPreserveProviderDiagnostics(
		TerminalControlStatus status,
		int? nativeErrorCode
	) {
		TerminalControlResult<TerminalSize> size = status switch {
			TerminalControlStatus.Unavailable => TerminalControlResult<TerminalSize>.Unavailable( "size unavailable", nativeErrorCode ),
			TerminalControlStatus.Failed => TerminalControlResult<TerminalSize>.Failed( "size unavailable", nativeErrorCode ),
			_ => TerminalControlResult<TerminalSize>.Unsupported( "size unavailable" )
		};
		await using TerminalSession session = await OpenSessionAsync(
			new RecordingTerminalOutput(),
			new TestTerminalControlProvider { Size = size }
		);

		TerminalControlResult<TerminalDimensions> dimensions = session.GetDimensions();

		Assert.Equal( status, dimensions.Status );
		Assert.Equal( "size unavailable", dimensions.Message );
		Assert.Equal( nativeErrorCode, dimensions.NativeErrorCode );
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

	[Fact]
	public async Task SynchronizedTransactionFramesBodyAndFlushesOnce() {
		RecordingTerminalOutput output = new();
		await using TerminalSession session = await OpenSessionAsync( output );
		TerminalScreenOutputTransaction transaction =
			session.CreateScreenOutputTransaction(
				new TerminalScreenOutputTransactionOptions {
					UseSynchronizedOutput = true
				}
			);
		transaction.WriteText( "body" );

		await transaction.CommitAsync();

		Assert.Equal(
			[
				.. CsiWriter.EncodeSynchronizedOutputBeginFrame(),
				.. Encoding.UTF8.GetBytes( "body" ),
				.. CsiWriter.EncodeSynchronizedOutputEndFrame()
			],
			output.Bytes
		);
		Assert.Equal( 1, output.FlushCount );
	}

	[Fact]
	public async Task TransactionComposesStrictHyperlinkTextInCallerOrder() {
		RecordingTerminalOutput output = new();
		await using TerminalSession session = await OpenSessionAsync( output );
		TerminalScreenOutputTransaction transaction =
			session.CreateScreenOutputTransaction();
		transaction.WriteText( "before" );
		transaction.WriteHyperlink(
			"link",
			"https://example.com/target",
			"screen-1"
		);
		transaction.WriteText( "after" );

		await transaction.CommitAsync();

		Assert.Equal(
			[
				.. Encoding.UTF8.GetBytes( "before" ),
				.. OscWriter.EncodeHyperlinkBeginFrame(
					"https://example.com/target",
					"screen-1"
				),
				.. Encoding.UTF8.GetBytes( "link" ),
				.. OscWriter.EncodeHyperlinkEndFrame(),
				.. Encoding.UTF8.GetBytes( "after" )
			],
			output.Bytes
		);
	}

	[Fact]
	public async Task HyperlinkPrimaryFailureAttemptsNonCancellableClose() {
		RecordingTerminalOutput output = new() {
			FailOnWriteNumber = 2
		};
		await using TerminalSession session = await OpenSessionAsync( output );
		TerminalScreenOutputTransaction transaction =
			session.CreateScreenOutputTransaction();
		transaction.WriteHyperlink( "link", "https://example.com/" );

		await Assert.ThrowsAsync<InvalidOperationException>(
			() => transaction.CommitAsync().AsTask()
		);

		Assert.Equal( 3, output.WriteAttempts.Count );
		Assert.Equal(
			OscWriter.EncodeHyperlinkEndFrame(),
			output.WriteAttempts[ 2 ]
		);
		Assert.False( output.WriteCancellationCanBeCanceled[ 2 ] );
	}

	[Theory]
	[InlineData( true, false )]
	[InlineData( false, false )]
	[InlineData( true, true )]
	[InlineData( false, true )]
	public async Task TransactionRejectsExistingFrameOwnerBeforeAnyOutput(
		bool hyperlinkOwner,
		bool failedCleanup
	) {
		RecordingTerminalOutput output = new() { FailOnWriteNumber = failedCleanup ? 2 : null };
		await using TerminalSession session = await OpenSessionAsync( output );
		await using IAsyncDisposable owner = hyperlinkOwner
			? await session.AcquireHyperlinkAsync( "https://example.com/outer" )
			: await session.AcquireSynchronizedOutputAsync();
		if ( failedCleanup ) {
			await Assert.ThrowsAsync<InvalidOperationException>( () => owner.DisposeAsync().AsTask() );
		}
		int attempts = output.WriteAttempts.Count;
		int flushes = output.FlushCount;
		byte[] before = output.Bytes.ToArray();
		// Capture the epoch after the outer lease (and any failed cleanup).
		TerminalScreenOutputTransaction transaction = session.CreateScreenOutputTransaction(
			new TerminalScreenOutputTransactionOptions { UseSynchronizedOutput = true }
		);
		transaction.WriteText( "must-not-write-prefix" );
		transaction.WriteHyperlink( "inner", "https://example.com/inner" );

		await Assert.ThrowsAsync<InvalidOperationException>( () => transaction.CommitAsync().AsTask() );

		Assert.Equal( attempts, output.WriteAttempts.Count );
		Assert.Equal( flushes, output.FlushCount );
		Assert.Equal( before, output.Bytes );
		await owner.DisposeAsync().AsTask().WaitAsync( TimeSpan.FromSeconds( 5 ) );
		Assert.Equal( attempts + 1, output.WriteAttempts.Count );
		Assert.Equal(
			hyperlinkOwner ? "\u001b]8;;\u001b\\"u8.ToArray() : "\u001b[?2026l"u8.ToArray(),
			output.WriteAttempts[ ^1 ]
		);
		TerminalScreenOutputTransaction next = session.CreateScreenOutputTransaction(
			new TerminalScreenOutputTransactionOptions { UseSynchronizedOutput = true }
		);
		next.WriteHyperlink( "next", "https://example.com/next" );
		await next.CommitAsync().AsTask().WaitAsync( TimeSpan.FromSeconds( 5 ) );
	}

	[Fact]
	public async Task ScreenReservationPrecedesOutputGateAndReleasesAfterFailure() {
		RecordingTerminalOutput output = new() { FailOnWriteNumber = 3 };
		await using TerminalSession session = await OpenSessionAsync( output );
		using IDisposable blockedOutput = await session.AcquireSessionOutputAsync( CancellationToken.None );
		TerminalScreenOutputTransaction transaction = session.CreateScreenOutputTransaction(
			new TerminalScreenOutputTransactionOptions { UseSynchronizedOutput = true }
		);
		transaction.WriteHyperlink( "body", "https://example.com/transaction" );
		Task commit = transaction.CommitAsync().AsTask();
		Task<TerminalHyperlinkLease> hyperlink = session.AcquireHyperlinkAsync( "https://example.com/outer" ).AsTask();
		Task<TerminalSynchronizedOutputLease> synchronized = session.AcquireSynchronizedOutputAsync().AsTask();
		Assert.False( commit.IsCompleted );
		Assert.False( hyperlink.IsCompleted );
		Assert.False( synchronized.IsCompleted );
		blockedOutput.Dispose();

		await Assert.ThrowsAsync<InvalidOperationException>( () => commit.WaitAsync( TimeSpan.FromSeconds( 5 ) ) );
		await using TerminalHyperlinkLease hyperlinkOwner = await hyperlink.WaitAsync( TimeSpan.FromSeconds( 5 ) );
		await using TerminalSynchronizedOutputLease synchronizedOwner = await synchronized.WaitAsync( TimeSpan.FromSeconds( 5 ) );
		Assert.Equal( "\u001b]8;;\u001b\\"u8.ToArray(), output.WriteAttempts[ 3 ] );
		Assert.Equal( "\u001b[?2026l"u8.ToArray(), output.WriteAttempts[ 4 ] );
		Assert.All( output.WriteCancellationCanBeCanceled, Assert.False );
	}

	[Fact]
	public async Task TransactionRejectsPendingSynchronizedCleanupWithoutAnOwner() {
		RecordingTerminalOutput output = new() { FailedWriteNumbers = [ 1, 2 ] };
		await using TerminalSession session = await OpenSessionAsync( output );
		await Assert.ThrowsAsync<AggregateException>( () => session.AcquireSynchronizedOutputAsync().AsTask() );
		TerminalScreenOutputTransaction transaction = session.CreateScreenOutputTransaction(
			new TerminalScreenOutputTransactionOptions { UseSynchronizedOutput = true }
		);
		transaction.WriteText( "must-not-write" );

		await Assert.ThrowsAsync<InvalidOperationException>( () => transaction.CommitAsync().AsTask() );

		Assert.Equal( 2, output.WriteAttempts.Count );
		Assert.Empty( output.Bytes );
		Assert.Equal( 0, output.FlushCount );
		await session.DisposeAsync();
		Assert.Equal( "\u001b[?2026l"u8.ToArray(), output.Bytes );
	}

	[Fact]
	public async Task UnframedTransactionPreservesOuterOwners() {
		RecordingTerminalOutput output = new();
		await using TerminalSession session = await OpenSessionAsync( output );
		await using TerminalHyperlinkLease hyperlink = await session.AcquireHyperlinkAsync( "https://example.com/outer" );
		await using TerminalSynchronizedOutputLease synchronized = await session.AcquireSynchronizedOutputAsync();
		TerminalScreenOutputTransaction transaction = session.CreateScreenOutputTransaction();
		transaction.WriteText( "body" );

		await transaction.CommitAsync();

		Assert.Equal( "\u001b]8;;https://example.com/outer\u001b\\\u001b[?2026hbody"u8.ToArray(), output.Bytes );
		await synchronized.DisposeAsync();
		await hyperlink.DisposeAsync();
		Assert.Equal( "\u001b[?2026l"u8.ToArray(), output.WriteAttempts[ ^2 ] );
		Assert.Equal( "\u001b]8;;\u001b\\"u8.ToArray(), output.WriteAttempts[ ^1 ] );
	}

	[Fact]
	public async Task CancelledScreenGateWaitReleasesManagerReservationsWithoutOutput() {
		RecordingTerminalOutput output = new();
		await using TerminalSession session = await OpenSessionAsync( output );
		using IDisposable blockedOutput = await session.AcquireSessionOutputAsync( CancellationToken.None );
		using CancellationTokenSource cancellation = new();
		TerminalScreenOutputTransaction transaction = session.CreateScreenOutputTransaction(
			new TerminalScreenOutputTransactionOptions { UseSynchronizedOutput = true }
		);
		transaction.WriteHyperlink( "cancelled", "https://example.com/cancelled" );
		Task commit = transaction.CommitAsync( cancellation.Token ).AsTask();
		cancellation.Cancel();
		await Assert.ThrowsAnyAsync<OperationCanceledException>( () => commit );
		Assert.Empty( output.WriteAttempts );
		Assert.Equal( 0, output.FlushCount );
		blockedOutput.Dispose();

		await using TerminalHyperlinkLease hyperlink = await session.AcquireHyperlinkAsync( "https://example.com/next" ).AsTask().WaitAsync( TimeSpan.FromSeconds( 5 ) );
		await using TerminalSynchronizedOutputLease synchronized = await session.AcquireSynchronizedOutputAsync().AsTask().WaitAsync( TimeSpan.FromSeconds( 5 ) );
	}

	[Fact]
	public async Task PreCommitCancellationAndSecondCommitEmitNoAdditionalBytes() {
		RecordingTerminalOutput output = new();
		await using TerminalSession session = await OpenSessionAsync( output );
		TerminalScreenOutputTransaction cancelled =
			session.CreateScreenOutputTransaction();
		cancelled.WriteText( "cancelled" );
		using CancellationTokenSource cancellation = new();
		cancellation.Cancel();

		await Assert.ThrowsAnyAsync<OperationCanceledException>(
			() => cancelled.CommitAsync( cancellation.Token ).AsTask()
		);
		Assert.Empty( output.Bytes );

		TerminalScreenOutputTransaction committed =
			session.CreateScreenOutputTransaction();
		committed.WriteText( "once" );
		await committed.CommitAsync();
		await Assert.ThrowsAsync<InvalidOperationException>(
			() => committed.CommitAsync().AsTask()
		);
		Assert.Equal( Encoding.UTF8.GetBytes( "once" ), output.Bytes );
	}

	[Fact]
	public void NewSemanticScreenContractsExposeNoTermInfoTypes() {
		Type[] contractRoots = typeof( TerminalSession ).Assembly
			.GetExportedTypes()
			.Where(
				static type => type.Name.StartsWith(
					"TerminalScreen",
					StringComparison.Ordinal
				) || type == typeof( TerminalDimensions )
					|| type == typeof( TerminalProfile )
					|| type == typeof( TerminalLineGlyph )
					|| type == typeof( TerminalLineGlyphRepresentation )
					|| type == typeof( TerminalAlertKind )
					|| type == typeof( TerminalTextAttributes )
			)
			.ToArray();

		foreach ( Type root in contractRoots ) {
			Assert.DoesNotContain(
				GetPublicContractTypes( root ),
				static type => string.Equals(
					type.Namespace,
					"Icod.TermInfo",
					StringComparison.Ordinal
				)
			);
		}
	}

	private static IReadOnlyCollection<Type> GetPublicContractTypes(
		Type root
	) {
		ArgumentNullException.ThrowIfNull( root );
		HashSet<Type> result = [ root ];
		foreach ( ConstructorInfo constructor in root.GetConstructors() ) {
			foreach ( ParameterInfo parameter in constructor.GetParameters() ) {
				AddTypeClosure( result, parameter.ParameterType );
			}
		}
		foreach ( PropertyInfo property in root.GetProperties() ) {
			AddTypeClosure( result, property.PropertyType );
		}
		foreach ( MethodInfo method in root.GetMethods() ) {
			AddTypeClosure( result, method.ReturnType );
			foreach ( ParameterInfo parameter in method.GetParameters() ) {
				AddTypeClosure( result, parameter.ParameterType );
			}
		}
		return result;
	}

	private static void AddTypeClosure(
		HashSet<Type> result,
		Type type
	) {
		if ( !result.Add( type ) ) {
			return;
		}
		if ( type.HasElementType && type.GetElementType() is Type element ) {
			AddTypeClosure( result, element );
		}
		foreach ( Type argument in type.GetGenericArguments() ) {
			AddTypeClosure( result, argument );
		}
	}

	private static ValueTask<TerminalSession> OpenSessionAsync(
		RecordingTerminalOutput output,
		TestTerminalControlProvider? provider = null
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
			provider ?? new TestTerminalControlProvider(),
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

		internal int? FailOnWriteNumber {
			get;
			init;
		}

		internal HashSet<int> FailedWriteNumbers { get; init; } = [];

		internal List<byte[]> WriteAttempts {
			get;
		} = [];

		internal List<bool> WriteCancellationCanBeCanceled {
			get;
		} = [];

		public ValueTask WriteAsync(
			ReadOnlyMemory<byte> buffer,
			CancellationToken cancellationToken = default
		) {
			cancellationToken.ThrowIfCancellationRequested();
			this.WriteAttempts.Add( buffer.ToArray() );
			this.WriteCancellationCanBeCanceled.Add( cancellationToken.CanBeCanceled );
			if ( this.FailOnWriteNumber == this.WriteAttempts.Count
				|| this.FailedWriteNumbers.Contains( this.WriteAttempts.Count ) ) {
				throw new InvalidOperationException( "Injected output failure." );
			}
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
		internal TerminalControlResult<TerminalSize> Size { get; init; } =
			TerminalControlResult<TerminalSize>.Available( new TerminalSize( 100, 30 ) );

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
			return this.Size;
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
