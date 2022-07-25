namespace EventService
{
    public interface IEventTrackable
    {
        public void TrackEvent(string type, string data);
    }
}
