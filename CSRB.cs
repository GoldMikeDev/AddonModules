using System.Diagnostics;
using System.Text;
using Rename.AddonModules.Extensions;
using static Rename.AddonModules.Extensions.ParseExtensions;
using static Rename.AddonModules.Extensions.RangeExtensions;
namespace Rename.AddonModules
{
	public class CSRB						//																Default config file size: 1MiB (1.04858MB)
	{
		static readonly Encoding UTF8 = new UTF8Encoding(false);
		const int MaxIndex = 4096;			// Maximum number of records in the csrb file					Default: 4096
		const int MaxIndexLength = 256;		// Maximum byte length of each record.							Default:  256	[Must be greater than headerLength + footerLength]
		const int MaxConcat = 1024;			// Maximum number of concatenated indexes for a single entry.	Default: 1024	[Must be less than maxIndex]
		const string Separator = ",";		// Character used to separate fields in the record.				Default:  ","	[For easy conversion to and from CSV]
		static readonly int separatorLength = UTF8.GetByteCount(Separator);
		const string Footer = "\n";			// New Line character at the end of each record.				Default: "\n"	["\n" (Line Feed), "\r" (Carriage Return) or "\r\n" (Carriage Return + Line Feed)]
		static readonly int footerLength = UTF8.GetByteCount(Footer);
		static readonly Range index = InclusiveRange(0, 3);
		static readonly Range rh = InclusiveRange(5, 5);
		static readonly Range wh = InclusiveRange(7, 7);
		static readonly Range parity = InclusiveRange(9, 9);
		static readonly Range concat = InclusiveRange(11, 14);
		static readonly int headerLength = index.Length() + separatorLength + rh.Length() + separatorLength + wh.Length() + separatorLength + parity.Length() + separatorLength + concat.Length() + separatorLength;	// Total header byte size. Default: 14
		static readonly Range payload = InclusiveRange(headerLength, MaxIndexLength - 2);
		static readonly int indexDigits = MaxIndex.ToString().Length;
		static readonly int rhDigits = rh.Length();
		const string RhPointer = "1";		// Indicator for current Read Head position.					Default: "1"	[Must be same length as readHead Range and should be different from readHeadNullPointer]
		const string RhNullPointer = "0";	// Indicator for empty Read Head position.						Default: "0"	[Must be same length as readHead Range and should be different from readHeadPointer]
		static readonly int whDigits = wh.Length();
		const string WhPointer = "1";		// Indicator for current Write Head position.					Default: "1"	[Must be same length as writeHead Range and should be different from writeHeadNullPointer]
		const string WhNullPointer = "0";	// Indicator for empty Write Head position.						Default: "0"	[Must be same length as writeHead Range and should be different from writeHeadPointer]
		static readonly int parityDigits = parity.Length();
		const string ParityEven = "0";		// Parity bit value for even parity.							Default: "0"	[Must be same length as parity Range and should be different from parityOdd]
		const string ParityOdd = "1";		// Parity bit value for odd parity.								Default: "1"	[Must be same length as parity Range and should be different from parityEven]
		static readonly int concatDigits = MaxConcat.ToString().Length;
		static readonly byte[,] csrbFile = new byte[MaxIndex, MaxIndexLength];
		static string lastSavedEntry = "";
		static readonly string userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
		static string csrbFilePath = Path.Combine(userProfile, ".dotnet", "tools", "CSRB", "CSRB.csrb");
		static int rhPointerIndex = 1;
		static int whPointerIndex = 1;
		public static void SetFilePath(string? path)
		{
			if (string.IsNullOrEmpty(path)) { return; }
			csrbFilePath = Path.Combine(userProfile, ".dotnet", "tools", "CSRB", path);
		}
		public enum Direction { previous, next }
		public enum Field { index, rh, wh, parity, concat, payload }
		public static void Create()
		{
			Directory.CreateDirectory(Path.Combine(userProfile, ".dotnet", "tools", "CSRB"));
			using var fs = File.Create(csrbFilePath);
			for (var i = 1; i <= MaxIndex; i++)
			{
				var record = $"{i.ToString().PadLeft(indexDigits, '0')},{(i == 1 ? RhPointer : RhNullPointer).PadLeft(rhDigits, '0')},{(i == 1 ? WhPointer : WhNullPointer).PadLeft(whDigits, '0')},{(i == 1 ? ParityOdd : ParityEven).PadLeft(parityDigits, '0')},{1.ToString().PadLeft(concatDigits, '0')}," + new string('\u0020', MaxIndexLength - headerLength - footerLength) + (i < MaxIndex ? Footer : " ");
				fs.Write(UTF8.GetBytes(record));
			}
		}
		public static void Load(string? path = null, ConsoleSpinner? spinner = null)
		{
			CSRB.SetFilePath(path);
			if (!File.Exists(csrbFilePath))
			{
				Create();
				if (spinner != null)
				{
					spinner.Enqueue("", true);
					spinner.Enqueue("⚠️ CSRB file not found. A new CSRB file has been created.", true);
					spinner.Enqueue("", true);
				}
				else { Console.WriteLine("⚠️ CSRB file not found. A new CSRB file has been created."); }
				return;
			}
			if (new FileInfo(csrbFilePath).Length != (long)MaxIndex * MaxIndexLength)
			{
				if (spinner != null)
				{
					spinner.Enqueue("", true);
					spinner.Enqueue("⚠️ CSRB file size has changed.", true);
					spinner.Enqueue("⚠️ Before typing any commands backup existing CSRB file if you need to preserve it. It will be overwritten upon next command entered.", true);
					spinner.Enqueue("", true);
				}
				else
				{
					Console.WriteLine(" ⚠️ CSRB file size has changed.");
					Console.WriteLine(" ⚠️ Before typing any commands backup existing CSRB file if you need to preserve it. It will be overwritten upon next command entered.");
				}
				return;
			}
			try
			{
				var data = File.ReadAllBytes(csrbFilePath);
				for (var i = 0; i < MaxIndex; i++) { for (var j = 0; j < MaxIndexLength; j++) { csrbFile[i, j] = data[i * MaxIndexLength + j]; } }
				ValidateAndRepair(spinner);
			}
			catch
			{
				if (spinner != null)
				{
					spinner.Enqueue("", true);
					spinner.Enqueue("⚠️ Failed to load CSRB file. Command history from previous session will be unavailable.", true);
					spinner.Enqueue("⚠️ Before typing any commands backup existing CSRB file if you need to preserve it. It will be overwritten upon saving.", true);
					spinner.Enqueue("", true);
				}
				else
				{
					Console.WriteLine(" ⚠️ Failed to load CSRB file. Command history from previous session will be unavailable.");
					Console.WriteLine(" ⚠️ Before typing any commands backup existing CSRB file if you need to preserve it. It will be overwritten upon saving.");
				}
			}
		}
		public static void ValidateAndRepair(ConsoleSpinner? spinner = null)
		{
			List<int> rhIndexList = [];
			List<int> whIndexList = [];
			for (var i = 0; i < MaxIndex; i++)
			{
				if (ReadField(csrbFile, UTF8, i, rh) == RhPointer) { rhIndexList.Add(i); }
				if (ReadField(csrbFile, UTF8, i, wh) == WhPointer) { whIndexList.Add(i); }
			}
			if (rhIndexList.Count != 1) { rhIndexList = ParitySearch(rhIndexList, rh, RhPointer, RhNullPointer, "Read", spinner); }
			if (whIndexList.Count != 1) { whIndexList = ParitySearch(whIndexList, wh, WhPointer, WhNullPointer, "Write", spinner); }
			rhPointerIndex = rhIndexList[0];
			whPointerIndex = whIndexList[0];
		}
		public static List<int> ParitySearch(List<int> pointerIndexList, Range field, string pointer, string nullPointer, string head, ConsoleSpinner? spinner = null)
		{
			var parityIndex = 0;
			var i = 0;
			string? paritySearch = GetFieldValue("string", Direction.next, parityIndex, i, Field.parity, spinner);
			if (string.IsNullOrEmpty(paritySearch) || (paritySearch != ParityEven && paritySearch != ParityOdd)) { FieldException(Field.parity, spinner); return pointerIndexList; }
			var paritySearchInitial = paritySearch;
			while (true)
			{
				parityIndex++;
				if (parityIndex > MaxIndex) { break; }
				paritySearch = GetFieldValue("string", Direction.next, parityIndex, i, Field.parity, spinner);
				if (string.IsNullOrEmpty(paritySearch)) { FieldException(Field.parity, spinner); return pointerIndexList; }
				if (paritySearch != paritySearchInitial && paritySearch is ParityEven or ParityOdd) { break; }
			}
			i = 2;
			int concatCheck = GetFieldValue("int", Direction.previous, parityIndex, i, Field.concat, spinner);
			if (concatCheck > 1)
			{
				List<int> concatList = [concatCheck];
				while (concatCheck > 1)
				{
					i++;
					concatCheck = GetFieldValue("int", Direction.previous, parityIndex, i, Field.concat, spinner);
					if (concatCheck == -1) { return pointerIndexList; }
					concatList.Add(concatCheck);
					if (concatCheck != 1) continue;
					i--; break;
				}
				concatList.Reverse();
				if (!ConcatValidation(concatList, spinner)) { return pointerIndexList; }
				paritySearch = GetFieldValue("string", Direction.previous, parityIndex, i, Field.parity, spinner);
				if (string.IsNullOrEmpty(paritySearch)) { FieldException(Field.parity, spinner); return pointerIndexList; }
				parityIndex = (parityIndex - i + MaxIndex) % MaxIndex;
			}
			else { parityIndex--; }
			WriteField(csrbFile, UTF8, parityIndex, field, pointer);
			HeadCleanup(pointerIndexList, field, nullPointer);
			pointerIndexList.Add(parityIndex);
			if (spinner != null) { spinner.Enqueue($"⚠️  {head} head was missing or duplicated and has been reset to index {parityIndex + 1}.", true); }
			else { Console.WriteLine($" ⚠️  {head} head was missing or duplicated and has been reset to index {parityIndex + 1}."); }
			Save(spinner);
			return pointerIndexList;
		}
		public static void HeadCleanup(List<int> pointerIndexList, Range field, string nullPointer)
		{
			while (pointerIndexList.Count > 0)
			{
				WriteField(csrbFile, UTF8, pointerIndexList[0], field, nullPointer);
				pointerIndexList.RemoveAt(0);
			}
		}
		public static bool ConcatValidation(List<int> concatList, ConsoleSpinner? spinner)
		{
			while (concatList.Count > 0)
			{
				var result = (concatList[0] - 1);
				if (concatList.Count == 1 && result == 0) { break; }
				if (concatList.Count == 1 && result != 0) { FieldException(Field.concat, spinner); return false; }
				if (result == concatList[1]) { concatList.RemoveAt(0); }
				else { FieldException(Field.concat, spinner); return false; }
			}
			return true;
		}
		public static string Read(int i, Direction direction, ConsoleSpinner? spinner, out int j)
		{
			int concatCheck = GetFieldValue("int", direction, rhPointerIndex, i, Field.concat, spinner);
			switch (concatCheck)
			{
				case -1:
					j = -1; return "";
				case > 1:
				{
					List<int> concatList = [concatCheck];
					StringBuilder payloadBuilder = new();
					while (concatCheck > 1)
					{
						string? stringPart = GetFieldValue("string", direction, rhPointerIndex, i, Field.payload, spinner);
						payloadBuilder.Append(stringPart);
						i++;
						concatCheck = GetFieldValue("int", direction, rhPointerIndex, i, Field.concat, spinner);
						if (concatCheck == -1) { j = -1; return ""; }
						concatList.Add(concatCheck);
					}
					if (direction == Direction.previous) { concatList.Reverse(); }
					if (!ConcatValidation(concatList, spinner)) { j = -1; return ""; }
					string? finalStringPart = GetFieldValue("string", direction, rhPointerIndex, i, Field.payload, spinner);
					payloadBuilder.Append(finalStringPart?.TrimEnd());
					j = (i + 1);
					return payloadBuilder.ToString();
				}
				default:
				{
					string? fieldValue = GetFieldValue("string", direction, rhPointerIndex, i, Field.payload, spinner); j = (i + 1); return fieldValue?.TrimEnd() ?? "";
				}
			}
		}
		public static void Write(string newEntry, ConsoleSpinner? spinner = null)
		{
			int padding;
			string paddedPayload;
			if (string.IsNullOrEmpty(newEntry)) { return; }
			if (newEntry == lastSavedEntry) { return; }
			WriteField(csrbFile, UTF8, whPointerIndex, rh, RhPointer);
			if (UTF8.GetByteCount(newEntry) <= payload.Length())
			{
				padding = payload.Length() - UTF8.GetByteCount(newEntry);
				paddedPayload = newEntry + new string('\u0020', padding);
				WriteField(csrbFile, UTF8, whPointerIndex, payload, paddedPayload);
				WriteField(csrbFile, UTF8, whPointerIndex, concat, "1".PadLeft(concatDigits, '0'));
				var parityBit = Parity(rhPointerIndex, whPointerIndex, spinner);
				if (string.IsNullOrEmpty(parityBit)) { FieldException(Field.parity, spinner); return; }
				WriteField(csrbFile, UTF8, whPointerIndex, parity, parityBit);
				var rhPointerIndexPrevious = rhPointerIndex;
				rhPointerIndex = whPointerIndex;
				WriteField(csrbFile, UTF8, whPointerIndex, wh, WhNullPointer);
				whPointerIndex = (whPointerIndex + 1) % MaxIndex;
				WriteField(csrbFile, UTF8, whPointerIndex, wh, WhPointer);
				WriteField(csrbFile, UTF8, rhPointerIndexPrevious, rh, RhNullPointer);
				lastSavedEntry = newEntry;
			}
			else if (UTF8.GetByteCount(newEntry) > payload.Length())
			{
				var concatValue = (int)Math.Ceiling(UTF8.GetByteCount(newEntry) / (decimal)payload.Length());
				var multiIndexEntry = newEntry;
				var parityBit = Parity(rhPointerIndex, whPointerIndex, spinner);
				if (string.IsNullOrEmpty(parityBit)) { FieldException(Field.parity, spinner); return; }
				var rhPointerIndexPrevious = rhPointerIndex;
				var isFirstWrite = true;
				while (concatValue > 1)
				{
					var payloadChunkBytes = 0;
					var payloadRunes = "";
					foreach (var r in newEntry.EnumerateRunes())
					{
						var runeBytes = r.Utf8SequenceLength;
						if (isFirstWrite)
						{
							WriteField(csrbFile, UTF8, whPointerIndex, rh, RhPointer);
							rhPointerIndex = whPointerIndex;
							isFirstWrite = false;
						}
						if (payloadChunkBytes + runeBytes > payload.Length())
						{
							padding = payload.Length() - payloadChunkBytes;
							paddedPayload = payloadRunes + new string('\u0020', padding);
							WriteField(csrbFile, UTF8, whPointerIndex, payload, paddedPayload);
							WriteField(csrbFile, UTF8, whPointerIndex, concat, concatValue.ToString().PadLeft(concatDigits, '0'));
							concatValue--;
							WriteField(csrbFile, UTF8, whPointerIndex, parity, parityBit);
							WriteField(csrbFile, UTF8, whPointerIndex, wh, WhNullPointer);
							whPointerIndex = (whPointerIndex + 1) % MaxIndex;
							WriteField(csrbFile, UTF8, whPointerIndex, wh, WhPointer);
							newEntry = newEntry[payloadRunes.Length..];
							break;
						}
						payloadChunkBytes += runeBytes;
						payloadRunes += r.ToString();
					}
				}
				padding = payload.Length() - UTF8.GetByteCount(newEntry);
				paddedPayload = newEntry + new string('\u0020', padding);
				WriteField(csrbFile, UTF8, whPointerIndex, payload, paddedPayload);
				WriteField(csrbFile, UTF8, whPointerIndex, concat, concatValue.ToString().PadLeft(concatDigits, '0'));
				WriteField(csrbFile, UTF8, whPointerIndex, parity, parityBit);
				WriteField(csrbFile, UTF8, whPointerIndex, wh, WhNullPointer);
				whPointerIndex = (whPointerIndex + 1) % MaxIndex;
				WriteField(csrbFile, UTF8, whPointerIndex, wh, WhPointer);
				WriteField(csrbFile, UTF8, rhPointerIndexPrevious, rh, RhNullPointer);
				lastSavedEntry = multiIndexEntry;
			}
			Save(spinner);
		}
		public static string Parity(int rhPointerIndexParity, int whPointerIndexParity, ConsoleSpinner? spinner = null)
		{
			string? rhParity = GetFieldValue("string", Direction.previous, rhPointerIndexParity, 0, Field.parity, spinner);
			string? whParity = GetFieldValue("string", Direction.previous, whPointerIndexParity, 0, Field.parity, spinner);
			if (rhParity == null || whParity == null) { FieldException(Field.parity, spinner); return ""; }
			string parityBit;
			if (rhParity == whParity)
			{
				switch (rhParity)
				{
					case ParityEven:
						parityBit = ParityOdd;
						break;
					case ParityOdd:
						parityBit = ParityEven;
						break;
					default:
						FieldException(Field.parity, spinner); return "";
				}
			}
			else { parityBit = rhParity; }
			return parityBit;
		}
		public static void Save(ConsoleSpinner? spinner = null)
		{
			byte[] data = [.. csrbFile.Cast<byte>()];
			try { File.WriteAllBytes(csrbFilePath, data); }
			catch
			{
				if (spinner != null)
				{
					spinner.Enqueue("", true);
					spinner.Enqueue("⚠️ Failed to save CSRB file.", true);
					spinner.Enqueue("⚠️ Backup existing CSRB file if you need to preserve it. It will be overwritten upon saving.", true);
					spinner.Enqueue("", true);
				}
				else
				{
					Console.WriteLine(" ⚠️ Failed to save CSRB file.");
					Console.WriteLine(" ⚠️ Backup existing CSRB file if you need to preserve it. It will be overwritten upon saving.");
				}
			}
		}
		public static dynamic? GetFieldValue(string type, Direction direction, int recordIndex, int i, Field field, ConsoleSpinner? spinner = null)
		{
			var fieldRange = field switch
			{
				Field.index => index,
				Field.rh => rh,
				Field.wh => wh,
				Field.parity => parity,
				Field.concat => concat,
				Field.payload => payload,
				_ => throw new UnreachableException("⚠️ Invalid field. Must be 'index', 'rh', 'wh', 'parity', 'concat' or 'payload'."),
			};
			try
			{
				object check;
				switch (direction)
				{
					case Direction.previous:
						check = Parse(type, ReadField(csrbFile, UTF8, ((recordIndex - i) + MaxIndex) % MaxIndex, fieldRange));
						return type switch
						{
							"int" => (int)check,
							"string" => (string)check,
							_ => throw new UnreachableException("⚠️ Invalid type. Must be 'int' or 'string'."),
						};
					case Direction.next:
						check = Parse(type, ReadField(csrbFile, UTF8, (recordIndex + i) % MaxIndex, fieldRange));
						return type switch
						{
							"int" => (int)check,
							"string" => (string)check,
							_ => throw new UnreachableException("⚠️ Invalid type. Must be 'int' or 'string'."),
						};
					default:
						throw new UnreachableException("⚠️ Invalid direction. Must be 'previous' or 'next'.");
				}
			}
			catch (ParseException) { FieldException(field, spinner); return null; }
		}
		public static void FieldException(Field field, ConsoleSpinner? spinner = null)
		{
			if (spinner != null)
			{
				spinner.Enqueue("", true);
				switch (field)
				{
					case Field.index:
						spinner.Enqueue("⚠️ CSRB index corruption.", true);
						break;
					case Field.rh:
						spinner.Enqueue("⚠️ CSRB read head corruption.", true);
						break;
					case Field.wh:
						spinner.Enqueue("⚠️ CSRB write head corruption.", true);
						break;
					case Field.parity:
						spinner.Enqueue("⚠️ CSRB parity corruption.", true);
						break;
					case Field.concat:
						spinner.Enqueue("⚠️ CSRB concat corruption.", true);
						break;
					case Field.payload:
						spinner.Enqueue("⚠️ CSRB payload corruption.", true);
						break;
					default:
						spinner.Enqueue("⚠️ CSRB unknown corruption.", true);
						break;
				}
				spinner.Enqueue("⚠️ Before typing any commands backup existing CSRB file if you need to preserve it. It will be overwritten upon next command entered.", true);
				spinner.Enqueue("", true);
			}
			else
			{
				throw field switch
				{
					Field.index => new Exception("⚠️ CSRB index corruption."),
					Field.rh => new Exception("⚠️ CSRB read head corruption."),
					Field.wh => new Exception("⚠️ CSRB write head corruption."),
					Field.parity => new Exception("⚠️ CSRB parity corruption."),
					Field.concat => new Exception("⚠️ CSRB concat corruption."),
					Field.payload => new Exception("⚠️ CSRB payload corruption."),
					_ => new UnreachableException("⚠️ CSRB unknown corruption."),
				};
			}
		}
	}
}