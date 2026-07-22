using System.Diagnostics;
using System.Text;
using Rename.AddonModules.Extensions;
using static Rename.AddonModules.Extensions.ParseExtensions;
using static Rename.AddonModules.Extensions.RangeExtensions;
namespace Rename.AddonModules
{
	public class CSRB                                                       //																Default config file size: 1MiB (1.04858MB)
	{
		static readonly Encoding UTF8 = new UTF8Encoding(false);
		static readonly int maxIndex = 4096;                                // Maximum number of records in the csrb file					Default: 4096
		static readonly int maxIndexLength = 256;                           // Maximum byte length of each record.							Default:  256	[Must be greater than headerLength + footerLength]
		static readonly int maxConcat = 1024;                               // Maximum number of concatenated indexes for a single entry.	Default: 1024	[Must be less than maxIndex]
		static readonly string separator = ",";                             // Character used to separate fields in the record.				Default:  ","	[For easy conversion to and from CSV]
		static readonly int separatorLength = UTF8.GetByteCount(separator);
		static readonly string footer = "\n";                               // New Line character at the end of each record.				Default: "\n"	["\n" (Line Feed), "\r" (Carriage Return) or "\r\n" (Carriage Return + Line Feed)]
		static readonly int footerLength = UTF8.GetByteCount(footer);
		static readonly Range index = InclusiveRange(0, 3);
		static readonly Range rh = InclusiveRange(5, 5);
		static readonly Range wh = InclusiveRange(7, 7);
		static readonly Range parity = InclusiveRange(9, 9);
		static readonly Range concat = InclusiveRange(11, 14);
		static readonly int headerLength = index.Length() + separatorLength + rh.Length() + separatorLength + wh.Length() + separatorLength + parity.Length() + separatorLength + concat.Length() + separatorLength;    // Total header byte size. Default: 14
		static readonly Range payload = InclusiveRange(headerLength, maxIndexLength - 2);
		static readonly int indexDigits = maxIndex.ToString().Length;
		static readonly int rhDigits = rh.Length();
		static readonly string rhPointer = "1";                             // Indicator for current Read Head postion.						Default: "1"	[Must be same length as readHead Range and should be different from readHeadNullPointer]
		static readonly string rhNullPointer = "0";                         // Indicator for empty Read Head position.						Default: "0"	[Must be same length as readHead Range and should be different from readHeadPointer]
		static readonly int whDigits = wh.Length();
		static readonly string whPointer = "1";                             // Indicator for current Write Head postion.					Default: "1"	[Must be same length as writeHead Range and should be different from writeHeadNullPointer]
		static readonly string whNullPointer = "0";                         // Indicator for empty Write Head position.						Default: "0"	[Must be same length as writeHead Range and should be different from writeHeadPointer]
		static readonly int parityDigits = parity.Length();
		const string parityEven = "0";										// Parity bit value for even parity.							Default: "0"	[Must be same length as parity Range and should be different from parityOdd]
		const string parityOdd = "1";										// Parity bit value for odd parity.								Default: "1"	[Must be same length as parity Range and should be different from parityEven]
		static readonly int concatDigits = maxConcat.ToString().Length;
		static readonly byte[,] csrbFile = new byte[maxIndex, maxIndexLength];
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
			using FileStream fs = File.Create(csrbFilePath);
			for (int i = 1; i <= maxIndex; i++)
			{
				string record = $"{i.ToString().PadLeft(indexDigits, '0')},{(i == 1 ? rhPointer : rhNullPointer).PadLeft(rhDigits, '0')},{(i == 1 ? whPointer : whNullPointer).PadLeft(whDigits, '0')},{(i == 1 ? parityOdd : parityEven).PadLeft(parityDigits, '0')},{1.ToString().PadLeft(concatDigits, '0')}," + new string('\u0020', maxIndexLength - headerLength - footerLength) + (i < maxIndex ? footer : " ");
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
			if (new FileInfo(csrbFilePath).Length != (long)maxIndex * maxIndexLength)
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
				byte[] data = File.ReadAllBytes(csrbFilePath);
				for (int i = 0; i < maxIndex; i++) { for (int j = 0; j < maxIndexLength; j++) { csrbFile[i, j] = data[i * maxIndexLength + j]; } }
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
				return;
			}
			return;
		}
		public static void ValidateAndRepair(ConsoleSpinner? spinner = null)
		{
			List<int> rhIndexList = [];
			List<int> whIndexList = [];
			for (int i = 0; i < maxIndex; i++)
			{
				if (ReadField(csrbFile, UTF8, i, rh) == rhPointer) { rhIndexList.Add(i); }
				if (ReadField(csrbFile, UTF8, i, wh) == whPointer) { whIndexList.Add(i); }
			}
			if (rhIndexList.Count != 1) { rhIndexList = ParitySearch(rhIndexList, rh, rhPointer, rhNullPointer, "Read", spinner); }
			if (whIndexList.Count != 1) { whIndexList = ParitySearch(whIndexList, wh, whPointer, whNullPointer, "Write", spinner); }
			rhPointerIndex = rhIndexList[0];
			whPointerIndex = whIndexList[0];
		}
		public static List<int> ParitySearch(List<int> pointerIndexList, Range field, string pointer, string nullPointer, string head, ConsoleSpinner? spinner = null)
		{
			int parityIndex = 0;
			int i = 0;
			string? paritySearch = GetFieldValue("string", Direction.next, parityIndex, i, Field.parity, spinner);
			if (string.IsNullOrEmpty(paritySearch)) { FieldException(Field.parity, spinner); return pointerIndexList; }
			if (paritySearch != parityEven && paritySearch != parityOdd) { FieldException(Field.parity, spinner); return pointerIndexList; }
			string paritySearchInitial = paritySearch;
			while (true)
			{
				parityIndex++;
				if (parityIndex > maxIndex) { break; }
				paritySearch = GetFieldValue("string", Direction.next, parityIndex, i, Field.parity, spinner);
				if (string.IsNullOrEmpty(paritySearch)) { FieldException(Field.parity, spinner); return pointerIndexList; }
				if (paritySearch != paritySearchInitial && (paritySearch == parityEven || paritySearch == parityOdd)) { break; }
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
					if (concatCheck == 1) { i--; break; }
				}
				concatList.Reverse();
				if (!ConcatValidation(concatList, spinner)) { return pointerIndexList; }
				paritySearch = GetFieldValue("string", Direction.previous, parityIndex, i, Field.parity, spinner);
				if (string.IsNullOrEmpty(paritySearch)) { FieldException(Field.parity, spinner); return pointerIndexList; }
				parityIndex = (parityIndex - i + maxIndex) % maxIndex;
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
				int result = (concatList[0] - 1);
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
			if (concatCheck == -1) { j = -1; return ""; }
			if (concatCheck > 1)
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
			else { string? payload = GetFieldValue("string", direction, rhPointerIndex, i, Field.payload, spinner); j = (i + 1); return payload?.TrimEnd() ?? ""; }
		}
		public static void Write(string newEntry, ConsoleSpinner? spinner = null)
		{
			int padding;
			string paddedPayload;
			if (string.IsNullOrEmpty(newEntry)) { return; }
			if (newEntry == lastSavedEntry) { return; }
			WriteField(csrbFile, UTF8, whPointerIndex, rh, rhPointer);
			if (UTF8.GetByteCount(newEntry) <= payload.Length())
			{
				padding = payload.Length() - UTF8.GetByteCount(newEntry);
				paddedPayload = newEntry + new string('\u0020', padding);
				WriteField(csrbFile, UTF8, whPointerIndex, payload, paddedPayload);
				WriteField(csrbFile, UTF8, whPointerIndex, concat, "1".PadLeft(concatDigits, '0'));
				string parityBit = Parity(rhPointerIndex, whPointerIndex, spinner);
				if (string.IsNullOrEmpty(parityBit)) { FieldException(Field.parity, spinner); return; }
				WriteField(csrbFile, UTF8, whPointerIndex, parity, parityBit);
				int rhPointerIndexPrevious = rhPointerIndex;
				rhPointerIndex = whPointerIndex;
				WriteField(csrbFile, UTF8, whPointerIndex, wh, whNullPointer);
				whPointerIndex = (whPointerIndex + 1) % maxIndex;
				WriteField(csrbFile, UTF8, whPointerIndex, wh, whPointer);
				WriteField(csrbFile, UTF8, rhPointerIndexPrevious, rh, rhNullPointer);
				lastSavedEntry = newEntry;
			}
			else if (UTF8.GetByteCount(newEntry) > payload.Length())
			{
				int concatValue = (int)Math.Ceiling((decimal)UTF8.GetByteCount(newEntry) / (decimal)payload.Length());
				string multiIndexEntry = newEntry;
				string parityBit = Parity(rhPointerIndex, whPointerIndex, spinner);
				if (string.IsNullOrEmpty(parityBit)) { FieldException(Field.parity, spinner); return; }
				int rhPointerIndexPrevious = rhPointerIndex;
				bool isFirstWrite = true;
				while (concatValue > 1)
				{
					int payloadChunkBytes = 0;
					string payloadRunes = "";
					foreach (Rune r in newEntry.EnumerateRunes())
					{
						int runeBytes = r.Utf8SequenceLength;
						if (isFirstWrite)
						{
							WriteField(csrbFile, UTF8, whPointerIndex, rh, rhPointer);
							rhPointerIndex = whPointerIndex;
							isFirstWrite = false;
						}
						if (payloadChunkBytes + runeBytes > payload.Length())
						{
							string multiIndexEntryPart = payloadRunes;
							padding = payload.Length() - payloadChunkBytes;
							paddedPayload = multiIndexEntryPart + new string('\u0020', padding);
							WriteField(csrbFile, UTF8, whPointerIndex, payload, paddedPayload);
							WriteField(csrbFile, UTF8, whPointerIndex, concat, concatValue.ToString().PadLeft(concatDigits, '0'));
							concatValue--;
							WriteField(csrbFile, UTF8, whPointerIndex, parity, parityBit);
							WriteField(csrbFile, UTF8, whPointerIndex, wh, whNullPointer);
							whPointerIndex = (whPointerIndex + 1) % maxIndex;
							WriteField(csrbFile, UTF8, whPointerIndex, wh, whPointer);
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
				WriteField(csrbFile, UTF8, whPointerIndex, wh, whNullPointer);
				whPointerIndex = (whPointerIndex + 1) % maxIndex;
				WriteField(csrbFile, UTF8, whPointerIndex, wh, whPointer);
				WriteField(csrbFile, UTF8, rhPointerIndexPrevious, rh, rhNullPointer);
				lastSavedEntry = multiIndexEntry;
			}
			Save(spinner);
		}
		public static string Parity(int rhPointerIndex, int whPointerIndex, ConsoleSpinner? spinner = null)
		{
			string? rhParity = GetFieldValue("string", Direction.previous, rhPointerIndex, 0, Field.parity, spinner);
			string? whParity = GetFieldValue("string", Direction.previous, whPointerIndex, 0, Field.parity, spinner);
			if (rhParity == null || whParity == null) { FieldException(Field.parity, spinner); return ""; }
			string parityBit;
			if (rhParity == whParity)
			{
				if (rhParity == parityEven) { parityBit = parityOdd; }
				else if (rhParity == parityOdd) { parityBit = parityEven; }
				else { FieldException(Field.parity, spinner); return ""; }
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
						check = Parse(type, ReadField(csrbFile, UTF8, ((recordIndex - i) + maxIndex) % maxIndex, fieldRange));
						return type switch
						{
							"int" => (int)check,
							"string" => (string)check,
							_ => throw new UnreachableException("⚠️ Invalid type. Must be 'int' or 'string'."),
						};
					case Direction.next:
						check = Parse(type, ReadField(csrbFile, UTF8, (recordIndex + i) % maxIndex, fieldRange));
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
			return;
		}
	}
}