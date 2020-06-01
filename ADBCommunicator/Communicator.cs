using ADBCommunicator.Exceptions;
using SharpAdbClient;
using SharpAdbClient.DeviceCommands;
using SharpAdbClient.Exceptions;
using Synergy.Logging;
using Synergy.Logging.Interfaces;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

namespace ADBCommunicator {
	internal class Communicator : IDisposable {
		private static readonly ILogger Logger = new Logger(nameof(Communicator));
		private static readonly Mutex ServerInstanceMutex;
		private static readonly SemaphoreSlim ProcessSync = new SemaphoreSlim(1, 1);
		private static readonly AdbServer Server;

		private readonly DeviceMonitor DeviceMonitor;
		private readonly AdbClient Client;
		private readonly List<DeviceData> ConnectedDevices = new List<DeviceData>();
		internal int DevicesCount => ConnectedDevices.Count;

		static Communicator() {
			string _mutexName = Assembly.GetExecutingAssembly().GetName().Name + "-Mutex";
			ServerInstanceMutex = new Mutex(false, _mutexName);

			Logger.Warning("Trying to acquire server mutex...");
			bool mutexAcquired;

			try {
				mutexAcquired = ServerInstanceMutex.WaitOne(60000);
			}
			catch (AbandonedMutexException) {
				mutexAcquired = true;
			}

			if (!mutexAcquired) {
				Logger.Error("Failed to acquire server mutex.");
				Logger.Error("You might be running multiple instances of the same process.");
				Logger.Error("Running multiple instances can cause unavoidable errors. Exiting now...");
				throw new MultipleInstancesRunningException();
			}

			ServerInstanceMutex.WaitOne();
			Server = new AdbServer();
		}

		internal Communicator() {
			DeviceMonitor = new DeviceMonitor(new AdbSocket(new IPEndPoint(IPAddress.Loopback, AdbClient.AdbServerPort)));
			Client = new AdbClient();
		}

		internal void InitDeviceMonitor() {
			if (!DeviceMonitor.IsRunning) {
				DeviceMonitor.Start();
			}

			DeviceMonitor.DeviceConnected += OnDeviceConnected;
			DeviceMonitor.DeviceDisconnected += OnDeviceDisconnected;
			DeviceMonitor.DeviceChanged += OnDeviceChanged;
		}

		internal static bool StartServer(string? adbPath = null) {
			if (string.IsNullOrEmpty(adbPath) && SearchCurrentDirectoryForADB(out adbPath)) {
				if (string.IsNullOrEmpty(adbPath)) {
					throw new AdbFilesNotFoundException();
				}
			}

			Logger.Trace("ADB files found at " + adbPath + " directory.");
			Logger.Info($"{Constants.ADB_EXE_NAME} found at {adbPath + "/" + Constants.ADB_EXE_NAME}");
			Console.WriteLine(Server.StartServer(adbPath + "/" + Constants.ADB_EXE_NAME, false));
			return true;
		}

		internal void InitClient() => Client.Connect(new IPEndPoint(IPAddress.Loopback, AdbClient.AdbServerPort));

		internal IEnumerable<DeviceData> GetDevicesYield() {
			foreach(DeviceData device in ConnectedDevices) {
				if(device == null) {
					continue;
				}

				yield return device;
			}
		}

		internal List<DeviceData> GetDevices() => ConnectedDevices;

		internal Dictionary<string, string> DisplayAllPackages(DeviceData? device) {
			if (DevicesCount == 0) {
				Logger.Warning("No devices found.");
			}

			if (device == null) {
				return default;
			}

			return new PackageManager(Client, device).Packages;
		}

		internal bool InstallPackage(DeviceData? device, string apkPath) {
			if (DevicesCount == 0) {
				Logger.Warning("No devices found.");
			}

			if (device == null || string.IsNullOrEmpty(apkPath)) {
				return false;
			}

			new PackageManager(Client, device).InstallPackage(apkPath, true);
			return true;
		}

		internal bool UninstallPackage(DeviceData? device, string packageName) {
			if (DevicesCount == 0) {
				Logger.Warning("No devices found.");
			}

			if (device == null || string.IsNullOrEmpty(packageName)) {
				return false;
			}

			new PackageManager(Client, device).UninstallPackage(packageName);
			return true;
		}

		internal async Task<string?> ExecuteCommand(string command, DeviceData? device, CancellationToken cancellationToken) {
			if (DevicesCount == 0) {
				Logger.Warning("No devices found.");
			}

			if (string.IsNullOrEmpty(command) || device == null) {
				return null;
			}

			ConsoleOutputReceiver recevier = new ConsoleOutputReceiver();

			try {				
				await Client.ExecuteRemoteCommandAsync(command, device, recevier, cancellationToken).ConfigureAwait(false);
			}
			catch (AdbException a) {
				Logger.Error(a.AdbError);
				return null;
			}
			
			return recevier.ToString();
		}

		private void OnDeviceChanged(object? sender, DeviceDataEventArgs e) {
			if (e == null || e.Device == null) {
				return;
			}

			Logger.Info($"{e.Device.Serial} has changed!");
			for (int i = 0; i < ConnectedDevices.Count; i++) {
				if (ConnectedDevices[i].Serial.Equals(e.Device.Serial)) {
					ConnectedDevices[i] = e.Device;
					break;
				}
			}
		}

		private void OnDeviceDisconnected(object? sender, DeviceDataEventArgs e) {
			if (e == null || e.Device == null) {
				return;
			}

			Logger.Info($"{e.Device.Serial} has been disconnected!");
			int removedCount = ConnectedDevices.RemoveAll(x => x.Serial.Equals(e.Device.Serial));
			Logger.Info($"'{removedCount}' device(s) removed from connected device collection.");
		}

		private void OnDeviceConnected(object? sender, DeviceDataEventArgs e) {
			if (e == null || e.Device == null) {
				return;
			}

			Logger.Info($"{e.Device.Serial} has been connected!");
			if (IsDuplicate(e.Device.Serial)) {
				return;
			}

			ConnectedDevices.Add(e.Device);
		}

		private static bool SearchCurrentDirectoryForADB(out string? adbDirectoryPath) {
			adbDirectoryPath = null;
			string currentDir = Directory.GetCurrentDirectory();

			foreach (string dir in Directory.GetDirectories(currentDir)) {
				if (string.IsNullOrEmpty(dir)) {
					continue;
				}

				DirectoryInfo dirInfo = new DirectoryInfo(dir);
				if (dirInfo.Name.Equals(Constants.ADB_DIRECTORY_NAME, StringComparison.OrdinalIgnoreCase)) {
					int fileExistCount = 0;
					foreach (string file in Directory.GetFiles(dir)) {
						if (string.IsNullOrEmpty(file)) {
							continue;
						}

						try {
							if (new FileInfo(file).Name.Equals(Constants.ADB_EXE_NAME, StringComparison.OrdinalIgnoreCase)) {
								fileExistCount++;
							}
							else if (new FileInfo(file).Name.Equals(Constants.ADB_WIN_API_DLL, StringComparison.OrdinalIgnoreCase)) {
								fileExistCount++;
							}
							else if (new FileInfo(file).Name.Equals(Constants.ADB_WIN_USB_API_DLL, StringComparison.OrdinalIgnoreCase)) {
								fileExistCount++;
							}
						}
						catch { continue; }
					}

					if (fileExistCount == 3) {
						adbDirectoryPath = dir;
						return true;
					}
				}
			}

			return false;
		}

		private DeviceData GetDevice(string serial) {			
			if (string.IsNullOrEmpty(serial) || DevicesCount == 0) {
				return null;
			}

			return ConnectedDevices.FirstOrDefault(x => x.Serial.Equals(serial));
		}

		private bool IsDuplicate(string? serial) {
			if (string.IsNullOrEmpty(serial)) {
				return false;
			}

			for (int i = 0; i < ConnectedDevices.Count; i++) {
				if (ConnectedDevices[i].Serial.Equals(serial)) {
					return true;
				}
			}

			return false;
		}

		public void Dispose() {
			ServerInstanceMutex.ReleaseMutex();
			ServerInstanceMutex.Dispose();
			Client.Disconnect(new DnsEndPoint(IPAddress.Loopback.ToString(), AdbClient.AdbServerPort));
			DeviceMonitor.Dispose();			
		}
	}
}
