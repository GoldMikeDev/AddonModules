using System.Text;
namespace Rename.AddonModules.Extensions
{
	static class RangeExtensions
	{
		public static Range InclusiveRange(int start, int end) => start..(end + 1);
		public static int Length(this Range r) => r.End.Value - r.Start.Value;
		public static string ReadField(byte[,] data, Encoding encoding, int row, Range field)
		{
			int start = field.Start.Value;
			int length = field.Length();
			byte[] bytes = new byte[length];
			for (int i = 0; i < length; i++) bytes[i] = data[row, start + i];
			return encoding.GetString(bytes);
		}
		public static void WriteField(byte[,] data, Encoding encoding, int row, Range field, string value)
		{
			int start = field.Start.Value;
			int length = field.Length();
			byte[] bytes = encoding.GetBytes(value.PadLeft(length, '0')[..length]);
			for (int i = 0; i < length; i++) data[row, start + i] = bytes[i];
		}
	}
}