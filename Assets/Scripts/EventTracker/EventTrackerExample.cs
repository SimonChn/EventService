using System;
using System.Collections;

using UnityEngine;

using EventService;

public class EventTrackerExample : MonoBehaviour
{

    [SerializeField] private string serverUrl = "http://localhost:8888/";
    [SerializeField] private float cooldownBeforeSendInSec = 5f;

    private IEventTrackable eventTracker;

    private IEnumerator Start()
    {   
        string backUpDirectory = $"{Application.persistentDataPath}/BackUp";

        var trackCore = new EventTracker(serverUrl, backUpDirectory, cooldownBeforeSendInSec, doEncryption: false);
        eventTracker = new EventTrackerThreadWrap(trackCore);

        eventTracker.TrackEvent("levelStart", "3");
        eventTracker.TrackEvent("coinsSpend", "100");

        yield return new WaitForSeconds(cooldownBeforeSendInSec * 1.5f);

        eventTracker.TrackEvent("levelStart", "4");
    }

    private void OnDestroy()
    {
        if(eventTracker != null && eventTracker is IDisposable disposable)
        {
            disposable.Dispose();
        }
    }
}
