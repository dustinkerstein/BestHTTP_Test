using System;
using System.Collections.Generic;
using UnityEngine;
using System.Collections;
using BestHTTP;

public sealed class DownloadSpeedTest_Test3 : MonoBehaviour
{
    public UnityEngine.UI.Text statusText;

    [NonSerialized]
	DistributedDownloadManager manager;

    private void Start()
    {
        Application.runInBackground = true;
		BestHTTP.HTTPManager.Setup ();
    }

    public void On_StartTestButtonClicked()
    {

		manager = new DistributedDownloadManager();
		string[] uris = new string[] {
			"http://cdn.panomoments.com/featured/shibuya_hq/N100.mp4",
			"http://cdn.panomoments.com/featured/shibuya_hq/N101.mp4",
			"http://cdn.panomoments.com/featured/shibuya_hq/N102.mp4",
			"http://cdn.panomoments.com/featured/shibuya_hq/N103.mp4",
			"http://cdn.panomoments.com/featured/shibuya_hq/N104.mp4",
			"http://cdn.panomoments.com/featured/shibuya_hq/N105.mp4",
			"http://cdn.panomoments.com/featured/shibuya_hq/N106.mp4",
			"http://cdn.panomoments.com/featured/shibuya_hq/N107.mp4",
			"http://cdn.panomoments.com/featured/shibuya_hq/N108.mp4",
			"http://cdn.panomoments.com/featured/shibuya_hq/N109.mp4",
			"http://cdn.panomoments.com/featured/shibuya_hq/N110.mp4",
			"http://cdn.panomoments.com/featured/shibuya_hq/N111.mp4",
			"http://cdn.panomoments.com/featured/shibuya_hq/N112.mp4",
			"http://cdn.panomoments.com/featured/shibuya_hq/N113.mp4",
			"http://cdn.panomoments.com/featured/shibuya_hq/N114.mp4",
			"http://cdn.panomoments.com/featured/shibuya_hq/N115.mp4",
			"http://cdn.panomoments.com/featured/shibuya_hq/N116.mp4",
			"http://cdn.panomoments.com/featured/shibuya_hq/N117.mp4",
			"http://cdn.panomoments.com/featured/shibuya_hq/N118.mp4",
			"http://cdn.panomoments.com/featured/shibuya_hq/N119.mp4",
			"http://cdn.panomoments.com/featured/shibuya_hq/N120.mp4",
			"http://cdn.panomoments.com/featured/shibuya_hq/N121.mp4",
			"http://cdn.panomoments.com/featured/shibuya_hq/N122.mp4",
			"http://cdn.panomoments.com/featured/shibuya_hq/N123.mp4",
			"http://cdn.panomoments.com/featured/shibuya_hq/N124.mp4",
			"http://cdn.panomoments.com/featured/shibuya_hq/N125.mp4",
			"http://cdn.panomoments.com/featured/shibuya_hq/N126.mp4",
			"http://cdn.panomoments.com/featured/shibuya_hq/N127.mp4",
			"http://cdn.panomoments.com/featured/shibuya_hq/N128.mp4",
			"http://cdn.panomoments.com/featured/shibuya_hq/N129.mp4",
			"http://cdn.panomoments.com/featured/shibuya_hq/N130.mp4",
			"http://cdn.panomoments.com/featured/shibuya_hq/N131.mp4",
			"http://cdn.panomoments.com/featured/shibuya_hq/N132.mp4",
			"http://cdn.panomoments.com/featured/shibuya_hq/N133.mp4",
			"http://cdn.panomoments.com/featured/shibuya_hq/N134.mp4",
			"http://cdn.panomoments.com/featured/shibuya_hq/N135.mp4",
			"http://cdn.panomoments.com/featured/shibuya_hq/N136.mp4",
			"http://cdn.panomoments.com/featured/shibuya_hq/N137.mp4",
			"http://cdn.panomoments.com/featured/shibuya_hq/N138.mp4",
			"http://cdn.panomoments.com/featured/shibuya_hq/N139.mp4",
			"http://cdn.panomoments.com/featured/shibuya_hq/N140.mp4",
			"http://cdn.panomoments.com/featured/shibuya_hq/N141.mp4",
			"http://cdn.panomoments.com/featured/shibuya_hq/N142.mp4",
			"http://cdn.panomoments.com/featured/shibuya_hq/N143.mp4",
			"http://cdn.panomoments.com/featured/shibuya_hq/N144.mp4",
			"http://cdn.panomoments.com/featured/shibuya_hq/N145.mp4",
			"http://cdn.panomoments.com/featured/shibuya_hq/N146.mp4",
			"http://cdn.panomoments.com/featured/shibuya_hq/N147.mp4",
			"http://cdn.panomoments.com/featured/shibuya_hq/N148.mp4",
			"http://cdn.panomoments.com/featured/shibuya_hq/N149.mp4",
			"http://cdn.panomoments.com/featured/shibuya_hq/N150.mp4",
			"http://cdn.panomoments.com/featured/shibuya_hq/N151.mp4",
			"http://cdn.panomoments.com/featured/shibuya_hq/N152.mp4",
			"http://cdn.panomoments.com/featured/shibuya_hq/N153.mp4",
			"http://cdn.panomoments.com/featured/shibuya_hq/N154.mp4",
			"http://cdn.panomoments.com/featured/shibuya_hq/N155.mp4",
			"http://cdn.panomoments.com/featured/shibuya_hq/N156.mp4",
			"http://cdn.panomoments.com/featured/shibuya_hq/N157.mp4",
			"http://cdn.panomoments.com/featured/shibuya_hq/N158.mp4",
			"http://cdn.panomoments.com/featured/shibuya_hq/N159.mp4",
			"http://cdn.panomoments.com/featured/shibuya_hq/N160.mp4",
			"http://cdn.panomoments.com/featured/shibuya_hq/N161.mp4",
			"http://cdn.panomoments.com/featured/shibuya_hq/N162.mp4",
			"http://cdn.panomoments.com/featured/shibuya_hq/N163.mp4",
			"http://cdn.panomoments.com/featured/shibuya_hq/N164.mp4",
			"http://cdn.panomoments.com/featured/shibuya_hq/N165.mp4",
			"http://cdn.panomoments.com/featured/shibuya_hq/N166.mp4",
			"http://cdn.panomoments.com/featured/shibuya_hq/N167.mp4",
			"http://cdn.panomoments.com/featured/shibuya_hq/N168.mp4",
			"http://cdn.panomoments.com/featured/shibuya_hq/N169.mp4",
			"http://cdn.panomoments.com/featured/shibuya_hq/N170.mp4",
			"http://cdn.panomoments.com/featured/shibuya_hq/N171.mp4",
			"http://cdn.panomoments.com/featured/shibuya_hq/N172.mp4",
			"http://cdn.panomoments.com/featured/shibuya_hq/N173.mp4",
			"http://cdn.panomoments.com/featured/shibuya_hq/N174.mp4",
			"http://cdn.panomoments.com/featured/shibuya_hq/N175.mp4",
			"http://cdn.panomoments.com/featured/shibuya_hq/N176.mp4",
			"http://cdn.panomoments.com/featured/shibuya_hq/N177.mp4",
			"http://cdn.panomoments.com/featured/shibuya_hq/N178.mp4",
			"http://cdn.panomoments.com/featured/shibuya_hq/N179.mp4",
			"http://cdn.panomoments.com/featured/shibuya_hq/N180.mp4",
			"http://cdn.panomoments.com/featured/shibuya_hq/N181.mp4",
			"http://cdn.panomoments.com/featured/shibuya_hq/N182.mp4",
			"http://cdn.panomoments.com/featured/shibuya_hq/N183.mp4",
			"http://cdn.panomoments.com/featured/shibuya_hq/N184.mp4",
			"http://cdn.panomoments.com/featured/shibuya_hq/N185.mp4",
			"http://cdn.panomoments.com/featured/shibuya_hq/N186.mp4",
			"http://cdn.panomoments.com/featured/shibuya_hq/N187.mp4",
			"http://cdn.panomoments.com/featured/shibuya_hq/N188.mp4",
			"http://cdn.panomoments.com/featured/shibuya_hq/N189.mp4",
			"http://cdn.panomoments.com/featured/shibuya_hq/N190.mp4",
			"http://cdn.panomoments.com/featured/shibuya_hq/N191.mp4",
			"http://cdn.panomoments.com/featured/shibuya_hq/N192.mp4",
			"http://cdn.panomoments.com/featured/shibuya_hq/N193.mp4",
			"http://cdn.panomoments.com/featured/shibuya_hq/N194.mp4",
			"http://cdn.panomoments.com/featured/shibuya_hq/N195.mp4",
			"http://cdn.panomoments.com/featured/shibuya_hq/N196.mp4",
			"http://cdn.panomoments.com/featured/shibuya_hq/N197.mp4",
			"http://cdn.panomoments.com/featured/shibuya_hq/N198.mp4",
			"http://cdn.panomoments.com/featured/shibuya_hq/N199.mp4",
			"http://cdn.panomoments.com/featured/shibuya_hq/N200.mp4",
			"http://cdn.panomoments.com/featured/shibuya_hq/N201.mp4",
			"http://cdn.panomoments.com/featured/shibuya_hq/N202.mp4",
			"http://cdn.panomoments.com/featured/shibuya_hq/N203.mp4",
			"http://cdn.panomoments.com/featured/shibuya_hq/N204.mp4",
			"http://cdn.panomoments.com/featured/shibuya_hq/N205.mp4",
			"http://cdn.panomoments.com/featured/shibuya_hq/N206.mp4",
			"http://cdn.panomoments.com/featured/shibuya_hq/N207.mp4",
			"http://cdn.panomoments.com/featured/shibuya_hq/N208.mp4",
			"http://cdn.panomoments.com/featured/shibuya_hq/N209.mp4",
			"http://cdn.panomoments.com/featured/shibuya_hq/N210.mp4",
			"http://cdn.panomoments.com/featured/shibuya_hq/N211.mp4",
			"http://cdn.panomoments.com/featured/shibuya_hq/N212.mp4",
			"http://cdn.panomoments.com/featured/shibuya_hq/N213.mp4",
			"http://cdn.panomoments.com/featured/shibuya_hq/N214.mp4",
			"http://cdn.panomoments.com/featured/shibuya_hq/N215.mp4",
			"http://cdn.panomoments.com/featured/shibuya_hq/N216.mp4",
			"http://cdn.panomoments.com/featured/shibuya_hq/N217.mp4",
			"http://cdn.panomoments.com/featured/shibuya_hq/N218.mp4",
			"http://cdn.panomoments.com/featured/shibuya_hq/N219.mp4",
			"http://cdn.panomoments.com/featured/shibuya_hq/N220.mp4",
			"http://cdn.panomoments.com/featured/shibuya_hq/N221.mp4",
			"http://cdn.panomoments.com/featured/shibuya_hq/N222.mp4",
			"http://cdn.panomoments.com/featured/shibuya_hq/N223.mp4",
			"http://cdn.panomoments.com/featured/shibuya_hq/N224.mp4",
			"http://cdn.panomoments.com/featured/shibuya_hq/N225.mp4",
			"http://cdn.panomoments.com/featured/shibuya_hq/N226.mp4",
			"http://cdn.panomoments.com/featured/shibuya_hq/N227.mp4",
			"http://cdn.panomoments.com/featured/shibuya_hq/N228.mp4",
			"http://cdn.panomoments.com/featured/shibuya_hq/N229.mp4",
			"http://cdn.panomoments.com/featured/shibuya_hq/N230.mp4",
			"http://cdn.panomoments.com/featured/shibuya_hq/N231.mp4",
			"http://cdn.panomoments.com/featured/shibuya_hq/N232.mp4",
			"http://cdn.panomoments.com/featured/shibuya_hq/N233.mp4",
			"http://cdn.panomoments.com/featured/shibuya_hq/N234.mp4",
			"http://cdn.panomoments.com/featured/shibuya_hq/N235.mp4",
			"http://cdn.panomoments.com/featured/shibuya_hq/N236.mp4",
			"http://cdn.panomoments.com/featured/shibuya_hq/N237.mp4",
			"http://cdn.panomoments.com/featured/shibuya_hq/N238.mp4",
			"http://cdn.panomoments.com/featured/shibuya_hq/N239.mp4",
			"http://cdn.panomoments.com/featured/shibuya_hq/N240.mp4",
			"http://cdn.panomoments.com/featured/shibuya_hq/N241.mp4",
			"http://cdn.panomoments.com/featured/shibuya_hq/N242.mp4",
			"http://cdn.panomoments.com/featured/shibuya_hq/N243.mp4",
			"http://cdn.panomoments.com/featured/shibuya_hq/N244.mp4",
			"http://cdn.panomoments.com/featured/shibuya_hq/N245.mp4",
			"http://cdn.panomoments.com/featured/shibuya_hq/N246.mp4",
			"http://cdn.panomoments.com/featured/shibuya_hq/N247.mp4",
			"http://cdn.panomoments.com/featured/shibuya_hq/N248.mp4",
			"http://cdn.panomoments.com/featured/shibuya_hq/N249.mp4",
			"http://cdn.panomoments.com/featured/shibuya_hq/N250.mp4"
		};
		RemoteFileMetadata fileMetadata = new RemoteFileMetadata();
		for (int i = 0; i < uris.Length; i++) {
			fileMetadata.AddFileFragmentData(new Uri(uris[i]), (uint) i);
		}
		manager.Download(fileMetadata, 1);

	}

    private void Update()
    {

    }
		
}