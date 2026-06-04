using System.Reflection;
namespace Rename.AddonModules.Extensions
{
	public class ParseExtensions()
	{
		public static object Parse(string type, object data)
		{
			object? output = null;
			object?[] input = [data, output];
			Type t = Type.GetType(type) ?? throw new ParseException();
			MethodInfo tryParse = t.GetMethod("TryParse", [typeof(string), t.MakeByRefType()]) ?? throw new ParseException();
			object? result = tryParse.Invoke(null, input) ?? throw new ParseException();
			bool success = (bool)result;
			output = input[1] as object ?? throw new ParseException();
			if (success) { return output; }
			else { throw new ParseException(); }
		}
	}
	public class ParseException : Exception
	{
		public ParseException() { }
		public override string? StackTrace => null;
	}
}