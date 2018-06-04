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
using UnityEngine.EventSystems;
using AndroidLib.Utils;
using System.Threading.Tasks;
using System.Threading;
using Enums;
using System.Linq;

[System.Serializable]
public class ConnectedDevices
{
    public string DeviceID;
    public string DeviceName;
    public DeviceState deviceState = DeviceState.Unknown;
}

public class ADBManager : MonoBehaviour
{

    #region Variables 

    public static ADBManager Instance;

    public delegate void OnConnectedDeviceCompletion();
    public static event OnConnectedDeviceCompletion OnConnectedDevice;

    public delegate void OnAPKInstallSuccess(int Index);
    public static event OnAPKInstallSuccess OnAPKSuccess;

    public delegate void OnAPKInstallFailure(int Index, string Error, InstallErrorType ErrorType);
    public static event OnAPKInstallFailure OnAPKFailure;

    public delegate void OnAPKUnInstallSuccess(int Index);
    public static event OnAPKUnInstallSuccess OnAPKUninstallSuccess;

    public delegate void OnAPKUnInstallFailure(int Index, string Error, UninstallErrorType ErrorType);
    public static event OnAPKUnInstallFailure OnAPKUninstallFailure;

    public delegate void OnFinallyCalled(Thread CurrentThread);
    public static event OnFinallyCalled OnFinallyCalledForAbort;

    public List<ConnectedDevices> connectedDevices = new List<ConnectedDevices>();

    Process CurrActiveProcess = new Process();

    List<string> lines = new List<string>();

    int CurrDeviceIndex;

    string CommandProcessName = "cmd";
    string ADBProcessName = "adb";

    #endregion

    #region StartUpdate

    // Use this for initialization
    void Start()
    {
        Instance = this;
    }

    #endregion

    #region Connected Device

    internal void GetConnectedDevices()
    {
        connectedDevices.Clear();
        lines.Clear();
        Process proc = null;
        try
        {
            using (proc = new Process())
            {

                CurrActiveProcess = proc;
                System.Diagnostics.ProcessStartInfo procStartInfo = new System.Diagnostics.ProcessStartInfo(CommandProcessName + ".exe");
                procStartInfo.RedirectStandardInput = true;
                procStartInfo.RedirectStandardOutput = true;
                procStartInfo.RedirectStandardError = true;
                procStartInfo.UseShellExecute = false;
                procStartInfo.CreateNoWindow = true;
                proc.StartInfo = procStartInfo;
                proc.ErrorDataReceived += new DataReceivedEventHandler(ErrorDataHandlerForConnectedDevice);

                proc.Start();
                //print(proc.Id);
                proc.StandardInput.WriteLine("adb devices");
                proc.StandardInput.WriteLine("exit");

                for (int i = 0; !proc.StandardOutput.EndOfStream; i++)
                {
                    string line = proc.StandardOutput.ReadLine();
                    lines.Add(line);
                    //print (line);
                }

                proc.Close();

                if (lines.Count > 8)
                {

                    for (int i = 0; i < lines.Count - 8; i++)
                    {
                        DeviceState state = IsDeviceAuthorized(lines[i + 5]);
                        if (state == DeviceState.Online)
                        {
                            ConnectedDevices NewDevice = new ConnectedDevices();
                            NewDevice.DeviceID = ExtractDeviceId(lines[i + 5]);
                            NewDevice.deviceState = state;


                            proc.Start();
                            proc.StandardInput.WriteLine("adb -s " + NewDevice.DeviceID + " shell getprop ro.product.model && exit");

                            string deviceName = "";
                            //proc.BeginErrorReadLine ();

                            string Line;
                            int Counter = 0;
                            while ((Line = proc.StandardOutput.ReadLine()) != null)
                            {
                                if (Line.Length > 2 && !Line.Contains("exit") && !Line.Contains("Microsoft"))
                                {
                                    deviceName = Line;
                                }
                            }

                            NewDevice.DeviceName = deviceName;

                            connectedDevices.Add(NewDevice);

                            proc.WaitForExit();
                        }
                    }
                    proc.Close();
                }
                OnConnectedDevice();
            }
        }
        catch (System.Exception e)
        {
            UnityEngine.Debug.Log(e.ToString());
        }
        finally
        {
            OnFinallyCalledForAbort(Thread.CurrentThread);
            KillProcess(proc.Id);
        }
    }

    private void ErrorDataHandlerForConnectedDevice(object sender, DataReceivedEventArgs args)
    {
        string message = args.Data;
        UnityEngine.Debug.LogError("ErrorDataHandlerForConnectedDevice " + message);
    }

    DeviceState IsDeviceAuthorized(string line)
    {
        //print (line);
        if (line.IndexOf("device") > 0)
            return DeviceState.Online;

        if (line.IndexOf("unauthorized") > 0)
            return DeviceState.Unauthorized;

        if (line.IndexOf("offline") > 0)
            return DeviceState.Offline;

        return DeviceState.Online;
    }

    string ExtractDeviceId(string line)
    {
        string str = "";

        if (line.IndexOf("device") > 0)
            str = line.Substring(0, line.IndexOf("device") - 1);
        if (line.IndexOf("unauthorized") > 0)
            str = line.Substring(0, line.IndexOf("unauthorized") - 1);

        if (line.IndexOf("offline") > 0)
            str = line.Substring(0, line.IndexOf("offline") - 1);

        return str;
    }

    public bool IsDeviceConnected(string deviceID)
    {

        if (connectedDevices.Count == 0) return false;

        foreach (ConnectedDevices dev in connectedDevices)
        {
            if (dev.DeviceID.Equals(deviceID)) return true;
        }

        return false;
    }

    public List<bool> IsDevicesConnected(List<string> deviceIDs)
    {
        //		print ("connected devices count " + connectedDevices.Count);
        if (connectedDevices.Count == 0) return null;
        List<bool> devicesConnected = new List<bool>();

        foreach (string str in deviceIDs)
        {
            if (connectedDevices.Exists(Device => Device.DeviceID == str) &&
                connectedDevices.Exists(Device => Device.deviceState == DeviceState.Online))
                devicesConnected.Add(true);
            else
                devicesConnected.Add(false);
            //			print ("Connected " + str);
        }
        return devicesConnected;
    }

    #endregion


    #region List packages in Device

    public string ListPackages(string deviceId)
    {
        string Output = "";
        using (Process proc = new Process())
        {
            System.Diagnostics.ProcessStartInfo procStartInfo = new System.Diagnostics.ProcessStartInfo("cmd.exe");
            procStartInfo.RedirectStandardInput = true;
            procStartInfo.RedirectStandardOutput = true;
            procStartInfo.RedirectStandardError = true;
            procStartInfo.UseShellExecute = false;
            procStartInfo.CreateNoWindow = true;
            proc.StartInfo = procStartInfo;


            proc.Start();
            proc.StandardInput.WriteLine("adb -s " + deviceId + " shell pm list packages && exit");
            //			proc.StandardInput.WriteLine("exit");

            for (int i = 0; !proc.StandardOutput.EndOfStream; i++)
            {
                string line = proc.StandardOutput.ReadLine();
                if (!line.Contains("exit") && !line.Contains("Microsoft"))
                    Output += ScrapSubstring(line, "package:") + "\n";
            }
        }

        return Output;
    }

    string ScrapSubstring(string original, string substring)
    {
        int index = original.IndexOf(substring);
        string cleanPath = (index < 0)
            ? original
            : original.Remove(index, substring.Length);
        return cleanPath;
    }

    #endregion


    #region Install Apk to Device




    internal void InstallApkToOneDevice(int deviceIndex, int StartInstallationFrom)
    {
        Process proc = null;
        try
        {
            AppManager.Instance.installationThreadStarted = true;
            for (int j = 0;
                j < UtilityManager.GetIntervalCount(AppManager.Instance.apkPaths.Count, StaticData.Instance.IntervalPeriodForAPKAtOneTime); j++)
            {

                int[] MinMax = UtilityManager.GetMinMaxInInterval(AppManager.Instance.apkPaths.Count, StaticData.Instance.IntervalPeriodForAPKAtOneTime, j);

                using (proc = new Process())
                {
                    CurrActiveProcess = proc;
                    System.Diagnostics.ProcessStartInfo procStartInfo = new System.Diagnostics.ProcessStartInfo(CommandProcessName + ".exe");
                    procStartInfo.RedirectStandardInput = true;
                    procStartInfo.RedirectStandardOutput = true;
                    procStartInfo.RedirectStandardError = true;
                    procStartInfo.UseShellExecute = false;
                    procStartInfo.CreateNoWindow = true;
                    proc.StartInfo = procStartInfo;
                    proc.ErrorDataReceived += (object _sender, DataReceivedEventArgs _args) =>
                      ErrorDataHandler(deviceIndex, _sender, _args);

                    proc.Start();

                    for (int i = MinMax[0]; i < MinMax[1]; i++)
                    {
                        if (i < StartInstallationFrom)
                            continue;

                        print(i + "adb -s " +
                            AppManager.Instance.deviceDetails[deviceIndex].DeviceID + " install -r " + AppManager.Instance.apkPaths[i]);

                        proc.StandardInput.WriteLine("adb -s " +
                            AppManager.Instance.deviceDetails[deviceIndex].DeviceID + " install -r " + AppManager.Instance.apkPaths[i]);
                    }

                    proc.StandardInput.WriteLine("exit");
                    proc.BeginErrorReadLine();

                    string Line;
                    while ((Line = proc.StandardOutput.ReadLine()) != null)
                    {
                        //UnityEngine.Debug.Log (Line);

                        if (Line.Contains("Failure") || Line.Contains("error:"))
                        {
                            UnityEngine.Debug.LogError(Line);

                            //							if(Line.Contains("not found"))

                            if (Line.Contains("no response: Connection reset by peer")
                                || Line.Contains("not found") || Line.Contains("couldn't read from device") || Line.Contains("waiting for device"))
                            {
                                ErrorDataHandler(deviceIndex, Line);
                                return;
                            }

                            ErrorDataHandler(deviceIndex, Line);

                        }

                        if (Line.Equals("Success"))
                        {
                            OnAPKSuccess(deviceIndex);
                        }

                    }

                    proc.WaitForExit();
                }
            }
        }
        catch (System.Exception e)
        {
            UnityEngine.Debug.Log(deviceIndex + " " + e.ToString());
        }
        finally
        {
            OnFinallyCalledForAbort(Thread.CurrentThread);
            KillProcess(proc.Id);
        }
    }

    private void ErrorDataHandler(int deviceIndex, object sender, DataReceivedEventArgs args)
    {
        string message = args.Data;
        ErrorDataHandler(deviceIndex, message);
    }

    private void ErrorDataHandler(int deviceIndex, string message)
    {
        if (message.Contains("exit"))
        {
            return;
        }

        Enums.InstallErrorType ErrorType = InstallErrorType.UNKNOWN;

        if (message.Contains("couldn't read from device") || message.Contains("waiting for device") ||
            message.Contains("no response: Connection reset by peer") || message.Contains("not found"))
            ErrorType = InstallErrorType.DEVICE_NOT_FOUND;

        if (message.Contains("INSTALL_FAILED_UPDATE_INCOMPATIBLE"))
            ErrorType = InstallErrorType.INSTALL_FAILED_UPDATE_INCOMPATIBLE;

        if (message.Contains("INSTALL_FAILED_INSUFFICIENT_STORAGE"))
            ErrorType = InstallErrorType.INSTALL_FAILED_INSUFFICIENT_STORAGE;
        if (message.Contains("INSTALL_FAILED_CONTAINER_ERROR"))
            ErrorType = InstallErrorType.INSTALL_FAILED_CONTAINER_ERROR;
        if (message.Contains("INSTALL_FAILED_OLDER_SDK"))
            ErrorType = InstallErrorType.INSTALL_FAILED_OLDER_SDK;

        OnAPKFailure(deviceIndex, message, ErrorType);
    }

    #endregion

    #region Uninstall

    internal void UninstallApkFromOneDevice(int deviceIndex, int startUninstallationFrom)
    {
        Process proc = null;
        List<App> selectedApks = UninstallManager.instance.selectedDeviceInfoList[deviceIndex].selectedApkList;
        try
        {
            AppManager.Instance.uninstallThreadStarted = true;


            print(selectedApks.Count + " " + StaticData.Instance.IntervalPeriodForAPKAtOneTime + " " + UtilityManager.GetIntervalCount(selectedApks.Count, StaticData.Instance.IntervalPeriodForAPKAtOneTime));
            for (int j = 0;
                j < UtilityManager.GetIntervalCount(selectedApks.Count, StaticData.Instance.IntervalPeriodForAPKAtOneTime); j++)
            {
                print("UninstallApkFromOneDevice " + j);

                int[] MinMax = UtilityManager.GetMinMaxInInterval(selectedApks.Count, StaticData.Instance.IntervalPeriodForAPKAtOneTime, j);

                using (proc = new Process())
                {
                    CurrActiveProcess = proc;
                    System.Diagnostics.ProcessStartInfo procStartInfo = new System.Diagnostics.ProcessStartInfo(CommandProcessName + ".exe");
                    procStartInfo.RedirectStandardInput = true;
                    procStartInfo.RedirectStandardOutput = true;
                    procStartInfo.RedirectStandardError = true;
                    procStartInfo.UseShellExecute = false;
                    procStartInfo.CreateNoWindow = true;
                    proc.StartInfo = procStartInfo;
                    proc.ErrorDataReceived += (object _sender, DataReceivedEventArgs _args) =>
                  ErrorDataHandlerUninstall(deviceIndex, _sender, _args);

                    proc.Start();

                    for (int i = MinMax[0]; i < MinMax[1]; i++)
                    {
                        if (i < startUninstallationFrom)
                            continue;

                        print(i + " adb -s " +
                            UninstallManager.instance.selectedDeviceInfoList[deviceIndex].DeviceID + " uninstall " + selectedApks[i].PackageID);

                        proc.StandardInput.WriteLine("adb -s " +
                            UninstallManager.instance.selectedDeviceInfoList[deviceIndex].DeviceID + " uninstall " + selectedApks[i].PackageID);
                    }

                    proc.StandardInput.WriteLine("exit");
                    proc.BeginErrorReadLine();

                    string Line;
                    while ((Line = proc.StandardOutput.ReadLine()) != null)
                    {
                        UnityEngine.Debug.Log(Line);

                        if (Line.Contains("Failure") || Line.Contains("error:") || Line.Contains("waiting for device"))
                        {
                            UnityEngine.Debug.LogError(Line);

                            if (Line.Contains("no response: Connection reset by peer")
                                || Line.Contains("not found") || Line.Contains("couldn't read from device") || Line.Contains("waiting for device"))
                            {
                                ErrorDataHandlerUninstall(deviceIndex, Line);
                                return;
                            }

                            ErrorDataHandlerUninstall(deviceIndex, Line);
                        }

                        if (Line.Equals("Success"))
                        {
                            OnAPKUninstallSuccess(deviceIndex);
                        }
                    }

                    proc.WaitForExit();
                }
            }
        }
        catch (System.Exception e)
        {
            UnityEngine.Debug.Log(deviceIndex + " " + e.ToString());
        }
        finally
        {
            OnFinallyCalledForAbort(Thread.CurrentThread);
            KillProcess(proc.Id);
        }
    }

    private void ErrorDataHandlerUninstall(int deviceIndex, object sender, DataReceivedEventArgs args)
    {
        string message = args.Data;
        //		if (message.Equals ("") || message.Equals (" ") || message == null)
        //			return;
        ErrorDataHandlerUninstall(deviceIndex, message);
    }

    private void ErrorDataHandlerUninstall(int deviceIndex, string message)
    {

        if (message.Contains("exit"))
        {
            return;
        }


        Enums.UninstallErrorType ErrorType = UninstallErrorType.UNKNOWN;

        if (message.Contains("couldn't read from device") || message.Contains("waiting for device") ||
           message.Contains("no response: Connection reset by peer") || message.Contains("not found"))
            ErrorType = UninstallErrorType.DEVICE_NOT_FOUND;
        else if (message.Contains("DELETE_FAILED_INTERNAL_ERROR"))
            ErrorType = UninstallErrorType.DELETE_FAILED_INTERNAL_ERROR;

        OnAPKUninstallFailure(deviceIndex, message, ErrorType);
    }


    #endregion

    #region StartProcess 


    void OnApplicationQuit()
    {
        KillProcess(CommandProcessName, true);
        KillProcess(ADBProcessName, true);
    }

    public static bool IsProcessRunning(string processName)
    {
        Process[] processes = Process.GetProcessesByName(processName);
        return processes.Length > 0;
    }

    public static void KillProcess(string processName, bool All)
    {
        Process[] processes = Process.GetProcessesByName(processName);
        for (int i = 0; i < processes.Length; i++)
        {
            Process p = processes[i];
            if (p.ProcessName.ToLower().Contains(processName.ToLower()))
            {
                print(p.ProcessName);
                p.Kill();
                if (!All)
                    return;
            }
        }
    }

    public static void KillProcess(int processID)
    {
        print(processID);
        Process processe = Process.GetProcessById(processID);
        print(processe.Id);
        print(processe.ProcessName);
        processe.Kill();
    }



    #endregion

}
