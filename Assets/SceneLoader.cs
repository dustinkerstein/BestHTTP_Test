using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class SceneLoader : MonoBehaviour {

	public void LoadTest1() {
		SceneManager.LoadScene ("DownloadSpeedTest_Test1");
	}

	public void LoadTest2() {
		SceneManager.LoadScene ("DownloadSpeedTest_Test2");
	}

	public void LoadTest3() {
		SceneManager.LoadScene ("DownloadSpeedTest_Test3");
	}

	public void LoadTest4() {
		SceneManager.LoadScene ("DownloadSpeedTest_Test4");
	}

	public void LoadSceneLoader() {
		SceneManager.LoadScene ("SceneLoader");
	}

}
