using Hex1b;

namespace Dotsider.Tests;

/// <summary>
/// Records the focus ring from inside a Hex1b root component so tests never enumerate
/// <see cref="Hex1bApp.Focusables"/> or <see cref="Hex1bApp.FocusedNode"/> from the test thread.
/// The root component runs on the app run loop, which also dispatches input and rebuilds the
/// focus ring after layout, so the ring is stable while <see cref="Observe"/> copies it.
/// </summary>
internal sealed class AppFocusProbe
{
    private readonly Lock _gate = new();
    private FocusObservation _latest = FocusObservation.None;
    private TaskCompletionSource<FocusObservation>? _nextBuild;

    /// <summary>
    /// Copies the app's focus ring. Call this only from the app's root component.
    /// </summary>
    /// <param name="app">The app whose root component is running.</param>
    internal void Observe(Hex1bApp app)
    {
        // Claim the waiter before reading the ring so every waiter receives a ring read after
        // it registered, never one read before.
        TaskCompletionSource<FocusObservation>? waiter;
        lock (_gate)
        {
            waiter = _nextBuild;
            _nextBuild = null;
        }

        var observation = new FocusObservation(app.FocusedNode, [.. app.Focusables]);
        lock (_gate)
            _latest = observation;
        waiter?.TrySetResult(observation);
    }

    /// <summary>
    /// Returns the most recent observation and requests another frame, so a polling
    /// <c>WaitUntil</c> predicate sees focus changes made after the last root build.
    /// </summary>
    /// <param name="app">The observed app.</param>
    /// <returns>The focus ring copied by the most recent root build.</returns>
    internal FocusObservation Poll(Hex1bApp app)
    {
        app.Invalidate();
        lock (_gate)
            return _latest;
    }

    /// <summary>
    /// Requests a frame and returns the focus ring copied by the first root build that starts
    /// after this call, so a single assertion sees every focus change made before it.
    /// </summary>
    /// <param name="app">The observed app.</param>
    /// <param name="cancellationToken">Cancels the wait for the next root build.</param>
    /// <returns>The focus ring copied by the next root build.</returns>
    internal Task<FocusObservation> NextAsync(Hex1bApp app, CancellationToken cancellationToken)
    {
        Task<FocusObservation> next;
        lock (_gate)
        {
            _nextBuild ??= new TaskCompletionSource<FocusObservation>(TaskCreationOptions.RunContinuationsAsynchronously);
            next = _nextBuild.Task;
        }

        app.Invalidate();
        return next.WaitAsync(cancellationToken);
    }
}
