using ADBCommunicator.Exceptions;
using SharpAdbClient;
using Synergy.Logging;
using Synergy.Logging.Interfaces;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Reflection;
using System.Threading;

namespace ADBCommunicator {
	internal class Communicator : IDisposable {
		private static readonly ILogger Logger = new Logger(nameof(Communicator));
		private static readonly Mutex ServerInstanceMutex;
		private static readonly AdbServer Server = new AdbServer();

		private readonly DeviceMonitor DeviceMonitor;
		private readonly List<DeviceData> ConnectedDevices = new List<DeviceData>();

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
		}

		internal Communicator() {
			DeviceMonitor = new DeviceMonitor(new AdbSocket(new IPEndPoint(IPAddress.Loopback, AdbClient.AdbServerPort)));
		}

		internal void StartNewDeviceMonitor() {
			if (!DeviceMonitor.IsRunning) {
				DeviceMonitor.Start();
			}

			DeviceMonitor.DeviceConnected += OnDeviceConnected;
			DeviceMonitor.DeviceDisconnected += OnDeviceDisconnected;
			DeviceMonitor.DeviceChanged += OnDeviceChanged;
		}

		private void OnDeviceChanged(object? sender, DeviceDataEventArgs e) {

		}

		private void OnDeviceDisconnected(object? sender, DeviceDataEventArgs e) {
			if (e == null || e.Device == null) {
				return;
			}

			ConnectedDevices.RemoveAll(x => x.Serial.Equals(e.Device.Serial));
		}

		private void OnDeviceConnected(object? sender, DeviceDataEventArgs e) {
			if (e == null || e.Device == null) {
				return;
			}

			if (IsDuplicate(e.Device.Serial)) {
				return;
			}

			ConnectedDevices.Add(e.Device);
		}



		private bool IsDuplicate(string? serial) {
			for (int i = 0; i < ConnectedDevices.Count; i++) {
				if (ConnectedDevices[i].Serial.Equals(serial)) {
					return true;
				}
			}

			return false;
		}

		internal static bool StartServer(string? adbPath = null) {
			if (string.IsNullOrEmpty(adbPath) && SearchCurrentDirectoryForADB(out adbPath)) {
				if (string.IsNullOrEmpty(adbPath)) {
					throw new AdbFilesNotFoundException();
				}
			}

			Server.StartServer(adbPath + "/" + Constants.ADB_EXE_NAME, true);
			return true;
		}

		private static bool SearchCurrentDirectoryForADB(out string? adbDirectoryPath) {
			adbDirectoryPath = null;
			string currentDir = Directory.GetCurrentDirectory();

			foreach (string dir in Directory.GetDirectories(currentDir)) {
				if (string.IsNullOrEmpty(dir)) {
					continue;
				}

				if (dir.Equals(Constants.ADB_DIRECTORY_NAME, StringComparison.OrdinalIgnoreCase)) {
					int fileExistCount = 1;
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

		public void Dispose() {
			ServerInstanceMutex.ReleaseMutex();
			ServerInstanceMutex.Dispose();
		}
	}
}
