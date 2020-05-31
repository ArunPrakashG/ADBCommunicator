using Synergy.Logging;
using Synergy.Logging.EventArgs;
using System;

namespace ADBCommunicator {
	internal class Program {
		private static void Main(string[] args) {
			Logger.LogMessageReceived += Logger_LogMessageReceived;
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
