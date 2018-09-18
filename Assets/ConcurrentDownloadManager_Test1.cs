using System;
using System.Collections;
using System.Collections.Generic;
using BestHTTP;
using UnityEngine;
using System.Diagnostics; // for testing the time/performance
using Debug = UnityEngine.Debug;
using System.Threading;

[Serializable]
public class ConcurrentDownloadManager_Test1 {

	[Serializable]
	internal class ContentRange {
		public readonly int start;
		public readonly int end;

		public ContentRange(int rangeStart, int rangeEnd) {
			start = rangeStart;
			end = rangeEnd;
		}

		public override string ToString() { return "" + start + " - " + end; }
	}

	#region Member Variables

	// State
	private long mContentSize = 0;
	private int mDownloadedContentSize = 0;
	private int mCompletedDownloadsCount = 0;
	private int mConnectionCount = 1;
	private List<HTTPRequest> mRequests = new List<HTTPRequest>();
	private bool mDownloadComplete = false;
	private List<HTTPRequest> mUnfinishedRequests 
		= new List<HTTPRequest>(); // for a back-up check if requests finished

	private List<ContentRange> mCompletedRanges = new List<ContentRange>();

	// When mFragmentSize or mConnectionCount is small and streaming is on,
	//    it will take multiple fragments to fill up one connection 
	//    This variable helps us keep track of how many
	//    bytes have been downloaded so that we know where the offset in memory
	//    is for fragments past the first one 
	private Dictionary<HTTPRequest, int> mRequestToBytesDownloaded =
		new Dictionary<HTTPRequest, int>();

	// Data
	private byte[] mDownloadedData = null;
	private int mConsecutiveBytesCount = 0;
	private int mCurrentThreadCount = 0;

	// Constants
	private const int mFragmentSize =  128 * 1024; 
	
	// Other
	private Stopwatch mStopwatch = new Stopwatch();
	private int mRequestsSent = 0;
	private int mCurrentRequestIndex = -1;
	private object mAddRangeLock = new object();
	private object mConsecutiveBytesLock = new object();

	#endregion

	#region Public Interface

	public ConcurrentDownloadManager_Test1() {
		HTTPManager.MaxConnectionPerServer = 255;
		ResetCompletedDownloadsCount();
	}

	public void Download(HTTPRequest request, int connectionCount) {
		Reset();
		mStopwatch.Start();
		SetConnectionCount(connectionCount);
		DoHeadRequest(request);
	}

	/// <summary>
    /// Returns a buffer of the consecutive downloaded bytes
    /// This is a direct reference and can modify the original data; this is fastest
    /// </summary>
    /// <returns>A direct reference to the consecutive downloaded bytes</returns>
	public ArraySegment<byte> GetConsecutiveBytes() {
		lock (mDownloadedData) {
			if (mDownloadedData == null) { return new ArraySegment<byte>(); }

			ArraySegment<byte> segment =
				new ArraySegment<byte>(mDownloadedData, 0, GetConsecutiveBytesCount());
			return segment;
		}
	}

	/// <summary>
    /// Copies the consecutive bytes into a new buffer and returns it.
    /// The original data is immutable, so this is safer, but slower.
    /// </summary>
    /// <returns>A copy of the consecutive downloaded bytes</returns>
	public byte[] GetConsecutiveBytesCopy() {
		lock (mDownloadedData) {
			if (mDownloadedData == null) { return null; }

			byte[] copyOfData = new byte[GetConsecutiveBytesCount()];
			Buffer.BlockCopy(mDownloadedData, 0, copyOfData, 0,
							 GetConsecutiveBytesCount());
			return copyOfData;
		}
	}

	public bool IsDownloadComplete() {
		//Debug.Log("Content size: " + mContentSize + "; ConsecutiveByteCnt: " + GetConsecutiveBytesCount());
		return GetCompletedDownloadsCount() >= mConnectionCount &&
			   GetConsecutiveBytesCount() >= mContentSize &&
			   mContentSize > 0 /*to ensure this only returns true AFTER a download begins*/
			   ;
	}

	public float GetPercentDone() {
		if (mContentSize <= 0) { return 0; }
		float value = ((float) mDownloadedContentSize / mContentSize);
		return Mathf.Clamp01(value);
	}

	public void CancelRequests() {
		HTTPManager.OnQuit();
	}

	#endregion

	#region Head Request

	private void DoHeadRequest(HTTPRequest request) {
		Uri requestUri = new Uri(request.Uri.AbsoluteUri);
		Debug.Log("Sending HEAD request to " + requestUri.AbsoluteUri);
		HTTPRequest headReq = new HTTPRequest(requestUri, HTTPMethods.Head, OnHeadRequestComplete);
		headReq.Send();
	}

	private void OnHeadRequestComplete(HTTPRequest request, HTTPResponse response) {
		if (response == null ||
			request.State == HTTPRequestStates.Error) {
			OnHeadRequestError(request);
			return;
		}

		long contentSize = Convert.ToInt64(response.GetFirstHeaderValue("content-length"));
		mContentSize = contentSize;

		Debug.Log("Head request is complete. Status code: " + response.StatusCode);
		ExecuteVideoDownload(request, mContentSize, mConnectionCount);
	}

	private void OnHeadRequestError(HTTPRequest request) {
		Debug.LogError("Error making HEAD request: " + request.Exception.Message);
	}

	#endregion

	#region Video Download

	private void ExecuteVideoDownload(HTTPRequest request, long contentSize, int connections) {
		AllocateMemory();

		//Debug.Log("Downloading content at " + request.Uri.AbsoluteUri);
		mConnectionCount = connections;
		Uri requestUri = new Uri(request.Uri.AbsoluteUri);
		
		for (int i = 0; i < connections; i++) {
			ContentRange range = GetContentRange(i, connections, contentSize);
			//Debug.Log("Content range request: " + range.start + "-" + range.end);
			HTTPRequest finalizedPartialRequest = PreparePartialDownloadRequest(requestUri, range);
			AddRequest(finalizedPartialRequest);
		}

		for (int i = 0; i < mRequests.Count; i++) { OnRequestInitialize(mRequests[i]); }
	}

	// Send the request
	private void OnRequestInitialize(HTTPRequest request) {
		//Debug.Log("Sending HTTP Request to " + request.Uri.AbsoluteUri);
		//Interlocked.Increment(ref mRequestsSent);
		request.Send();
	}

	private void OnDownloadFinish(HTTPRequest request, HTTPResponse response) {
		if (request.State == HTTPRequestStates.Error) {
			OnRequestError(request);
		} else if (request.State == HTTPRequestStates.TimedOut) {
			OnRequestTimeout(request);
		} else if (request.State == HTTPRequestStates.ConnectionTimedOut) {
			OnRequestConnectionTimeout(request);
		} else {
			ProcessDownloadedData(request,
								  response);
            if (request.State == HTTPRequestStates.Finished)
			    OnRequestFinish(request);
		}
	}

	private void OnRequestError(HTTPRequest request) {
		Debug.LogError("Error with request: "  +
		               "\n" + request.Exception.ToString() +
					   "\n" + request.Exception.StackTrace);
		OnRequestInitialize(request);
	}

	private void OnRequestTimeout(HTTPRequest request) {
		Debug.LogError("Request timed out: ");
		OnRequestInitialize(request);
	}

	private void OnRequestConnectionTimeout(HTTPRequest request) {
		Debug.LogError("Request connection timed out");
		OnRequestInitialize(request);
	}

    private void OnRequestFinish(HTTPRequest request) {
		//Debug.Log("On request finish");
		lock (mUnfinishedRequests) { mUnfinishedRequests.Remove(request); }

		if (request == null) { return; }
		OnDownloadComplete();

		if (AreDownloadsFinished()) {
			mStopwatch.Stop();
            this.elapsed = mStopwatch.Elapsed;
			Debug.Log("Time elapsed (ms): " + mStopwatch.Elapsed);
			Debug.Log("Consecutive bytes: " + GetConsecutiveBytesCount());
			//mDownloadComplete = true;
		}
	}

    private TimeSpan elapsed;
    public TimeSpan GetDownloadTime() { return this.elapsed; }

	private void OnDownloadComplete() { Interlocked.Increment(ref mCompletedDownloadsCount); }

    #endregion

    #region Memory & State

	private void AllocateMemory() {
		Debug.Assert(mContentSize > 1024); // just a sanity check for the content size; 1KB is arbitrary
		mDownloadedData = new byte[mContentSize]; // Allocate memory
		mConsecutiveBytesCount = 0;
	}

	private void ProcessDownloadedData(HTTPRequest request, HTTPResponse response) {
		//Debug.Log("Processing downloaded data");
		if (request == null || response == null) { return; }

		if (!mRequestToBytesDownloaded.ContainsKey(request)) {
			mRequestToBytesDownloaded[request] = 0;
		}

		if (!response.IsStreamed) {
			byte[] data = response.Data;

			// Copy data into memory
			int offset = response.GetRange().FirstBytePos + mRequestToBytesDownloaded[request];
			int length = data.Length;
			CopyDownloadedFragment(offset, length, ref data);

			// Keep track of how many bytes this connection downloaded & consecutive bytes
			Interlocked.Add(ref mDownloadedContentSize, length);
			Interlocked.Add(ref mConsecutiveBytesCount, length);
			mDownloadComplete = mConsecutiveBytesCount >= mContentSize
								&& mContentSize > 0;
		} else {
			List<byte[]> fragments = response.GetStreamedFragments();
            if (fragments == null)
                return;

			for (int i = 0; i < fragments.Count; i++) {
				byte[] fragment = fragments[i];

				// Copy data into memory
				int offset = response.GetRange().FirstBytePos + mRequestToBytesDownloaded[request];
				int length = fragment.Length;
				CopyDownloadedFragment(offset, length, ref fragment);

				// Keep track of how many bytes this connection downloaded & consecutive bytes
				Interlocked.Add(ref mDownloadedContentSize, length);
				mRequestToBytesDownloaded[request] += length;
				Interlocked.Add(ref mConsecutiveBytesCount, length);
				mDownloadComplete = mConsecutiveBytesCount >= mContentSize
									&& mContentSize > 0;
			}
		}
	}

    private void CopyDownloadedFragment(int offset, int length, ref byte[] data) {
		Buffer.BlockCopy(data, 0, mDownloadedData, offset, length);
	}

	#endregion

    #region Helpers

    private void Reset() {
		mContentSize = 0;
		mDownloadedContentSize = 0;
		mCompletedDownloadsCount = 0;
		mConnectionCount = 1;
		mRequests = new List<HTTPRequest>();
		mDownloadComplete = false;

        mCompletedRanges = new List<ContentRange>();

		// Data
		mDownloadedData = null;
		mConsecutiveBytesCount = 0;

		// Other
		mStopwatch = new Stopwatch();
        elapsed = TimeSpan.Zero;
		mRequestsSent = 0;
		mCurrentRequestIndex = -1;
	}

    // division is 0-indexed
    private ContentRange GetContentRange(int division, int totalDivisions, long contentLength) {
		long divisionSize = contentLength / totalDivisions;
		long offset = divisionSize * (division);

		long remainingBytes = contentLength % totalDivisions;
		if (remainingBytes > 0 && division == totalDivisions-1) { // last division
			divisionSize += remainingBytes; // get the last bytes (but sub 1 because index starts at 0)
		}

		ContentRange range = new ContentRange((int) offset, (int) (offset+divisionSize-1));
		return range;
	}

	private HTTPRequest PreparePartialDownloadRequest(Uri requestUri, ContentRange range) {
		HTTPRequest partialRequest = new HTTPRequest(requestUri, OnDownloadFinish);
		partialRequest.UseStreaming = true;
		partialRequest.DisableCache = true;
		partialRequest.SetRangeHeader(range.start, range.end);
        partialRequest.MaxFragmentQueueLength = 1000;

		return partialRequest;
	}

	#endregion

	#region Getters/Setters

	private void SetConnectionCount(int newConnectionCount) {
		Debug.Assert(newConnectionCount > 0);
		if (newConnectionCount <= 0) { return; }
		mConnectionCount = newConnectionCount;
	}

	private int GetConnectionCount() {
		Debug.Assert(mConnectionCount > 0);
		if (mConnectionCount <= 0) { SetConnectionCount(1); } // ensure valid output
		return mConnectionCount;
	}

	public int GetConsecutiveBytesCount() {
		return mConsecutiveBytesCount;
	}

	public byte[] GetDownloadedData() {
		return mDownloadedData;
	}

	private bool AreDownloadsFinished() {
		bool downloadsComplete = GetCompletedDownloadsCount() >= GetConnectionCount();
		return downloadsComplete;
	}

	private int GetFragmentSize() { return mFragmentSize; }

	private int GetCompletedDownloadsCount() { return mCompletedDownloadsCount; }
	private void ResetCompletedDownloadsCount() { mCompletedDownloadsCount = 0; }

	private List<HTTPRequest> GetRequests() { return mRequests; }

	private void AddRequest(HTTPRequest request) {
		GetRequests().Add(request);
		mUnfinishedRequests.Add(request);
	}
	private void RemoveRequest(HTTPRequest request) { GetRequests().Remove(request); }

	#endregion
}