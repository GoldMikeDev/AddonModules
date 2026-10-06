namespace Rename.AddonModules.Extensions
{
	static class DirectoryExtensions
	{
		internal enum SpecialDirectory
		{
			desktop = 0,
			programs = 2,
			myDocuments = 5, // aka Personal
			favorites = 6,
			startup = 7,
			recent = 8,
			sendTo = 9,
			startMenu = 11,
			myMusic = 13,
			myVideos = 14,
			desktopDirectory = 16,
			myComputer = 17,
			networkShortcuts = 19,
			fonts = 20,
			templates = 21,
			commonStartMenu = 22,
			commonPrograms = 23,
			commonStartup = 24,
			commonDesktopDirectory = 25,
			applicationData = 26,
			printerShortcuts = 27,
			localApplicationData = 28,
			internetCache = 32,
			cookies = 33,
			history = 34,
			commonApplicationData = 35,
			windows = 36,
			system = 37,
			programFiles = 38,
			myPictures = 39,
			userProfile = 40,
			systemx86 = 41,
			programFilesx86 = 42,
			commonProgramFiles = 43,
			commonProgramFilesx86 = 44,
			commonTemplates = 45,
			commonDocuments = 46,
			commonAdminTools = 47,
			adminTools = 48,
			commonMusic = 53,
			commonPictures = 54,
			commonVideos = 55,
			resources = 56,
			localizedResources = 57,
			commonOemLinks = 58,
			cdBurning = 59
		}
		internal static string GetCurrentDirectoryName() => Path.GetFileName(Directory.GetCurrentDirectory());
		internal static string GetCurrentDirectoryPath() => Directory.GetCurrentDirectory();
		internal static string GetSpecialDirectoryPath(SpecialDirectory dir) => Environment.GetFolderPath((Environment.SpecialFolder)dir);
	}
}