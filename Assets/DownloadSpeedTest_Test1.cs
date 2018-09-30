using System;
using System.Collections.Generic;
using UnityEngine;
using BestHTTP;

public sealed class DownloadSpeedTest_Test1 : MonoBehaviour
{
    public UnityEngine.UI.Text statusText;

    [NonSerialized]
	ConcurrentDownloadManager_Test1 manager;

    private void Start()
    {
        Application.runInBackground = true;
        HTTPUpdateDelegator.IsThreaded = true;
        HTTPManager.KeepAliveDefaultValue = true;
        HTTPManager.IsCookiesEnabled = false;
        HTTPManager.EnablePrivateBrowsing = true;
        HTTPManager.IsCachingDisabled = false;
        
        int workerThreads, completionThreads;
        //System.Threading.ThreadPool.GetAvailableThreads(out workerThreads, out completionThreads);
        //Debug.Log("Available worker threads: " + workerThreads);

        System.Threading.ThreadPool.GetMinThreads(out workerThreads, out completionThreads);
        Debug.Log("Min worker threads: " + workerThreads);

        System.Threading.ThreadPool.GetMaxThreads(out workerThreads, out completionThreads);
        Debug.Log("Max worker threads: " + workerThreads);
    }

    public void On_StartTestButtonClicked()
    {
		manager = new ConcurrentDownloadManager_Test1();
		manager.Download(new BestHTTP.HTTPRequest(new Uri("http://s3.amazonaws.com/data.panomoments.com/processed/58a7514e550d37000bfd46d2/5a90ce21d3df99000e981d11/uhd_dashinit.mp4")), 8);
    }

    private void Update()
    {
        if (this.manager != null)
        {
            if (!this.manager.IsDownloadComplete())
                this.statusText.text = this.manager.GetPercentDone().ToString("F2");
            else
                this.statusText.text = "Finished in " + this.manager.GetDownloadTime().TotalMilliseconds.ToString("N0") + "ms";
        }
    }
}