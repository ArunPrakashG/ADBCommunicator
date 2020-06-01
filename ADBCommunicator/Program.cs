using SharpAdbClient;
using Synergy.Logging;
using Synergy.Logging.EventArgs;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ADBCommunicator {
	internal class Program {
		private static readonly Communicator Communicator = new Communicator();
		private static readonly CancellationTokenSource KeepAliveToken = new CancellationTokenSource();
		private static int ExitCode = 0;
		private static bool DisposeAlreadyCalled;

		private static async Task<int> Main(string[] args) {
			Synergy.Extensions.Helpers.CloseProcess(Constants.ADB_EXE_NAME);
			Logger.LogMessageReceived += Logger_LogMessageReceived;
			AppDomain.CurrentDomain.ProcessExit += OnEnvironmentExit;
			Console.CancelKeyPress += OnForceQuitApp;
			Communicator.StartServer();
			Communicator.InitDeviceMonitor();
			Communicator.InitClient();

			if(Communicator.DevicesCount == 0) {
				Out("No devices found. If the device is already connected, recheck if USB Debugging is enabled in Developer settings!");
			}

			return await KeepAlive().ConfigureAwait(false);
		}

		private static void OnForceQuitApp(object sender, ConsoleCancelEventArgs e) => Exit(-1);

		private static void OnEnvironmentExit(object? sender, EventArgs e) => Exit(0);

		private static async Task<int> KeepAlive() {
			Out("press 'i' to install a package onto the device.");
			Out("press 'u' to uninstall a package from the device.");
			Out("press 'd' to display all connected devices on this pc.");
			Out("press 'p' to display installed packages on the device.");
			Out("press 'c' for interactive command processing.");

			while (!KeepAliveToken.IsCancellationRequested) {
				ConsoleKey key = Console.ReadKey(true).Key;
				switch (key) {
					case ConsoleKey.P:
						if (Communicator.DevicesCount == 0) {
							Out("No devices found.");
						}

						if (!DisplayAndGetSelection(out DeviceData? selectedDevice)) {
							continue;
						}

						Dictionary<string, string> packages = Communicator.DisplayAllPackages(selectedDevice);

						if(packages.Count > 0) {
							foreach(KeyValuePair<string, string> packPair in packages) {
								Out($"{packPair.Key} | {packPair.Value}", ConsoleColor.Green);
							}

							continue;
						}

						Out("No packages can be found on the device.");
						continue;
					case ConsoleKey.I:
						if (Communicator.DevicesCount == 0) {
							Out("No devices found.");
						}

						if (!DisplayAndGetSelection(out selectedDevice)) {
							continue;
						}

						Out("Enter the APK file path: ");
						string apkPath = Console.ReadLine();

						if (string.IsNullOrEmpty(apkPath)) {
							Out("Invalid path.");
							continue;
						}

						if (!File.Exists(apkPath)) {
							Out("Such a file doesn't exist.");
							continue;
						}

						Out("APK File path: " + apkPath, ConsoleColor.Green);
						if (Communicator.InstallPackage(selectedDevice, apkPath)) {
							Out("Operation completed successfully!");
							continue;
						}

						Out("Operation failed.");
						continue;
					case ConsoleKey.U:
						if (Communicator.DevicesCount == 0) {
							Out("No devices found.");
						}

						if (!DisplayAndGetSelection(out selectedDevice)) {
							continue;
						}

						Out("Enter the APK file path: ");
						apkPath = Console.ReadLine();

						if (string.IsNullOrEmpty(apkPath)) {
							Out("Invalid path.");
							continue;
						}

						if (!File.Exists(apkPath)) {
							Out("Such a file doesn't exist.");
							continue;
						}

						Out("APK File path: " + apkPath, ConsoleColor.Green);
						if (Communicator.UninstallPackage(selectedDevice, apkPath)) {
							Out("Operation completed successfully!");
							continue;
						}

						Out("Operation failed.");
						continue;
					case ConsoleKey.D:
						if (Communicator.DevicesCount == 0) {
							Out("No devices found.");
						}

						DisplayDevices();
						continue;
					case ConsoleKey.C:
						if (Communicator.DevicesCount == 0) {
							Out("No devices found.");
						}

						Out("Starting interactive command processor...", ConsoleColor.Green);
						if (!DisplayAndGetSelection(out selectedDevice)) {
							continue;
						}

						const ConsoleKey exitKey = ConsoleKey.Q;
						const int maxCommandWaitTime = 10;
						Console.WriteLine(">> Press any key to Initiate command processor...");

						while(Console.ReadKey(true).Key != exitKey) {
							Console.Write("CMD >>> ");
							string? cmd = Console.ReadLine();

							if (string.IsNullOrEmpty(cmd)) {
								Out($"Invalid command. Try again or press '{exitKey}' to exit the command processor.");
								continue;
							}

							Out("Processing...", ConsoleColor.Green);
							CancellationTokenSource commandToken = new CancellationTokenSource(TimeSpan.FromMinutes(maxCommandWaitTime));
							string? result = await Communicator.ExecuteCommand(cmd, selectedDevice, commandToken.Token).ConfigureAwait(false);

							if (string.IsNullOrEmpty(result)) {
								Out("There is no result to display.");
								continue;
							}

							Console.Write("RESULT >>> " + result);
						}

						continue;
					default:
						continue;
				}
			}

			return ExitCode;
		}

		internal static void Exit(int exitCode = 0) {
			if (DisposeAlreadyCalled) {
				return;
			}

			Synergy.Extensions.Helpers.CloseProcess(Constants.ADB_EXE_NAME);
			KeepAliveToken.Cancel();
			DisposeAlreadyCalled = true;
			Communicator.Dispose();			
			ExitCode = exitCode;
		}

		private static void DisplayDevices() {
			int count = 0;
			foreach (DeviceData device in Communicator.GetDevicesYield()) {
				Out($"{++count} | {device.Serial} | [{device.State}]");
			}
		}

		private static bool DisplayAndGetSelection(out DeviceData? selectedDevice) {
			int count = 0;

			foreach (DeviceData device in Communicator.GetDevicesYield()) {
				Out($"{++count} | {device.Serial} | [{device.State}]");
			}

			Out("Enter the Number of the device you want to execute commands on: ");
			string sel = Console.ReadLine();

			if (string.IsNullOrEmpty(sel) || !int.TryParse(sel, out int selectedIndex)) {
				Out("Invalid selection; run again!");
				selectedDevice = null;
				return false;
			}

			try {
				selectedDevice = Communicator.GetDevices().ElementAt(selectedIndex - 1);

				if (selectedDevice == null) {
					Out("No device selected.");
					return false;
				}

				Out($"Selected device: {selectedDevice.Name}", ConsoleColor.Green);
				return true;
			}
			catch {
				Out("Such a device doesn't exist in the index.");
				selectedDevice = null;
				return false;
			}
		}

		private static void Out(string? msg, ConsoleColor color = ConsoleColor.Gray) {
			if (string.IsNullOrEmpty(msg)) {
				return;
			}

			Console.ForegroundColor = color;
			Console.WriteLine(">> " + msg);
			Console.ResetColor();
		}

		private static void Logger_LogMessageReceived(object sender, LogMessageEventArgs e) {
			if (e == null) {
				return;
			}

			switch (e.LogLevel) {
				case Enums.LogLevels.Trace:
				case Enums.LogLevels.Debug:
				case Enums.LogLevels.Info:
					Console.ForegroundColor = ConsoleColor.White;
					Console.WriteLine($"{e.ReceivedTime} | {e.LogIdentifier} | {e.LogLevel} | {e.CallerMemberName}() {e.LogMessage}");
					break;
				case Enums.LogLevels.Warn:
					Console.ForegroundColor = ConsoleColor.Magenta;
					Console.WriteLine($"{e.ReceivedTime} | {e.LogIdentifier} | {e.LogLevel} | {e.CallerMemberName}() {e.LogMessage}");
					break;
				case Enums.LogLevels.Error:
					Console.ForegroundColor = ConsoleColor.Red;
					Console.WriteLine($"{e.ReceivedTime} | {e.LogIdentifier} | {e.LogLevel} | {e.CallerMemberName}() {e.LogMessage}");
					break;
				case Enums.LogLevels.Exception:
					Console.ForegroundColor = ConsoleColor.Red;
					Console.WriteLine($"{e.ReceivedTime} | {e.LogIdentifier} | {e.LogLevel} | {e.CallerMemberName}() {e.LogMessage}");
					break;
				case Enums.LogLevels.Fatal:
					Console.ForegroundColor = ConsoleColor.Yellow;
					Console.WriteLine($"{e.ReceivedTime} | {e.LogIdentifier} | {e.LogLevel} | {e.CallerMemberName}() {e.LogMessage}");
					break;
				case Enums.LogLevels.Green:
					Console.ForegroundColor = ConsoleColor.Green;
					Console.WriteLine($"{e.ReceivedTime} | {e.LogIdentifier} | {e.LogLevel} | {e.CallerMemberName}() {e.LogMessage}");
					break;
				case Enums.LogLevels.Red:
					Console.ForegroundColor = ConsoleColor.Red;
					Console.WriteLine($"{e.ReceivedTime} | {e.LogIdentifier} | {e.LogLevel} | {e.CallerMemberName}() {e.LogMessage}");
					break;
				case Enums.LogLevels.Blue:
					Console.ForegroundColor = ConsoleColor.Blue;
					Console.WriteLine($"{e.ReceivedTime} | {e.LogIdentifier} | {e.LogLevel} | {e.CallerMemberName}() {e.LogMessage}");
					break;
				case Enums.LogLevels.Cyan:
					Console.ForegroundColor = ConsoleColor.Cyan;
					Console.WriteLine($"{e.ReceivedTime} | {e.LogIdentifier} | {e.LogLevel} | {e.CallerMemberName}() {e.LogMessage}");
					break;
				case Enums.LogLevels.Magenta:
					Console.ForegroundColor = ConsoleColor.Magenta;
					Console.WriteLine($"{e.ReceivedTime} | {e.LogIdentifier} | {e.LogLevel} | {e.CallerMemberName}() {e.LogMessage}");
					break;
				case Enums.LogLevels.Input:
					Console.ForegroundColor = ConsoleColor.Gray;
					Console.WriteLine($"{e.ReceivedTime} | {e.LogIdentifier} | {e.LogLevel} | {e.CallerMemberName}() {e.LogMessage}");
					break;
				case Enums.LogLevels.Custom:
					Console.WriteLine($"{e.ReceivedTime} | {e.LogIdentifier} | {e.LogLevel} | {e.CallerMemberName}() {e.LogMessage}");
					break;
			}

			Console.ResetColor();
		}
	}
}
