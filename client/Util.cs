using System;
using UnityEngine;

namespace ksp_ris
{
	public static class Logging
	{
		public static void Log(string text)
		{
			Debug.Log("[RaceIntoSpace] " + text);
		}
		public static void LogFormat(string format, params object[] args)
		{
			Debug.LogFormat("[RaceIntoSpace] " + format, args);
		}
		public static void LogWarningFormat(string format, params object[] args)
		{
			Debug.LogWarningFormat("[RaceIntoSpace] " + format, args);
		}
		public static void LogException(Exception e)
		{
			Debug.LogException(e);
		}
	}
}

