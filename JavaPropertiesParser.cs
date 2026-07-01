using System.Globalization;
using System.Text;
namespace CsharpDashboardCollector
{
	public static class JavaPropertiesParser
	{
		public static readonly Dictionary<string, string> config = [];
		private static readonly ILogger L = DashboardCollector.L;
		public static void LoadProperties(Stream input)
		{
			try
			{
				using StreamReader reader = new(input, Encoding.Latin1, detectEncodingFromByteOrderMarks: false, leaveOpen: true);
				string? line;
				while ((line = ReadLogicalLine(reader)) != null)
				{
					string trimmedLine = line.TrimStart(' ', '\t', '\f');
					if (trimmedLine.Length == 0 || trimmedLine.StartsWith('#') || trimmedLine.StartsWith('!')) continue;
					int separatorIndex = FindSeparatorIndex(line);
					if (separatorIndex < 0) separatorIndex = line.Length;
					string rawKey = line[..separatorIndex].TrimStart(' ', '\t', '\f');
					string rawValue = GetRawValue(line, separatorIndex);
					string key = UnescapePropertiesString(rawKey);
					string value = UnescapePropertiesString(rawValue);
					config[key] = value;
				}
			}
			catch (IOException e) { L.LogError(e, "Failed to load properties from stream"); throw; }
			catch (Exception e) { L.LogError(e, "Unexpected error while loading properties"); throw; }
		}
		private static string? ReadLogicalLine(this StreamReader reader)
		{
			string? line = reader.ReadLine();
			if (line == null) return null;
			StringBuilder lineBuilder = new();
			bool isContinuation = false;
			do
			{
				int backslashCount = 0;
				if (line.Length != 0 && !isContinuation) { string commentCheck = line.TrimStart(' ', '\t', '\f'); if (commentCheck.Length != 0 && (commentCheck[0] == '#' || commentCheck[0] == '!')) return line; }
				for (int i = line.Length - 1; i >= 0 && line[i] == '\\'; i--) backslashCount++;
				isContinuation = backslashCount % 2 != 0;
				if (isContinuation) line = line[..^1];
				if (lineBuilder.Length > 0) line = line.TrimStart(' ', '\t', '\f');
				lineBuilder.Append(line);
				if (!isContinuation) break;
				line = reader.ReadLine();
				if (line == null) break;
			}
			while (true);
			return lineBuilder.ToString();
		}
		private static int FindSeparatorIndex(string line)
		{
			bool inLeadingWhitespace = true;
			bool inEscape = false;
			for (int i = 0; i < line.Length; i++)
			{
				char c = line[i];
				if (c == '\\' && !inEscape) { inEscape = true; inLeadingWhitespace = false; continue; }
				if (inLeadingWhitespace && (c == ' ' || c == '\t' || c == '\f')) continue;
				inLeadingWhitespace = false;
				if ((c == '\t' || c == '\f' || c == ' ' || c == ':' || c == '=') && !inEscape) return i;
				inEscape = false;
			}
			return -1;
		}
		private static string GetRawValue(string line, int separatorIndex)
		{
			bool falseSeparator = false;
			int i = separatorIndex;
			while (i < line.Length && (line[i] == '\t' || line[i] == '\f' || line[i] == ' ' || line[i] == ':' || line[i] == '='))
			{
				if ((line[i] == ':' || line[i] == '=') && !falseSeparator) falseSeparator = true;
				else if ((line[i] == ':' || line[i] == '=') && falseSeparator) break;
				i++;
			}
			int valueStart = i;
			string value = line[valueStart..];
			return value;
		}
		private static string UnescapePropertiesString(string input)
		{
				StringBuilder result = new();
			for (int i = 0; i < input.Length; i++)
			{
				char c = input[i];
				if (c == '\\' && i + 1 < input.Length)
				{
					i++;
					if (input[i] == 'u')
					{
						if (i + 4 >= input.Length) throw new FormatException("Incomplete Unicode escape sequence");
						string hex = input[(i + 1)..(i + 5)];
						bool validHex = int.TryParse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out int codeUnit);
						if (!validHex) throw new FormatException($"Invalid Unicode escape sequence: \\u{hex}");
						result.Append((char)codeUnit);
						i += 4;
						continue;
					}
					c = input[i] switch
					{
						'\\' => '\\',
						'f' => '\f',
						'n' => '\n',
						'r' => '\r',
						't' => '\t',
						_ => c
					};
				}
				result.Append(c);
			}
			return result.ToString();
		}
	}
}