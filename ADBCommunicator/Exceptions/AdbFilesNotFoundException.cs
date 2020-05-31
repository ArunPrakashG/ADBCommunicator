using System;
using System.Collections.Generic;
using System.Text;

namespace ADBCommunicator.Exceptions
{
	public class AdbFilesNotFoundException: Exception
	{
		public AdbFilesNotFoundException() : base("Failed to find adb files in current directory.")
		{

		}
	}
}
