using Rename.AddonModules;
namespace Rename.AddonModules.ToolBox
{
	class ToolBoxHandshake
	{
		public static bool VerifyToolBoxHost()
		{
			const string Sentinel = "🔍 Verifying parent is ToolBox...";
			var isToolBox = string.Equals(Environment.GetEnvironmentVariable("TOOLBOX_HOST"), "1", StringComparison.Ordinal);
			var prefix = (!Console.IsOutputRedirected && isToolBox) ? (Environment.GetEnvironmentVariable("TOOLBOX_PREFIX") ?? " 🧰 > ") : "";
			Console.WriteLine(prefix + Sentinel);
			if (isToolBox)
			{
				Console.WriteLine(prefix + "✅ ToolBox detected.");
				return true;
			}
			using var spinner = new ConsoleSpinner(new Lock(), "");
			if (!Console.IsOutputRedirected) spinner.Start("⏳ Waiting for ToolBox");
			var end = Environment.TickCount64 + 5000;
			Task<string?> readTask = Task.Run(Console.ReadLine);
			while (Environment.TickCount64 < end)
			{
				var remaining = (int)Math.Max(0, end - Environment.TickCount64);
				if (readTask.Wait(remaining))
				{
					var resp = (readTask.Result ?? "").Trim().TrimStart('\uFEFF');
					if (string.Equals(resp, "ToolBox is open", StringComparison.Ordinal))
					{
						spinner.StopAndFlush();
						Console.WriteLine("✅ ToolBox detected.");
						return true;
					}
				}
				Thread.Sleep(10);
			}
			spinner.StopAndFlush();
			Console.WriteLine("❌ ToolBox required to use this tool.");
			return false;
		}
	}
}