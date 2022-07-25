using System;
using System.Collections.Concurrent;
using System.Threading;

namespace EventService
{
    public class EventTrackerThreadWrap : IEventTrackable, IDisposable
    {
        private readonly IEventTrackable eventTracker;

        private readonly ConcurrentQueue<(string, string)> events = new ConcurrentQueue<(string, string)>();
        private readonly ManualResetEvent mre = new ManualResetEvent(true);
        private readonly Thread workingThread;

        private bool disposed = false;

        public EventTrackerThreadWrap(IEventTrackable eventTracker)
        {
            this.eventTracker = eventTracker;     

            workingThread = new Thread(SendEvents)
            {
                IsBackground = true,
                Priority = ThreadPriority.BelowNormal,             
            };
            workingThread.Start();
        }

        public void Dispose()
        {           
            disposed = true;
            workingThread?.Interrupt();

            if(eventTracker is IDisposable disposable)
            {
                disposable.Dispose();
            }

            GC.SuppressFinalize(this);         
        }

        public void TrackEvent(string type, string data)
        {
            events.Enqueue((type, data));
            mre.Set();
        }

        private void SendEvents()
        {
            try
            {
                while (!disposed)
                {
                    while (!events.IsEmpty)
                    {
                        try
                        {

                            if (events.TryDequeue(out (string type, string data) eventToSend))
                            {
                                eventTracker.TrackEvent(eventToSend.type, eventToSend.data);
                            }
                            else
                            {
                                Thread.Sleep(millisecondsTimeout: 5);
                            }
                        }
                        catch (Exception) //It's not the best practise but ok
                        {
                            break;
                        }
                    }

                    mre.Reset();
                    mre.WaitOne(millisecondsTimeout: 500);
                }
            }
            catch (ThreadInterruptedException)
            {

            }         
        }
    }
}