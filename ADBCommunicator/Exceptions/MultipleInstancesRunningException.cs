using System;
using System.Collections.Generic;
using System.Text;

namespace ADBCommunicator.Exceptions
{
	public class MultipleInstancesRunningException : Exception
	{
		public MultipleInstancesRunningException() : base("Multiple instances are running of the same process.")
		{

		}
	}
}
