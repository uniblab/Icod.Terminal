namespace Icod.Terminal;

/// <summary>
/// Reversible presentation-state ownership for <see cref="TerminalSession"/>.
/// </summary>
public sealed partial class TerminalSession {
	private readonly TerminalPresentationManager presentationManager;

	/// <summary>
	/// Acquires one reversible set of terminal presentation-state requirements.
	/// </summary>
	/// <param name="options">The presentation state required while the lease is active.</param>
	/// <param name="cancellationToken">Cancellation for acquisition only.</param>
	/// <returns>
	/// An available result containing the acquired lease, or a controlled unavailable
	/// result when the selected terminal does not advertise the required capabilities.
	/// </returns>
	public async ValueTask<TerminalControlResult<TerminalPresentationLease>> AcquirePresentationAsync(
		TerminalPresentationOptions options,
		CancellationToken cancellationToken = default
	) {
		ArgumentNullException.ThrowIfNull( options );
		options.Validate();
		cancellationToken.ThrowIfCancellationRequested();

		using IDisposable composition = await this.AcquireStateCompositionAsync(
			cancellationToken
		).ConfigureAwait( false );
		return await this.presentationManager.AcquireAsync(
			options,
			cancellationToken
		).ConfigureAwait( false );
	}

	private void InvalidatePresentationState() {
		this.presentationManager.Invalidate();
		this.InvalidateProgressState();
		this.InvalidatePointerShapeState();
		this.InvalidatePaletteColorState();
		this.InvalidateDynamicColorState();
	}

	private async ValueTask SuspendPresentationStateAsync() {
		using IDisposable composition = await this.AcquireStateCompositionAsync(
			CancellationToken.None
		).ConfigureAwait( false );
		await this.presentationManager.SuspendAsync().ConfigureAwait( false );
	}

	private async ValueTask ResumePresentationStateAsync() {
		using IDisposable composition = await this.AcquireStateCompositionAsync(
			CancellationToken.None
		).ConfigureAwait( false );

		bool externalResume = 0 == Volatile.Read( ref this.lifecycleStateReleased );
		if ( externalResume ) {
			await this.inputProtocolManager.ReenterAsync().ConfigureAwait( false );
		}

		await this.presentationManager.ReenterAsync().ConfigureAwait( false );
		if ( externalResume ) {
			Interlocked.Exchange(
				ref this.inputProtocolsReenteredBeforePresentation,
				1
			);
		}
	}

	private async ValueTask<Exception?> ClosePresentationStateAsync() {
		List<Exception> exceptions = [];

		Exception? cursorStyleException =
			await this.CloseCursorStyleStateAsync().ConfigureAwait( false );
		if ( cursorStyleException is not null ) {
			exceptions.Add( cursorStyleException );
		}

		Exception? hyperlinkException =
			await this.CloseHyperlinkStateAsync().ConfigureAwait( false );
		if ( hyperlinkException is not null ) {
			exceptions.Add( hyperlinkException );
		}

		Exception? paletteColorException =
			await this.ClosePaletteColorStateAsync().ConfigureAwait( false );
		if ( paletteColorException is not null ) {
			exceptions.Add( paletteColorException );
		}

		Exception? dynamicColorException =
			await this.CloseDynamicColorStateAsync().ConfigureAwait( false );
		if ( dynamicColorException is not null ) {
			exceptions.Add( dynamicColorException );
		}

		try {
			using IDisposable composition = await this.AcquireStateCompositionAsync(
				CancellationToken.None
			).ConfigureAwait( false );
			await this.presentationManager.CloseAsync().ConfigureAwait( false );
		} catch ( Exception exception ) {
			exceptions.Add( exception );
		}

		Exception? pointerShapeException =
			await this.ClosePointerShapeStateAsync().ConfigureAwait( false );
		if ( pointerShapeException is not null ) {
			exceptions.Add( pointerShapeException );
		}

		Exception? progressException =
			await this.CloseProgressStateAsync().ConfigureAwait( false );
		if ( progressException is not null ) {
			exceptions.Add( progressException );
		}

		Exception? synchronizedOutputException =
			await this.CloseSynchronizedOutputStateAsync().ConfigureAwait( false );
		if ( synchronizedOutputException is not null ) {
			exceptions.Add( synchronizedOutputException );
		}

		return exceptions.Count switch {
			0 => null,
			1 => exceptions[ 0 ],
			_ => new AggregateException(
				"Multiple errors occurred while closing terminal output state.",
				exceptions
			)
		};
	}
}
