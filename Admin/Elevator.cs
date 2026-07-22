using Microsoft.Win32.SafeHandles;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security;
using Windows.Win32.Security;
using static Windows.Win32.PInvoke;
namespace Rename.AddonModules.Admin
{
    /* 
    Ensure the following exist in .csproj to use:
        <PropertyGroup>
    	    <CompilerGeneratedFilesOutputPath>generated</CompilerGeneratedFilesOutputPath>
		    <EmitCompilerGeneratedFiles>true</EmitCompilerGeneratedFiles>
    	</PropertyGroup>
        <ItemGroup>
		    <Compile Remove="generated\**\*.cs" />
		    <AdditionalFiles Include="AddonModules\Admin\NativeMethods.txt" />
		    <None Include="generated\**\*.cs" Visible="true" />
        </ItemGroup>
    */
    public static partial class Elevator
    {
        public static bool CLIcredentials(out string? user, out SecureString? pass)
        {
            bool credentials = false;
            user = null;
            pass = new();
            if (!ProcessElevated())
            {
            CredentialQuery:
                Console.WriteLine(" ⚠️ Enter admin credentials? (Y/N): ");
                string? input = Console.ReadLine();
                if (input?.Trim().ToUpper() == "Y")
                {
                CredentialInput:
                    Console.WriteLine(" 🔒 Enter username: ");
                    user = Console.ReadLine();
                    Console.WriteLine(" 🔒 Enter password: ");
                    while (true)
                    {
                        var key = Console.ReadKey(intercept: true);
                        if (key.Key == ConsoleKey.Enter) { break; }
                        pass.AppendChar(key.KeyChar);
                    }
                CredentialConfirm:
                    Console.WriteLine(" ❓ Have credentials been entered correctly? (Y/N): ");
                    string? confirm = Console.ReadLine();
                    if (confirm?.Trim().ToUpper() == "Y") { credentials = true; }
                    else if (confirm?.Trim().ToUpper() == "N") { goto CredentialInput; }
                    else { goto CredentialConfirm; }
                }
                else if (input?.Trim().ToUpper() == "N") { pass.Dispose(); return credentials; }
                else { goto CredentialQuery; }
            }
            return credentials;
        }
        private static bool ProcessElevated()
        {
            if (!OperatingSystem.IsWindowsVersionAtLeast(5, 1, 2600)) throw new PlatformNotSupportedException("Requires Windows XP SP2 or later.");
            bool elevation = false;
            Span<byte> tokenElevationBuffer = stackalloc byte[Marshal.SizeOf<TOKEN_ELEVATION>()];
            bool openProcess = OpenProcessToken(Process.GetCurrentProcess().SafeHandle, TOKEN_ACCESS_MASK.TOKEN_QUERY, out SafeFileHandle tokenHandle);
            if (!openProcess) { throw new Exception("❌ Failed to open process token."); }
            using (tokenHandle)
            {
                bool tokenInformation = GetTokenInformation(tokenHandle, TOKEN_INFORMATION_CLASS.TokenElevation, tokenElevationBuffer, out _);
                if (!tokenInformation) { throw new Exception("❌ Failed to get token information."); }
                elevation = MemoryMarshal.Read<TOKEN_ELEVATION>(tokenElevationBuffer).TokenIsElevated != 0;
            }
            return elevation;
        }
    }
}