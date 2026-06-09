using System.Reflection;
namespace ToolBox.AddonModules.Extensions
{
	public class ParseExtensions()
	{
		public static object Parse(string type, object data)
		{
			object? output = null;
			object?[] input = [data, output];
			type = type switch
			{
				"bool" => "System.Boolean",
				"byte" => "System.Byte",
				"char" => "System.Char",
				"decimal" => "System.Decimal",
				"double" => "System.Double",
				"float" => "System.Single",
				"int" => "System.Int32",
				"long" => "System.Int64",
				"sbyte" => "System.SByte",
				"short" => "System.Int16",
				"string" => "System.String",
				"uint" => "System.UInt32",
				"ulong" => "System.UInt64",
				"ushort" => "System.UInt16",
				_ => throw new ParseException($"Unsupported type: {type}"),
			};
			Type t = Type.GetType(type) ?? throw new ParseException($"Type not found: {type}");
			MethodInfo tryParse = t.GetMethod("TryParse", [typeof(string), t.MakeByRefType()]) ?? throw new ParseException($"Method not found: TryParse");
			object? result = tryParse.Invoke(null, input) ?? throw new ParseException($"Failed to parse {type}");
			bool success = (bool)result;
			output = input[1] as object ?? throw new ParseException($"Failed to parse {type}");
			if (success) { return output; }
			else { throw new ParseException($"Failed to parse {type}"); }
		}
	}
	public class ParseException : Exception
	{
		public ParseException() { }
		public ParseException(string message) : base(message) { }
		public override string? StackTrace => null;
	}
}