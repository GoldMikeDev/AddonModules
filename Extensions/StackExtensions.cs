namespace Rename.AddonModules.Extensions
{
	static class StackExtensions
	{
		public static T Current<T>(this Stack<T> stack) { return stack.Peek(); }
		public static T GoUp<T>(this Stack<T> stack) { return stack.Pop(); }
		public static void GoDown<T>(this Stack<T> stack, T item) { stack.Push(item); }
	}
}