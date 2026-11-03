using System;
using System.Collections;
using System.Text;

namespace BB.Mobile.Net;

public static class PathUtils
{
	public static Uri GenerateQueryAsPath(string basePath, params object[] variables)
	{
		if (basePath == null || variables == null)
		{
			throw new ArgumentNullException();
		}
		StringBuilder stringBuilder = new StringBuilder(basePath);
		GenerateSubPath(stringBuilder, variables);
		return new Uri(stringBuilder.ToString(), UriKind.Relative);
	}

	private static void GenerateSubPath(StringBuilder queryString, ICollection collection)
	{
		foreach (object item in collection)
		{
			if (item == null)
			{
				throw new ArgumentNullException();
			}
			if (item is ICollection)
			{
				GenerateSubPath(queryString, (ICollection)item);
				continue;
			}
			queryString.Append('/');
			queryString.Append(item);
		}
	}
}
