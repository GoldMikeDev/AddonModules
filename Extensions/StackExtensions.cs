namespace Rename.AddonModules.Extensions
{
	static class StackExtensions
	{
		internal static T Current<T>(this Stack<T> stack) { return stack.Peek(); }
		internal static T GoUp<T>(this Stack<T> stack) { return stack.Pop(); }
		internal static void GoDown<T>(this Stack<T> stack, T item) { stack.Push(item); }
	}
}