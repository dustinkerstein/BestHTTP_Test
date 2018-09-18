using System;
using System.Collections;
using System.Collections.Generic;
using BestHTTP;
using UnityEngine;
using System.Diagnostics;
using System.Linq;
// for testing the time/performance
using Debug = UnityEngine.Debug;
using System.Threading;


/// <summary>
/// Each video file will be fragmented into multiple sections at different URIs. These fragments
/// have to each be downloaded and subsequently reassembled
/// 1. For each URI, do a head request to determine the fragment's content length
///        Otherwise, we wouldn't know where each fragment gets placed in memory,
///        and there would be extra memory usage which we want to avoid
/// 2. After all the content lengths are determined, GET requests should be sent to download the fragments
/// 3. As each fragment is being downloaded, each chunk needs to be copied into memory
///       TODO: update consecutive bytes state + count
/// 4. When all fragments are done, the download is done
/// </summary>
public class DistributedDownloadManager {

	#region Member Variables

	// State
	private long mContentSize = 0;
	private int mDownloadedContentSize = 0;
	private int mCompletedDownloadsCount = 0;
	private int mConnectionCount = 1;
	private int mThreadCount = 1;
	private bool mDownloadComplete = false;

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
	private List<Thread> mThreads = new List<Thread>();
	private int mCurrentThreadCount = 0;
	private RemoteFileMetadata m_RemoteFileMetadata;

	// Constants
	private const int mFragmentSize =  8 * 1024 * 1024; 
	
	// Other
	public Stopwatch mStopwatch = new Stopwatch();
	private string lastDownloadedURI = ""; // for error checking

	#endregion

	#region Public Interface

	public DistributedDownloadManager() {
		HTTPManager.MaxConnectionPerServer = 8;
		ResetCompletedDownloadsCount();
	}

	/// <summary>
    /// Downloads a file that is fragmented into different URIs
    /// </summary>
    /// <param name="fileMetadata"></param>
    /// <param name="threadCount"></param>
	public void Download(RemoteFileMetadata fileMetadata, int threadCount) {
		Reset();
		if (!fileMetadata.IsValid()) { return; }

        m_RemoteFileMetadata = fileMetadata;
		SetConnectionCount(fileMetadata.GetFileFragments().Count);
		SetThreadCount(threadCount);

		for (int i = 0; i < mThreadCount; ++i) {
			Thread t = new Thread(WorkerThread);
			mThreads.Add(t);
			t.Start();
		}

		mStopwatch.Start();
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
		if (mDownloadedData == null) { return null; }

		lock (mDownloadedData) {

			byte[] copyOfData = new byte[GetConsecutiveBytesCount()];
			Buffer.BlockCopy(mDownloadedData, 0, copyOfData, 0,
							 GetConsecutiveBytesCount());
			return copyOfData;
		}
	}

	public bool IsDownloadComplete() {
		//Debug.Log("Content size: " + mContentSize + "; ConsecutiveByteCnt: " + GetConsecutiveBytesCount());
		//return GetCompletedDownloadsCount() >= mConnectionCount &&
		//	   GetConsecutiveBytesCount() >= mContentSize &&
		//	   mContentSize > 0 /*to ensure this only returns true AFTER a download begins*/
		//	   ;
		return mDownloadComplete &&
			   mDownloadedContentSize >= mContentSize &&
			   mContentSize > 0;
	}

	public byte[] GetDownloadedData() { return mDownloadedData; }

	public float GetPercentDone() {
		if (mContentSize <= 0) { return 0; }
		float value = ((float) mDownloadedContentSize / mContentSize);
		return Mathf.Clamp01(value);
	}

	public void StopThreads() { mDownloadComplete = true; }

    #endregion

    #region Controller

	private void WorkerThread() {
		ConcurrentQueue<RemoteFileFragmentMetadata> fragmentQueue =
			m_RemoteFileMetadata.GetFileFragmentDataQueue();

		while (!mDownloadComplete) {
			RemoteFileFragmentMetadata fragmentMetadata;
			bool hasData = fragmentQueue.Dequeue(out fragmentMetadata);

			if (!hasData || fragmentMetadata == null) {
				fragmentQueue.Enqueue(fragmentMetadata);
				Thread.Sleep(16);
				continue;
			}

			if (fragmentMetadata.IsReadyForHeadRequest()) {
				BeginHeadRequest(fragmentMetadata);
				fragmentQueue.Enqueue(fragmentMetadata);
			}
			else if (m_RemoteFileMetadata.AreHeadRequestsCompleted() &&
					 fragmentMetadata.IsReadyForDownload()) {
				BeginDownloadRequest(fragmentMetadata);
				fragmentQueue.Enqueue(fragmentMetadata);
			}
			else if (fragmentMetadata.IsFinishedDownloading()) {
				OnDownloadFinish(fragmentMetadata);
			} else {
				fragmentQueue.Enqueue(fragmentMetadata);
			}

			Thread.Sleep(24);
		}
		Debug.Log("Worker thread completed");
	}

    #endregion

    #region Head Request

    private void BeginHeadRequest(RemoteFileFragmentMetadata fragmentMetadata) {
		HTTPRequest headRequest = new HTTPRequest(fragmentMetadata.uri, 
												  HTTPMethods.Head, 
												  OnHeadRequestComplete);
		fragmentMetadata.BeginHeadRequest();
		DoHeadRequest(headRequest);
	}

	private void DoHeadRequest(HTTPRequest request) {
		Debug.Log("Sending HEAD request to " + request.Uri.AbsoluteUri);
		request.Send();
	}

	private void OnHeadRequestComplete(HTTPRequest request, HTTPResponse response) {
		if (response == null) {
			OnHeadRequestError(request);
			return;
		}

		RemoteFileFragmentMetadata fragment =
			m_RemoteFileMetadata.GetFileFragmentDataFromUri(request.Uri.AbsoluteUri);
		fragment.ProcessHeadResponse(response);

		Debug.Log("Head request is complete. Status code: " + response.StatusCode);

		if (m_RemoteFileMetadata.AreHeadRequestsCompleted()) { OnAllHeadRequestsComplete(); }
	}

	private void OnAllHeadRequestsComplete() {
		// Memory
		mContentSize = m_RemoteFileMetadata.GetMemorySize();
		AllocateMemory();
    }

	private void OnHeadRequestError(HTTPRequest request) {
		Debug.LogError("Error making HEAD request: " + request.Exception.Message);
		BeginHeadRequest(m_RemoteFileMetadata.GetFileFragmentDataFromUri(request.Uri.AbsoluteUri));
	}

	#endregion

	#region Video Download

	private void BeginDownloadRequest(RemoteFileFragmentMetadata fragmentMetadata) {
		// Request
		HTTPRequest request =
			PreparePartialDownloadRequest(fragmentMetadata.uri.AbsoluteUri);
		fragmentMetadata.BeginDownload();
		SendDownloadRequest(request);
	}

	// Send the request
	private void SendDownloadRequest(HTTPRequest request) {
		Debug.Log("Sending HTTP Request to " + request.Uri.AbsoluteUri);
		request.Send();
	}

	private void OnDownloadProcessing(HTTPRequest request, HTTPResponse response) {
		Debug.Assert(response != null);
		Debug.Log("Processing request from " + request.Uri.AbsoluteUri);
		RemoteFileFragmentMetadata fragmentMetadata =
			m_RemoteFileMetadata.GetFileFragmentDataFromUri(request.Uri.AbsoluteUri);
		Debug.Log("Fragment index: " + fragmentMetadata.index);

		lock (fragmentMetadata) {
			if (!response.HasStreamedFragments()) { return; }
			List<byte[]> fragmentBytesList = response.GetStreamedFragments();

			//if (lastDownloadedURI == request.Uri.AbsoluteUri) {
			//	Debug.LogWarning("Download callback called twice in a row -- ignoring the second one");
			//	return;
			//}
			if (fragmentBytesList == null) { return; }

			for (int i = 0; i < fragmentBytesList.Count; ++i) {
				byte[] fragmentBytes = fragmentBytesList[i];
				int offset =
					m_RemoteFileMetadata.GetFileFragmentMemoryOffset(fragmentMetadata)
					+ (int) fragmentMetadata.GetDownloadedBytes();
				int length = fragmentBytes.Length;

				Debug.Log("Copying " + length + " bytes at offset " + offset);
				CopyDownloadedFragment(offset, length, ref fragmentBytes);
				fragmentMetadata.AddDownloadedBytes(length);
				Interlocked.Add(ref mDownloadedContentSize, length);
				Debug.Log("Downloaded content size: " + mDownloadedContentSize + " / " + mContentSize);
			}

			fragmentMetadata.ProcessGetResponse(response);
		}
		lastDownloadedURI = request.Uri.AbsoluteUri; // for error checking
	}

    private void OnDownloadFinish(RemoteFileFragmentMetadata fragment) {
		Debug.Log("On request finish");

		OnDownloadComplete();

		if (AreDownloadsFinished()) {
			//mDownloadComplete = true;
			OnAllDownloadsFinished();
		}
	}

	// copy stuff into memory
	private void OnAllDownloadsFinished() {
		Debug.Log("Time elapsed (ms): " + mStopwatch.Elapsed);
		Debug.Log("Consecutive bytes: " + GetConsecutiveBytesCount());
        mDownloadComplete = true;
	}

	private void OnDownloadComplete() { Interlocked.Increment(ref mCompletedDownloadsCount); }

    #endregion

    #region Memory & State

	private void AllocateMemory() {
		Debug.Assert(mContentSize > 1024); // just a sanity check for the content size; 1KB is arbitrary
		mDownloadedData = new byte[mContentSize]; // Allocate memory
		mConsecutiveBytesCount = 0;
	}

    private void CopyDownloadedFragment(int offset, int length, ref byte[] data) {
		lock (mDownloadedData) {
			Buffer.BlockCopy(data, 0, mDownloadedData, offset, length);
		}
	}

	#endregion

    #region Helpers

    private void Reset() {
		mContentSize = 0;
		mDownloadedContentSize = 0;
		mCompletedDownloadsCount = 0;
		mConnectionCount = 1;
		mThreadCount = 1;
		mDownloadComplete = false;

		// Data
		mDownloadedData = null;
        m_RemoteFileMetadata = null;
		mConsecutiveBytesCount = 0;

		// Other
		mStopwatch = new Stopwatch();
	}

	private HTTPRequest PreparePartialDownloadRequest(string requestUriString) {
		Uri requestUri = new Uri(requestUriString);
		HTTPRequest partialRequest = new HTTPRequest(requestUri) {
			UseStreaming = true,
			StreamFragmentSize = GetFragmentSize(),
			DisableCache = true,
			Callback = OnDownloadProcessing
        };

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

	private void SetThreadCount(int newThreadCount) {
		Debug.Assert(newThreadCount > 0);
		if (newThreadCount <= 0) { return; }
		mThreadCount = newThreadCount;
	}

	private int GetThreadCount() {
		Debug.Assert(mThreadCount > 0);
		if (mThreadCount <= 0) { SetThreadCount(1); } // ensure valid output
		return mThreadCount;
	}

	public int GetConsecutiveBytesCount() {
		return mConsecutiveBytesCount;
	}


	private bool AreDownloadsFinished() {
		bool downloadsComplete = GetCompletedDownloadsCount() >=
								 m_RemoteFileMetadata.GetFileFragments().Count;
		return downloadsComplete;
	}

	private int GetFragmentSize() { return mFragmentSize; }

	private int GetCompletedDownloadsCount() { return mCompletedDownloadsCount; }
	private void ResetCompletedDownloadsCount() { mCompletedDownloadsCount = 0; }

	#endregion
}

public class RemoteFileMetadata {
	private List<RemoteFileFragmentMetadata> m_FileData =
		new List<RemoteFileFragmentMetadata>();

	private ConcurrentQueue<RemoteFileFragmentMetadata> m_FileDataQueue;

	public RemoteFileMetadata AddFileFragmentData(RemoteFileFragmentMetadata metadata) {
		m_FileData.Add(metadata);
		Sort();
		return this;
	}

	public RemoteFileMetadata AddFileFragmentData(Uri uri, uint index) {
		RemoteFileFragmentMetadata metadata = new RemoteFileFragmentMetadata(uri, index);
		m_FileData.Add(metadata);
		Sort();
		return this;
	}

	public bool IsValid() {
		for (int i = 0; i < GetFileFragments().Count; i++) {
			RemoteFileFragmentMetadata fragment = GetFileFragments()[i];
			if (fragment == null) {
				Debug.LogWarning("FileFragmentData is NULL");
				return false;
			}

			if (fragment.index != i) {
				Debug.LogWarning("FileFragmentData index is " + fragment.index +
								 " but should be " + i);
				return false;
			}
		}

		return true;
	}

	public bool IsFinishedDownloading() {
		for (int i = 0; i < GetFileFragments().Count; ++i) {
			RemoteFileFragmentMetadata fragment = GetFileFragments()[i];
			if (!fragment.IsFinishedDownloading()) { return false; }
		}

		return true;
	}

	public bool AreHeadRequestsCompleted() {
		for (int i = 0; i < GetFileFragments().Count; ++i) {
			RemoteFileFragmentMetadata fragment = GetFileFragments()[i];
			if (!(fragment.IsReadyForDownload() ||
				fragment.IsDownloading() ||
				fragment.IsFinishedDownloading())) { return false; }
		}

		return true;
	}

	public RemoteFileFragmentMetadata GetFileFragmentDataFromUri(string absoluteUri) {
		for (int i = 0; i < GetFileFragments().Count; ++i) {
			RemoteFileFragmentMetadata fragment = GetFileFragments()[i];
			if (fragment.uri.AbsoluteUri == absoluteUri) { return fragment; }
		}

		return null;
	}

	/// <summary>
	/// Get this fragment's offset in the buffer. Previous fragments must finish downloading though to make their
	/// content length known (in chunked encoding) though
	/// </summary>
	/// <param name="fragment"></param>
	/// <returns>The offset in memory to which this fragment should be copied to</returns>
	public int GetFileFragmentMemoryOffset(RemoteFileFragmentMetadata fragment) {
		int offset = 0;
		for (int i = 0; i < GetFileFragments().Count; i++) {
			RemoteFileFragmentMetadata currentFragment = GetFileFragments()[i];

			if (currentFragment != fragment) {
				offset += (int) currentFragment.GetSize();
			} else { break; }
		}

		return offset;
	}

	public int GetMemorySize() {
		int bytes = 0;
		for (int i = 0; i < GetFileFragments().Count; ++i) {
			bytes += (int) GetFileFragments()[i].GetSize();
		}

		return bytes;
	}

	/// <summary>
	/// Creates a concurrent queue from the list of fragments.
	/// Only call this after all the fragments have been added to the list
	/// </summary>
	/// <returns></returns>
	public ConcurrentQueue<RemoteFileFragmentMetadata> GetFileFragmentDataQueue() {
		if (m_FileDataQueue == null) {
			m_FileDataQueue = new ConcurrentQueue<RemoteFileFragmentMetadata>();

			for (int i = 0; i < GetFileFragments().Count; ++i) {
				m_FileDataQueue.Enqueue(GetFileFragments()[i]);
			}
		}

		return m_FileDataQueue;
	}

	private void Sort() {
		GetFileFragments().Sort((x, y) => (x.index.CompareTo(y.index)));
	}

	public List<RemoteFileFragmentMetadata> GetFileFragments() { return m_FileData; }
}

public class RemoteFileFragmentMetadata {
	private enum FileFragmentDataState {
		ReadyForHeadRequest,
		HeadRequest,
		ReadyForDownload,
		Downloading,
		Finished
	};

	public Uri uri;
	public uint index;
	private uint size = 0; // in bytes
	private uint downloadedBytes = 0;
	private FileFragmentDataState state = FileFragmentDataState.ReadyForHeadRequest;


	public RemoteFileFragmentMetadata(Uri _uri, uint _index) {
		uri = _uri;
		index = _index;
	}

	public void ProcessHeadResponse(HTTPResponse response) {
		Debug.Assert(response != null, this);
		size = Convert.ToUInt32(response.GetFirstHeaderValue("content-length"));
		state = FileFragmentDataState.ReadyForDownload;
	}

	public void ProcessGetResponse(HTTPResponse response) {
		if (response.IsStreamingFinished) { state = FileFragmentDataState.Finished; }
	}

	public void AddDownloadedBytes(int size) { downloadedBytes += (uint) size; }

	public uint GetDownloadedBytes() { return downloadedBytes; }

	public uint GetSize() { return size; }

	public void BeginHeadRequest() { state = FileFragmentDataState.HeadRequest; }

	public void BeginDownload() { state = FileFragmentDataState.Downloading; }

	public bool IsReadyForHeadRequest() {
		return state == FileFragmentDataState.ReadyForHeadRequest;
	}

	public bool IsReadyForDownload() {
		return state == FileFragmentDataState.ReadyForDownload;
	}

	public bool IsDownloading() { return state == FileFragmentDataState.Downloading; }

	public bool IsFinishedDownloading() {
		return state == FileFragmentDataState.Finished;
	}

}
