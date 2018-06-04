using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using Crosstales.FB;
using System.IO;
using System.Management;
using System.Diagnostics;
using System.Collections;
using UnityEngine.Collections;
using System.Threading.Tasks;
using System.Threading;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using Enums;
using System.Linq;

public class AppManager : MonoBehaviour
{
    #region Variables
    public static AppManager Instance;

	public CurrentAppState AppState = CurrentAppState.APKSelection;

    public GameObject DummyApkRow, DummyDeviceRow;
    public GameObject DeviceListTableView, ApkListTableView;
    public InputField folderPath;

    public Image[] usbRowImages;

    public Text outputText;

    public List<DeviceDetails> deviceDetails = new List<DeviceDetails>();

    internal List<Thread> ProcessThreads = new List<Thread>();

    internal List<string> apkNames = new List<string>();
    public List<string> apkPaths = new List<string>();

    internal bool installationThreadStarted,uninstallThreadStarted = false;

    internal int OverallNumberofInstallations = 0;
    internal int TotalAPKPerDevice = 0;

	int connectedDevicesCount = 0;

    List<string> apkPathsCache = new List<string>();
    List<Text> usbRows = new List<Text>();
    List<Text> apkRows = new List<Text>();

    public List<ApkData> apkDataList;

    string path = "E:/Builds/2017.apk";

    string apkPath = "";

    int numberOfApkSelected = 0;

    Color unSetTabColor = new Color32(197, 197, 197, 255);
    Color setTabColor = new Color32(255, 255, 255, 255);

    #endregion


    #region Public methods

    void Awake() {
        Instance = this;
    }

	void Init()
	{
		connectedDevicesCount = ADBManager.Instance.connectedDevices.Count;
	}

    void OnEnable()
    {
        ADBManager.OnConnectedDevice += ADBManager_OnConnectedDevicesList; ;
        ADBManager.OnFinallyCalledForAbort += OnThreadAbort; ;
    }

    void OnDisable()
    {
        ADBManager.OnConnectedDevice -= ADBManager_OnConnectedDevicesList;
        ADBManager.OnFinallyCalledForAbort -= OnThreadAbort; ;
    }

    #endregion

    #region SelectAPKFolder

    public void SelectRow(int id)
    {
        for (int i = 0; i < usbRows.Count; i++)
        {
            usbRowImages[i].color = Color.white;
            usbRows[i].color = Color.black;
        }
        usbRowImages[id].color = new Color32(38, 129, 255, 255);
        usbRows[id].color = Color.white;
        ListPackages(deviceDetails[id].DeviceID);
    }

    public void ListPackages(string deviceId)
    {
        outputText.text = ADBManager.Instance.ListPackages(deviceId);
    }


	List<string> apkFilePaths = new List<string>();
	public void SelectApkFolder()
	{
		string path = FileBrowser.OpenSingleFolder("Open Folder");
		folderPath.text = path;
		apkFilePaths.Clear ();
		AddApkFilesFromAFolder (path);
	}

	public void AddApkFilesFromAFolder(string path)
    {

        if (path != "")
        {
			apkFilePaths.AddRange (System.IO.Directory.GetFiles (path));

			string[] subDirectories = System.IO.Directory.GetDirectories (path);

			if (subDirectories.Length > 0) {
				foreach (var item in subDirectories) {
					AddApkFilesFromAFolder (item);
				}
			}
			else
				rebuildList(apkFilePaths.ToArray());

        }
    }

    public void DisplayApkList(InputField input)
    {
        UnityEngine.Debug.Log("display list " + input.text);
        if (Directory.Exists(input.text))
            rebuildList(System.IO.Directory.GetFiles(path));
    }


	public void ChangeJSONFIle() {
		//Debug.Log("OpenSingleFile");


		var extensions = new[] {
			new ExtensionFilter("JSON File", "json"),

		};


		//string extensions = "";

		string newJosnFile = FileBrowser.OpenSingleFile("Open File", "", extensions);
		//jsonFolderPath.text = newJosnFile;
		//print (newJosnFile);

		//string fileToBeReplaced = "C:/Users/harsh.priyadarshi/Desktop/curriculam-modules.json";
		string fileToBeReplaced = Application.streamingAssetsPath + "/curriculum-modules.json";
		//print (fileToBeReplaced);

		//string backupFilie = "C:/Users/harsh.priyadarshi/Desktop/curriculam-modules-bac.json";
		string backupFilie = Application.streamingAssetsPath + "/curriculam-modules-bac.json";
		//debugText.text = Application.streamingAssetsPath;
		//print (backupFilie);

		System.IO.File.Copy(newJosnFile, fileToBeReplaced, true);
	}

    public void SaveFile()
    {
        string extensions = "txt";
        string path = FileBrowser.SaveFile("Save File", "", "MySaveFile", extensions);

        rebuildList(path);
    }

    #endregion

    public void RefreshConnectedDevicesList()
    {
        AppState = CurrentAppState.DeviceSelection;
        ClearConnectedDeviceVariables();

        UIManager.Instance.DeviceSelectionToggleAll.gameObject.SetActive(false);
        UIManager.Instance.ConnectDeviceResetBtn.SetActive(false);
        UIManager.Instance.DeviceLoadingPanel.SetActive(true);
        UIManager.Instance.NoDevicePanel.SetActive(false);

        ADBManager adbManager = ADBManager.Instance;

        ProcessThreads.Add(new Thread(() => adbManager.GetConnectedDevices()));
        ProcessThreads[ProcessThreads.Count - 1].Start();

        Thread.Sleep(1);
    }

    void ADBManager_OnConnectedDevicesList()
    {
        //print ( "ADBManager_OnConnectedDevicesList" );
        UnityMainThreadDispatcher.Instance().Enqueue(() => OnConnectedDeviceCompletionOnMainThread());
    }

    void OnConnectedDeviceCompletionOnMainThread()
    {

        if (AppState == CurrentAppState.DeviceSelection)
        {
            UIManager.Instance.ConnectDeviceResetBtn.SetActive(true);
            UIManager.Instance.DeviceLoadingPanel.SetActive(false);

            if (ADBManager.Instance.connectedDevices.Count <= 0)
                UIManager.Instance.NoDevicePanel.SetActive(true);
            if (ADBManager.Instance.connectedDevices.Count > 1)
                UIManager.Instance.DeviceSelectionToggleAll.gameObject.SetActive(true);

            for (int i = 0; i < ADBManager.Instance.connectedDevices.Count; i++)
            {

                GameObject newDeviceRow = GameObject.Instantiate(DummyDeviceRow, DeviceListTableView.transform);

                deviceDetails.Add(newDeviceRow.GetComponent<DeviceDetails>());

                deviceDetails[i].Init(ADBManager.Instance.connectedDevices[i].DeviceID,
                    ADBManager.Instance.connectedDevices[i].DeviceName, apkPaths.Count);
            }
        }
    }

    void ClearConnectedDeviceVariables()
    {
        for (int i = 0; i < deviceDetails.Count; i++)
        {
            Destroy(deviceDetails[i].gameObject);
        }
        deviceDetails.Clear();
    }

    public void ActivateConnectedDeviceNext()
    {
        for (int i = 0; i < deviceDetails.Count; i++)
        {
            if (deviceDetails[i].toggle.isOn)
            {
                UIManager.Instance.connectDeviceNext.interactable = true;
                return;
            }
        }
        UIManager.Instance.connectDeviceNext.interactable = false;
    }

    public void InstallApk()
    {
        AppState = CurrentAppState.Installation;
        OverallNumberofInstallations = deviceDetails.Count * apkPaths.Count;

        InstallationManager.Instance.Init();
        Thread th;
        for (int i = 0; i < deviceDetails.Count; i++)
        {
            if (deviceDetails[i].toggle.isOn)
            {
                deviceDetails[i].InitForInstallation();

                installationThreadStarted = false;

                ProcessThreads.Add(th = new Thread(() =>
                    InstallationManager.Instance.StartInstallationForEachDevice(i)
                //ADBManager.Instance.InstallApkToOneDevice(i)
                ));
                th.Start();
                Thread.Sleep(100);

            }
            else
            {
                OverallNumberofInstallations -= (apkPaths.Count);
                deviceDetails[i].gameObject.SetActive(false);
            }
        }

    }

    public void ResumeInstallation(int DeviceOrder, int ResumeFrom)
    {
        Thread th;
        print("ResumeInstallation");
        if (deviceDetails[DeviceOrder].toggle.isOn)
        {

            ProcessThreads.Add(th = new Thread(() =>
                InstallationManager.Instance.ResumeInstallation(DeviceOrder, ResumeFrom)
            //ADBManager.Instance.InstallApkToOneDevice(i)
            ));
            th.Start();
            Thread.Sleep(1);

        }
    }


    internal void OnOverallProcessCompletion()
    {
        AppState = CurrentAppState.Summary;
        installationThreadStarted = false;
        for (int i = 0; i < ProcessThreads.Count; i++)
        {
            if (ProcessThreads[i].IsAlive)
                ProcessThreads[i].Abort();
        }
        ProcessThreads.Clear();

        UIManager.Instance.SummaryButton.SetActive(true);
    }

    public void OpenFilesAsync()
    {
        string extensions = "";
        FileBrowser.OpenFilesAsync("Open Files", "", extensions, true, (string[] paths) => { writePaths(paths); });
    }

    public void OpenFoldersAsync()
    {
        FileBrowser.OpenFoldersAsync("Open Folders", "", true, (string[] paths) => { writePaths(paths); });
    }

    public void SaveFileAsync()
    {
        string extensions = "txt";
        FileBrowser.SaveFileAsync("Save File", "", "MySaveFile", extensions, (string paths) => { writePaths(paths); });
    }

    private void writePaths(params string[] paths)
    {
        rebuildList(paths);
    }

    private void rebuildList(params string[] e)
    {
        AppState = CurrentAppState.APKSelection;

        if (e == null || e.Equals(""))
            return;

        apkPaths.Clear();
        foreach (Text txt in apkRows)
            Destroy(txt.gameObject.transform.parent.gameObject);
        apkRows.Clear();
        apkDataList.Clear();
        for (int ii = 0; ii < e.Length; ii++)
        {
            if (e[ii].ToString().Contains("apk"))
            {
                GameObject go = Instantiate(DummyApkRow, ApkListTableView.transform);
                Text apkFileNameText = go.transform.GetChild(1).GetComponent<Text>();
                Text apkNameText = go.transform.GetChild(2).GetComponent<Text>();
                string APKName = e[ii].ToString();
                int LastIndex = APKName.LastIndexOf('\\') + 1;
                apkFileNameText.text = APKName.Substring(LastIndex, APKName.Length - LastIndex);
                apkNameText.text = FilterManager.instance.ID2Title(apkFileNameText.text.ToString().Replace(".apk", ""));

                apkPaths.Add('"' + APKName + '"');
                apkRows.Add(apkFileNameText);
                apkNames.Add(apkFileNameText.text);

                #region Add ApkData for each APP
                ApkData apkData = go.GetComponent<ApkData>();
                apkData.Path = '"' + APKName + '"';
                apkData.ID = apkFileNameText.text.ToString().Replace(".apk", "").ToLower();
                apkData.Name = FilterManager.instance.ID2Title(apkData.ID);
                #endregion                
            }
        }

        TotalAPKPerDevice = apkPaths.Count;
        OverallNumberofInstallations = deviceDetails.Count * apkPaths.Count;

        StartCoroutine(AfterRebuildList());
        //UnityEngine.Debug.Log("totalNumberOfInstallations " + totalNumberOfInstallations + "deviceIds.Count " + deviceDetails.Count + "apkPahts.Count " + apkPaths.Count);
    }

    IEnumerator AfterRebuildList()
    {
        yield return 0;
        FilterManager.instance.ResetFilter();
    }

    public void UpdateInstallList(List<string> InstallList, List<ApkData> _ApkDataList)
    {
        apkPaths = new List<string>(InstallList);
        apkDataList = new List<ApkData>(_ApkDataList);

        TotalAPKPerDevice = apkPaths.Count;
        OverallNumberofInstallations = deviceDetails.Count * apkPaths.Count;
    }

    internal void OnThreadAbort(Thread th)
    {
        UnityMainThreadDispatcher.Instance().Enqueue(() => RunThreadAbortOnMainThread(th));
    }

    void RunThreadAbortOnMainThread(Thread th)
    {
        try
        {
            if (ProcessThreads.Count > 0 && ProcessThreads.Contains(th))
            {
                //print ("Thread stopped : "+th.ManagedThreadId);
                th.Abort();
                ProcessThreads.Remove(th);
                //UnityEngine.Debug.LogError ("Total Threads Active "+ ProcessThreads.Count);
            }
        }
        catch (System.Exception e)
        {
            print(e.ToString());
        }

    }
}
