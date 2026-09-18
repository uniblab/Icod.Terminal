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
namespace Icod.Terminal.Tests.Lifecycle;

using System.Reflection;
using System.Threading.Channels;
using Icod.Terminal;
using Icod.TermInfo;
using Xunit;

/// <summary>Verifies the Terminal-owned dimension projection and its legacy compatibility.</summary>
public sealed class TerminalDimensionsTests {
	[Fact]
	public void ConstructorStoresPositiveDimensions() {
		TerminalDimensions dimensions = new( 132, 43 );

		Assert.Equal( 132, dimensions.Columns );
		Assert.Equal( 43, dimensions.Rows );
	}

	[Theory]
	[InlineData( 0, 24, "columns" )]
	[InlineData( -1, 24, "columns" )]
	[InlineData( 80, 0, "rows" )]
	[InlineData( 80, -1, "rows" )]
	public void ConstructorRejectsNonPositiveDimensions(
		int columns,
		int rows,
		string parameterName
	) {
		ArgumentOutOfRangeException exception = Assert.Throws<ArgumentOutOfRangeException>(
			() => new TerminalDimensions( columns, rows )
		);

		Assert.Equal( parameterName, exception.ParamName );
	}

	[Fact]
	public async Task GetDimensionsRejectsDefaultLegacySizeWhenProjected() {
		TerminalControlResult<TerminalSize> source =
			TerminalControlResult<TerminalSize>.Available( default );
		await using TerminalSession session = await OpenSessionAsync(
			new RecordingTerminalControlProvider { SizeResult = source }
		);

		Assert.Equal( default, session.GetSize().GetRequiredValue() );
		ArgumentOutOfRangeException exception = Assert.Throws<ArgumentOutOfRangeException>(
			() => session.GetDimensions()
		);

		Assert.Equal( "columns", exception.ParamName );
	}

	[Theory]
	[InlineData( TerminalControlStatus.Available, null, null )]
	[InlineData( TerminalControlStatus.Unavailable, "size unavailable", 25 )]
	[InlineData( TerminalControlStatus.Unsupported, "size unsupported", null )]
	[InlineData( TerminalControlStatus.Failed, "size failed", 5 )]
	public async Task GetDimensionsProjectsEveryResultStatusAndDiagnostic(
		TerminalControlStatus status,
		string? message,
		int? nativeErrorCode
	) {
		TerminalControlResult<TerminalSize> source = CreateSizeResult(
			status,
			message,
			nativeErrorCode
		);
		await using TerminalSession session = await OpenSessionAsync(
			new RecordingTerminalControlProvider { SizeResult = source }
		);

		TerminalControlResult<TerminalSize> legacy = session.GetSize();
		TerminalControlResult<TerminalDimensions> projected = session.GetDimensions();

		Assert.Equal( source.Status, legacy.Status );
		Assert.Equal( source.Message, legacy.Message );
		Assert.Equal( source.NativeErrorCode, legacy.NativeErrorCode );
		Assert.Equal( source.Status, projected.Status );
		Assert.Equal( source.Message, projected.Message );
		Assert.Equal( source.NativeErrorCode, projected.NativeErrorCode );
		if ( TerminalControlStatus.Available == status ) {
			Assert.Equal( new TerminalDimensions( 132, 43 ), projected.GetRequiredValue() );
		} else {
			Assert.False( projected.IsAvailable );
			Assert.Throws<InvalidOperationException>(
				() => {
					_ = projected.GetRequiredValue();
				}
			);
		}
	}

	[Fact]
	public async Task ResizeAndResumeProjectDimensionsFromTheSameLegacyObservation() {
		RecordingTerminalControlProvider provider = new() {
			SizeResult = TerminalControlResult<TerminalSize>.Available(
				new TerminalSize( 100, 40 )
			)
		};
		TestTerminalLifecycleSource lifecycle = new();
		await using TerminalSession session = await OpenSessionAsync( provider, lifecycle );
		using CancellationTokenSource timeout = new( TimeSpan.FromSeconds( 5 ) );
		Assert.Equal( 0, provider.SizeCallCount );

		lifecycle.Publish( TerminalLifecycleSignalKind.Resize );
		TerminalLifecycleEvent resize = await session.ReadLifecycleEventAsync( timeout.Token );
		AssertEventDimensions( resize, TerminalLifecycleEventKind.Resize, 100, 40 );
		Assert.Equal( 1, provider.SizeCallCount );

		provider.SizeResult = TerminalControlResult<TerminalSize>.Available(
			new TerminalSize( 120, 50 )
		);
		lifecycle.Publish( TerminalLifecycleSignalKind.Resume );
		TerminalLifecycleEvent resumed = await session.ReadLifecycleEventAsync( timeout.Token );
		AssertEventDimensions( resumed, TerminalLifecycleEventKind.Resumed, 120, 50 );
		Assert.Equal( 2, provider.SizeCallCount );

		lifecycle.Publish( TerminalLifecycleSignalKind.Interrupt );
		TerminalLifecycleEvent interrupt = await session.ReadLifecycleEventAsync( timeout.Token );
		Assert.Equal( TerminalLifecycleEventKind.Interrupt, interrupt.Kind );
		Assert.Null( interrupt.Size );
		Assert.Null( interrupt.Dimensions );
	}

	[Fact]
	public async Task ResizeAndResumeLeaveBothDimensionViewsNullWhenSizeIsUnavailable() {
		RecordingTerminalControlProvider provider = new() {
			SizeResult = TerminalControlResult<TerminalSize>.Unavailable(
				"size unavailable",
				25
			)
		};
		TestTerminalLifecycleSource lifecycle = new();
		await using TerminalSession session = await OpenSessionAsync( provider, lifecycle );
		using CancellationTokenSource timeout = new( TimeSpan.FromSeconds( 5 ) );

		lifecycle.Publish( TerminalLifecycleSignalKind.Resize );
		TerminalLifecycleEvent resize = await session.ReadLifecycleEventAsync( timeout.Token );
		Assert.Equal( TerminalLifecycleEventKind.Resize, resize.Kind );
		Assert.Null( resize.Size );
		Assert.Null( resize.Dimensions );

		lifecycle.Publish( TerminalLifecycleSignalKind.Resume );
		TerminalLifecycleEvent resumed = await session.ReadLifecycleEventAsync( timeout.Token );
		Assert.Equal( TerminalLifecycleEventKind.Resumed, resumed.Kind );
		Assert.Null( resumed.Size );
		Assert.Null( resumed.Dimensions );
	}

	[Fact]
	public void NewDimensionSignaturesAreTerminalOwnedAndLegacySignaturesRemain() {
		MethodInfo getDimensions = Assert.Single(
			typeof( TerminalSession ).GetMethods(),
			static method => method.Name == nameof( TerminalSession.GetDimensions )
		);
		PropertyInfo dimensions = typeof( TerminalLifecycleEvent ).GetProperty(
			nameof( TerminalLifecycleEvent.Dimensions )
		) ?? throw new InvalidOperationException();
		MethodInfo getSize = Assert.Single(
			typeof( TerminalSession ).GetMethods(),
			static method => method.Name == nameof( TerminalSession.GetSize )
		);
		PropertyInfo size = typeof( TerminalLifecycleEvent ).GetProperty(
			nameof( TerminalLifecycleEvent.Size )
		) ?? throw new InvalidOperationException();

		Assert.Equal(
			typeof( TerminalControlResult<TerminalDimensions> ),
			getDimensions.ReturnType
		);
		Assert.Empty( getDimensions.GetParameters() );
		Assert.Equal( typeof( TerminalDimensions? ), dimensions.PropertyType );
		Assert.False( dimensions.CanWrite );
		Assert.Equal( typeof( TerminalControlResult<TerminalSize> ), getSize.ReturnType );
		Assert.Empty( getSize.GetParameters() );
		Assert.Equal( typeof( TerminalSize? ), size.PropertyType );
		Assert.False( size.CanWrite );

		HashSet<Type> newSignatureTypes = [];
		AddTypeClosure( newSignatureTypes, typeof( TerminalDimensions ) );
		AddTypeClosure( newSignatureTypes, getDimensions.ReturnType );
		AddTypeClosure( newSignatureTypes, dimensions.PropertyType );
		foreach ( ConstructorInfo constructor in typeof( TerminalDimensions ).GetConstructors() ) {
			foreach ( ParameterInfo parameter in constructor.GetParameters() ) {
				AddTypeClosure( newSignatureTypes, parameter.ParameterType );
			}
		}
		foreach ( PropertyInfo property in typeof( TerminalDimensions ).GetProperties() ) {
			AddTypeClosure( newSignatureTypes, property.PropertyType );
		}

		Assert.DoesNotContain(
			newSignatureTypes,
			static type => string.Equals(
				type.Namespace,
				"Icod.TermInfo",
				StringComparison.Ordinal
			)
		);
	}

	private static TerminalControlResult<TerminalSize> CreateSizeResult(
		TerminalControlStatus status,
		string? message,
		int? nativeErrorCode
	) {
		return status switch {
			TerminalControlStatus.Available => TerminalControlResult<TerminalSize>.Available(
				new TerminalSize( 132, 43 )
			),
			TerminalControlStatus.Unavailable =>
				TerminalControlResult<TerminalSize>.Unavailable( message, nativeErrorCode ),
			TerminalControlStatus.Unsupported =>
				TerminalControlResult<TerminalSize>.Unsupported( message ),
			TerminalControlStatus.Failed =>
				TerminalControlResult<TerminalSize>.Failed( message, nativeErrorCode ),
			_ => throw new ArgumentOutOfRangeException( nameof( status ) )
		};
	}

	private static void AssertEventDimensions(
		TerminalLifecycleEvent value,
		TerminalLifecycleEventKind kind,
		int columns,
		int rows
	) {
		ArgumentNullException.ThrowIfNull( value );
		TerminalSize expectedSize = new( columns, rows );
		TerminalDimensions expectedDimensions = new( columns, rows );

		Assert.Equal( kind, value.Kind );
		Assert.Equal( expectedSize, value.Size );
		Assert.Equal( expectedDimensions, value.Dimensions );
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
		RecordingTerminalControlProvider provider,
		TestTerminalLifecycleSource? lifecycle = null
	) {
		ArgumentNullException.ThrowIfNull( provider );
		return TerminalSession.OpenAsync(
			provider,
			TerminalEndpoint.StandardInput,
			TerminalEndpoint.StandardOutput,
			new TestTerminalInput(),
			new TestTerminalOutput(),
			new TerminalSessionOptions {
				TerminalOverride = TerminalProfiles.Dumb,
				ConfigureOutput = false,
				LifecycleSource = lifecycle
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

	private sealed class TestTerminalOutput : ITerminalOutput {
		public ValueTask WriteAsync(
			ReadOnlyMemory<byte> buffer,
			CancellationToken cancellationToken = default
		) {
			cancellationToken.ThrowIfCancellationRequested();
			return ValueTask.CompletedTask;
		}

		public ValueTask FlushAsync(
			CancellationToken cancellationToken = default
		) {
			cancellationToken.ThrowIfCancellationRequested();
			return ValueTask.CompletedTask;
		}
	}

	private sealed class TestTerminalLifecycleSource : ITerminalLifecycleSource {
		private readonly Channel<TerminalLifecycleSignal> signals =
			Channel.CreateUnbounded<TerminalLifecycleSignal>();

		internal void Publish(
			TerminalLifecycleSignalKind kind
		) {
			Assert.True(
				this.signals.Writer.TryWrite( new TerminalLifecycleSignal( kind ) )
			);
		}

		public ValueTask<TerminalLifecycleSignal> ReadAsync(
			CancellationToken cancellationToken = default
		) {
			return this.signals.Reader.ReadAsync( cancellationToken );
		}

		public void Dispose() {
			this.signals.Writer.TryComplete();
		}
	}

	private sealed class RecordingTerminalControlProvider : ITerminalControlProvider {
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

		internal TerminalControlResult<TerminalSize> SizeResult {
			get;
			set;
		} = TerminalControlResult<TerminalSize>.Available( new TerminalSize( 80, 24 ) );

		internal int SizeCallCount {
			get;
			private set;
		}

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
						| TerminalControlCapabilities.LiveSize
						| TerminalControlCapabilities.ModeRead
						| TerminalControlCapabilities.ModeWrite
				)
			);
		}

		public TerminalControlResult<TerminalSize> GetSize(
			TerminalEndpoint endpoint
		) {
			ArgumentNullException.ThrowIfNull( endpoint );
			++this.SizeCallCount;
			return this.SizeResult;
		}

		public TerminalControlResult<TerminalModeSnapshot> GetMode(
			TerminalEndpoint endpoint
		) {
			ArgumentNullException.ThrowIfNull( endpoint );
			return TerminalControlResult<TerminalModeSnapshot>.Available( this.baseline );
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
