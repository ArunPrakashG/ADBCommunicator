using ADBCommunicator.Exceptions;
using SharpAdbClient;
using Synergy.Logging;
using Synergy.Logging.Interfaces;
using System;
using System.IO;
using System.Reflection;
using System.Threading;

namespace ADBCommunicator {
	internal class Communicator : IDisposable {
		private static readonly ILogger Logger = new Logger(nameof(Communicator));
		private static readonly Mutex ServerInstanceMutex;
		private static readonly AdbServer Server = new AdbServer();

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

		}

		internal static bool StartServer(string? adbPath = null) {
			if (string.IsNullOrEmpty(adbPath) && SearchCurrentDirectoryForADB(out adbPath)) {
				if (string.IsNullOrEmpty(adbPath)) {
					throw new AdbFilesNotFoundException();
				}
			}

			Server.StartServer(adbPath, true);
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
