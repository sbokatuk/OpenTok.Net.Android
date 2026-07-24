using Android.Util;

namespace OpenTok.Net.Android.DeviceTests;

/// <summary>
/// Runs <see cref="SmokeTests"/> on launch and reports the verdict to logcat.
/// </summary>
/// <remarks>
/// There is no test framework here on purpose. A unit-test runner on device would have to be
/// installed, instrumented and scraped for results; this app has one job - drive the real SDK once
/// and say whether it worked - and a bare Activity does that with nothing between the checks and
/// the runtime.
/// <para>
/// <c>Name</c> is pinned rather than left to the generated <c>crc64…</c> value so that
/// <c>adb shell am start</c> has a stable target across builds; see
/// .github/scripts/run-emulator-tests.sh.
/// </para>
/// </remarks>
[Activity(
    Name = "com.sbokatuk.opentok.android.devicetests.MainActivity",
    Label = "OpenTok.Net.Android Device Tests",
    MainLauncher = true)]
public class MainActivity : Activity
{
    /// <summary>The logcat tag the runner script filters on.</summary>
    private const string Tag = "OpenTokE2E";

    /// <summary>
    /// The line the runner script greps for. Nothing else prints it.
    /// </summary>
    private const string DoneMarker = "OPENTOK_E2E_DONE";

    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);

        // Off the UI thread: creating a publisher does camera/microphone and file work, and a
        // StrictMode violation on the main thread would be reported as a failure of whichever
        // check happened to be running.
        Task.Run(RunSmokeTests);
    }

    private static void RunSmokeTests()
    {
        var passed = 0;
        var failed = 0;

        SmokeTests.Reporter = message => Log.Info(Tag, $"      {message}");

        foreach (var test in SmokeTests.All)
        {
            try
            {
                test.Execute();
                passed++;
                Log.Info(Tag, $"PASS  {test.Name}");
            }
            catch (Exception exception)
            {
                failed++;
                Log.Error(Tag, $"FAIL  {test.Name}: {exception}");
            }
        }

        // The verdict is a single line so the runner can decide from a grep. It is printed whether
        // or not anything failed, so a timeout is distinguishable from a failure.
        var verdict = failed == 0 ? "PASS" : "FAIL";
        Log.Info(Tag, $"{DoneMarker} {verdict} passed={passed} failed={failed}");
    }
}
