using EventService;

class Program
{
    public static void Main()
    {
        MainAsync().GetAwaiter().GetResult();
    }

    private static async Task MainAsync()
    {
        string url = "http://localhost:8888/";
        string backUpPath = @"D:\Projects\C#\EventTrackerService\Save";

        var trackCore = new EventTracker(url, backUpPath, 4);

        Console.WriteLine("Start");

        IEventTrackable tracker = new EventTrackerThreadWrap(trackCore);

        tracker.TrackEvent("1", "5");
        tracker.TrackEvent("2", "6");

        await Task.Delay(20000);

        tracker.TrackEvent("2", "6");

        Thread.Sleep(15000);
        Console.WriteLine("End");

        if (tracker is IDisposable disposable)
        {
            Console.WriteLine("dispose");
            disposable.Dispose();
        }
    }
}