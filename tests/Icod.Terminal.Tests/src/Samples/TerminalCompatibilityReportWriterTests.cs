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
using Icod.Terminal.Compatibility.Sample;
using Xunit;

public sealed class TerminalCompatibilityReportWriterTests {
	[Fact]
	public async Task WritesValidatedClosedJsonBesideTemporaryFile() {
		using TemporaryDirectory directory = new();
		string destination = Path.Combine( directory.Path, "evidence.json" );
		var storage = new RecordingStorage();

		await CompatibilityReportWriter.WriteAsync(
			destination,
			[ TerminalCompatibilityEvidenceTests.Valid() ],
			overwrite: false,
			CancellationToken.None,
			storage
		);

		Assert.True( File.Exists( destination ) );
		Assert.Equal( directory.Path, Path.GetDirectoryName( Assert.Single( storage.TemporaryPaths ) ) );
		using JsonDocument document = JsonDocument.Parse( await File.ReadAllTextAsync( destination ) );
		JsonElement item = document.RootElement[ 0 ];
		Assert.Equal( "kitty", item.GetProperty( "terminalId" ).GetString() );
		Assert.False( item.TryGetProperty( "clipboardPayload", out _ ) );
		Assert.False( item.TryGetProperty( "rawInput", out _ ) );
	}

	[Fact]
	public async Task ExistingDestinationIsProtectedUnlessOverwriteIsExplicit() {
		using TemporaryDirectory directory = new();
		string destination = Path.Combine( directory.Path, "evidence.json" );
		await File.WriteAllTextAsync( destination, "original" );

		await Assert.ThrowsAsync<IOException>(
			() => CompatibilityReportWriter.WriteAsync(
				destination,
				[ TerminalCompatibilityEvidenceTests.Valid() ],
				overwrite: false,
				CancellationToken.None
			).AsTask()
		);
		Assert.Equal( "original", await File.ReadAllTextAsync( destination ) );

		await CompatibilityReportWriter.WriteAsync(
			destination,
			[ TerminalCompatibilityEvidenceTests.Valid() ],
			overwrite: true,
			CancellationToken.None
		);
		Assert.StartsWith( "[", await File.ReadAllTextAsync( destination ), StringComparison.Ordinal );
	}

	[Fact]
	public async Task CancellationBeforeRenameLeavesNoDestinationOrTemporaryFile() {
		using TemporaryDirectory directory = new();
		string destination = Path.Combine( directory.Path, "evidence.json" );
		using CancellationTokenSource cancellation = new();
		var storage = new RecordingStorage {
			AfterRead = cancellation.Cancel
		};

		await Assert.ThrowsAnyAsync<OperationCanceledException>(
			() => CompatibilityReportWriter.WriteAsync(
				destination,
				[ TerminalCompatibilityEvidenceTests.Valid() ],
				overwrite: false,
				cancellation.Token,
				storage
			).AsTask()
		);

		Assert.False( File.Exists( destination ) );
		Assert.All( storage.TemporaryPaths, path => Assert.False( File.Exists( path ) ) );
	}

	[Theory]
	[InlineData( FailureStage.Write )]
	[InlineData( FailureStage.Rename )]
	public async Task WriteAndRenameFailureCleanTemporaryFile(
		FailureStage stage
	) {
		using TemporaryDirectory directory = new();
		string destination = Path.Combine( directory.Path, "evidence.json" );
		var storage = new RecordingStorage {
			Failure = stage
		};

		await Assert.ThrowsAsync<IOException>(
			() => CompatibilityReportWriter.WriteAsync(
				destination,
				[ TerminalCompatibilityEvidenceTests.Valid() ],
				overwrite: false,
				CancellationToken.None,
				storage
			).AsTask()
		);

		Assert.False( File.Exists( destination ) );
		Assert.All( storage.TemporaryPaths, path => Assert.False( File.Exists( path ) ) );
	}

	[Fact]
	public async Task InvalidBoundedNoteFailsBeforeCreatingTemporaryFile() {
		using TemporaryDirectory directory = new();
		var storage = new RecordingStorage();
		CompatibilityEvidence invalid = TerminalCompatibilityEvidenceTests.Valid() with {
			Note = new string( 'x', 513 )
		};

		await Assert.ThrowsAsync<FormatException>(
			() => CompatibilityReportWriter.WriteAsync(
				Path.Combine( directory.Path, "evidence.json" ),
				[ invalid ],
				overwrite: false,
				CancellationToken.None,
				storage
			).AsTask()
		);

		Assert.Empty( storage.TemporaryPaths );
	}

	public enum FailureStage {
		None,
		Write,
		Rename
	}

	private sealed class RecordingStorage : ICompatibilityReportStorage {
		private readonly CompatibilityReportFileStorage inner = new();

		internal FailureStage Failure { get; init; }

		internal Action? AfterRead { get; init; }

		internal List<string> TemporaryPaths { get; } = [];

		public bool FileExists( string path ) => this.inner.FileExists( path );

		public bool DirectoryExists( string path ) => this.inner.DirectoryExists( path );

		public Stream CreateTemporaryFile( string path ) {
			this.TemporaryPaths.Add( path );
			Stream stream = this.inner.CreateTemporaryFile( path );
			return FailureStage.Write == this.Failure
				? new FailingWriteStream( stream )
				: stream;
		}

		public async ValueTask<string> ReadAllTextAsync(
			string path,
			CancellationToken cancellationToken
		) {
			string value = await this.inner.ReadAllTextAsync( path, cancellationToken );
			this.AfterRead?.Invoke();
			return value;
		}

		public void Move( string source, string destination, bool overwrite ) {
			if ( FailureStage.Rename == this.Failure ) {
				throw new IOException( "Injected rename failure." );
			}
			this.inner.Move( source, destination, overwrite );
		}

		public void Delete( string path ) => this.inner.Delete( path );
	}

	private sealed class FailingWriteStream : Stream {
		private readonly Stream inner;

		internal FailingWriteStream( Stream inner ) {
			this.inner = inner;
		}

		public override bool CanRead => false;
		public override bool CanSeek => false;
		public override bool CanWrite => true;
		public override long Length => this.inner.Length;
		public override long Position { get => this.inner.Position; set => throw new NotSupportedException(); }
		public override void Flush() => this.inner.Flush();
		public override int Read( byte[] buffer, int offset, int count ) => throw new NotSupportedException();
		public override long Seek( long offset, SeekOrigin origin ) => throw new NotSupportedException();
		public override void SetLength( long value ) => this.inner.SetLength( value );
		public override void Write( byte[] buffer, int offset, int count ) => throw new IOException( "Injected write failure." );
		public override ValueTask WriteAsync(
			ReadOnlyMemory<byte> buffer,
			CancellationToken cancellationToken = default
		) => ValueTask.FromException( new IOException( "Injected write failure." ) );

		protected override void Dispose( bool disposing ) {
			if ( disposing ) {
				this.inner.Dispose();
			}
			base.Dispose( disposing );
		}

		public override async ValueTask DisposeAsync() {
			await this.inner.DisposeAsync();
			GC.SuppressFinalize( this );
		}
	}

	private sealed class TemporaryDirectory : IDisposable {
		internal TemporaryDirectory() {
			this.Path = System.IO.Path.Combine(
				System.IO.Path.GetTempPath(),
				$"icod-report-{Guid.NewGuid():N}"
			);
			Directory.CreateDirectory( this.Path );
		}

		internal string Path { get; }

		public void Dispose() => Directory.Delete( this.Path, recursive: true );
	}
}
