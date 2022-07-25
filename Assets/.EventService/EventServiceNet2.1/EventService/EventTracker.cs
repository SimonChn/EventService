using System;
using System.Collections.Generic;

using System.IO;
using System.Text;

using System.Net;
using System.Net.Http;

using System.Threading;
using System.Threading.Tasks;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace EventService
{
    public class EventTracker : IEventTrackable
    {
        private const string weakPassword = "1234567890";

        private static readonly SemaphoreSlim semaphoreSlim = new SemaphoreSlim(1, 1);

        private readonly HttpClient httpClient = new HttpClient(); //TODO HttpClientFactory

        private readonly string serverUrl;
        private readonly string backUpDirectory;
        private readonly int cooldownBeforeSendInMilliSeconds;

        private readonly bool doEncryption;

        private List<Event> eventsToSend;
        private string jsonToSend = string.Empty;

        public EventTracker(string serverUrl, string backUpDirectory, float cooldownBeforeSendInSec = 5, bool doEncryption = false)
        {
            this.serverUrl = serverUrl;
            this.backUpDirectory = backUpDirectory;
            cooldownBeforeSendInMilliSeconds = (int)(cooldownBeforeSendInSec * 1000);

            this.doEncryption = doEncryption;

            eventsToSend = LoadBackUp();
            if (eventsToSend.Count > 0)
            {
                SendEvents();
            }
        }

        public async void TrackEvent(string type, string data)
        {
            await semaphoreSlim.WaitAsync();

            AddEvent(type, data);

            semaphoreSlim.Release();

            if (eventsToSend.Count == 1)
            {
                SendEvents();
            }
        }

        private List<Event> LoadBackUp()
        {
            List<Event> loadedEvents;

            try
            {
                using (FileStream fstream = new FileStream(GetFullFilePath(), FileMode.Open))
                {
                    byte[] buffer = new byte[fstream.Length];

                    fstream.Read(buffer, 0, buffer.Length);

                    string encryptedJson = Parameters.ContentEncoding.GetString(buffer);

                    jsonToSend = (doEncryption ? XOR(encryptedJson) : encryptedJson);
                }

                var jObject = JObject.Parse(jsonToSend);
                var eventsFromJObject = jObject[Constants.EventsAlias]?.ToObject<List<Event>>();

                if (eventsFromJObject != null)
                {
                    loadedEvents = eventsFromJObject;
                }
                else
                {
                    loadedEvents = new List<Event>();
                }
            }
            catch (Exception e)
            when (e is FileNotFoundException || e is JsonReaderException)
            {
                loadedEvents = new List<Event>();
            }

            return loadedEvents;
        }

        private string GetFullFilePath()
        {
            if (!Directory.Exists(backUpDirectory))
            {
                Directory.CreateDirectory(backUpDirectory);
            }

            return @$"{backUpDirectory}\{Constants.BackUpFileName}";
        }

        private string XOR(string value)  //TODO Move encrypting to another service
        {
            byte[] valueArr = Parameters.ContentEncoding.GetBytes(value);
            byte[] keyArr = Parameters.ContentEncoding.GetBytes(weakPassword);

            for (int i = 0; i < valueArr.Length; i++)
            {
                valueArr[i] = (byte)(valueArr[i] ^ keyArr[i % keyArr.Length]);
            }

            return Parameters.ContentEncoding.GetString(valueArr);
        }

        private void SendEvents()
        {
            Task.Run(async () =>
            {
                await Task.Delay(cooldownBeforeSendInMilliSeconds);
                PostEvents();    
            });
        }

        private async void PostEvents()
        {                  
            await semaphoreSlim.WaitAsync();

            try
            {
                var content = new StringContent(jsonToSend, Parameters.ContentEncoding, Parameters.MediaType);

                var response = await httpClient.PostAsync(serverUrl, content);

                if (response.StatusCode == HttpStatusCode.OK)
                {
                    ClearEventsToSend();
                }
            }
            catch (HttpRequestException)
            {
                //TODO: Proceed network async exceptions
            }

            semaphoreSlim.Release();
        }

        private void ClearEventsToSend()
        {
            eventsToSend.Clear();
            UpdateJsonToSendString();
        }

        private void AddEvent(string type, string data)
        {
            eventsToSend.Add(new Event(type, data));
            UpdateJsonToSendString();
        }

        private void UpdateJsonToSendString()
        {
            var jObject = new JObject
            {
                { Constants.EventsAlias, JArray.FromObject(eventsToSend) }
            };

            jsonToSend = jObject.ToString();

            string encriptedString = (doEncryption ? XOR(jsonToSend) : jsonToSend);

            using (FileStream fstream = new FileStream(GetFullFilePath(), FileMode.Create))
            {
                byte[] input = Parameters.ContentEncoding.GetBytes(encriptedString);
                fstream.Write(input, 0, input.Length);              
            }
        }

        private readonly struct Event
        {
            [JsonProperty(Constants.TypeAlias)]
            public string Type { get; }

            [JsonProperty(Constants.DataAlias)]
            public string Data { get; }

            public Event(string type, string data)
            {
                Type = type;
                Data = data;
            }
        }

        private static class Constants
        {
            public const string TypeAlias = "type";
            public const string DataAlias = "data";
            public const string EventsAlias = "events";

            public const string BackUpFileName = "Events.bckp";
        }

        private static class Parameters
        {
            public const string MediaType = "application/json";
            public static readonly Encoding ContentEncoding = Encoding.UTF8;
        }
    }
}