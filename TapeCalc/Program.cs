using System;
using System.Collections.Generic;
using System.IO.Ports;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TapeCalc.Properties;

namespace TapeCalc {
	class Program {
		static readonly byte[] RemoveWatermarkBytes = new byte[] { 10, 29, 123, 119, 0 };
		static readonly byte[] CutBytes = new byte[] { 10, 29, 86, 66, 0 };
		static string decimalFormat = "#,##0.##";

		static decimal total;
		static decimal lineTotal;
		static string numberInput = "";
		static string lineInput = "";
		static string numberInputHeader = "";
		static bool lineNegative;
		static bool numberDivision;
		static State state;

		static void Clear(bool prompt = true) {
			PrintLine("--------------------------------------------");
			Cut();
			PrintLine("--------------------------------------------");

			total = 0;
			lineTotal = 1;
			numberInput = "";
			lineInput = "  ";
			numberInputHeader = "";
			lineNegative = false;
			numberDivision = false;
			state = State.Clean;
		}

		static void Main(string[] args) {
			PrintToPrinter(bytes: RemoveWatermarkBytes);
			Clear();

			while (true) {
				ClearConsoleLines(2);
				Console.Write(lineInput + numberInputHeader + numberInput);
				WriteNextLine("Subtotal: " + total.ToString(decimalFormat));
				var key = Console.ReadKey(true);
				ClearConsoleLines(2);
				switch (char.ToLowerInvariant(key.KeyChar)) {
					case 'p':
						ChangePort();
						break;

					case '1':
					case '2':
					case '3':
					case '4':
					case '5':
					case '6':
					case '7':
					case '8':
					case '9':
					case '0':
					case '.':
						Input(key.KeyChar);
						break;

					case '*':
						Multiply();
						break;
					case '/':
						Divide();
						break;

					case '+':
						Plus();
						break;
					case '-':
						Minus();
						break;

					case 'n':
						Negate();
						break;

					case '=':
					case '\r':
					case '\n':
						Calculate();
						break;

					case '\b':
						Backspace();
						break;

					case 'c':
						Cut();
						break;

					case 's':
						Subtotal();
						break;

					case 'd':
						ChangeNumberOfSignificantDigits();
						break;

					case 'z':
						Clear();
						break;

					case '?':
						Help();
						break;

					default:
						switch (key.Key) {
							case ConsoleKey.F4:
								if (key.Modifiers.HasFlag(ConsoleModifiers.Alt)) {
									Cut();
									return;
								}
								break;

							default:
								System.Diagnostics.Debug.WriteLine("Unknown key " + key.Key);
								break;
						}
						break;
				}
			}
		}

		private static void ChangeNumberOfSignificantDigits() {
			Console.WriteLine();
			Console.WriteLine();
			Console.WriteLine("Change number of significant digits.");
			Console.Write("New number? ");
			var str = Console.ReadLine();
			byte num;
			if (byte.TryParse(str, out num)) {
				decimalFormat = "#,##0." + new string('#', num);
			} else {
				Console.WriteLine("Invalid number " + str);
				Console.WriteLine();
			}
			Clear();
		}

		private static void Backspace() {
			if (numberInput.Length > 0) {
				numberInput = numberInput.Substring(0, numberInput.Length - 1);
			}
		}

		private static void ChangePort() {
			Console.WriteLine();
			Console.WriteLine();
			Console.WriteLine("Change COM port.");
			Console.Write("New port? ");
			var port = Console.ReadLine();
			if (!string.IsNullOrWhiteSpace(port)) {
				Settings.Default.Port = port;
				Settings.Default.Save();
			}
			Clear();
		}

		private static void Help() {
			Console.WriteLine();
			Console.WriteLine();
			Console.WriteLine("TapeCalc usage:");
			Console.WriteLine("Each line is added to the total, and may contain multiplications and divisions inside it.");
			Console.WriteLine("Supported operations: +, -, *, /");
			Console.WriteLine("Press = or enter to calculate the result and start over.");
			Console.WriteLine("Press z to clear");
			Console.WriteLine("Press c to cut the printer paper");
			Console.WriteLine("Press s to print a subtotal");
			Console.WriteLine("Press p to change the printer's COM port number");
			Clear();
		}

		private static void Input(char digit) {
			if (digit == '.' && numberInput.Contains(".")) {
				return;
			}
			numberInput += digit;
		}

		private static void Plus() {
			PlusMinus(negative: false, indent: "+ ");
		}
		private static void Minus() {
			PlusMinus(negative: true, indent: "- ");
		}
		private static void PlusMinus(bool negative, string indent) {
			FinishLine();
			lineNegative = negative;
			lineInput = indent;
			RemoveState(State.ShouldCalculateLine);
			AddState(State.ShouldCalculate);
			AddState(State.ShouldCut);
		}

		private static void Negate() {
			lineNegative = !lineNegative;
			var startChar = lineNegative ? '-' : '+';
			lineInput = startChar + lineInput.Substring(1);
		}

		private static void AddState(State s) {
			state |= s;
		}
		private static void RemoveState(State s) {
			state &= ~s;
		}

		private static void Multiply() {
			MultiplyDivide(division: false, inputHeader: " * ");
		}
		private static void Divide() {
			MultiplyDivide(division: true, inputHeader: " / ");
		}
		private static void MultiplyDivide(bool division, string inputHeader) {
			FinishNumber();
			numberDivision = division;
			numberInputHeader = inputHeader;
			AddState(State.ShouldCalculateLine);
			AddState(State.ShouldCalculate);
			AddState(State.ShouldCut);
		}

		private static void FinishNumber() {
			var number = string.IsNullOrEmpty(numberInput) ? 1m : decimal.Parse(numberInput);
			if (lineInput != null && lineInput.Length > 2) {
				if (numberDivision) {
					if (number == 0) {
						lineTotal = 0;
					} else {
						lineTotal /= number;
					}
				} else {
					lineTotal *= number;
				}
				lineInput += numberInputHeader;
			} else {
				if (string.IsNullOrEmpty(numberInput)) {
					number = 0;
				}
				lineTotal = number;
			}
			lineInput += number.ToString(decimalFormat);
			numberInput = "";
		}

		private static void FinishLine() {
			FinishNumber();
			if (lineNegative) {
				lineTotal = -lineTotal;
			}
			total += lineTotal;
			PrintLine(lineInput);
			if (state.HasFlag(State.ShouldCalculateLine)) {
				RemoveState(State.ShouldCalculateLine);
				PrintLine("  (=== " + lineTotal.ToString(decimalFormat) + ")");
			}
			lineTotal = 1;
			lineInput = null; // it would be wrong to use it without setting it immediately after calling FinishLine
			numberInputHeader = "";
		}

		private static void PrintLine(string line) {
			Console.WriteLine(line);
			PrintToPrinter(line);
		}

		private static void Calculate() {
			FinishLine();
			PrintLine("= " + total.ToString(decimalFormat));
			Clear();
		}

		private static void Subtotal() {
			Plus();
			PrintLine("Subtotal: " + total.ToString(decimalFormat));
		}

		private static void Cut() {
			Console.WriteLine();
			Console.WriteLine();
			PrintToPrinter(bytes: CutBytes);
		}

		public static void ClearConsoleLines(int count = 1) {
			int line = Console.CursorTop;
			for (int i = 0; i < count; i++) {
				Console.SetCursorPosition(0, line + i);
				Console.Write(new string(' ', Console.WindowWidth));
			}
			Console.SetCursorPosition(0, line);
		}

		public static void WriteNextLine(string text) {
			var position = new { Console.CursorTop, Console.CursorLeft };
			Console.SetCursorPosition(0, Console.CursorTop + 1);
			ClearConsoleLines();
			Console.Write(text);
			Console.SetCursorPosition(position.CursorLeft, position.CursorTop);
		}

		private static void PrintToPrinter(string str = null, byte[] bytes = null) {
			try {
				using (var port = new SerialPort(Settings.Default.Port)) {
					port.Open();
					port.WriteTimeout = 200;
					if (str != null) {
						port.WriteLine(str);
					}
					if (bytes != null) {
						port.Write(bytes, 0, bytes.Length);
					}
				}
			} catch (Exception e) {
				Console.Error.WriteLine();
				Console.Error.WriteLine(e.Message);
			}
		}

		[Flags]
		private enum State {
			Clean = 0,
			ShouldCut = 1,
			ShouldCalculate = 2,
			ShouldCalculateLine = 4
		}
	}
}
