namespace Rename.AddonModules.Extensions
{
    static class StackExtensions
    {
        public static T Current<T>(this Stack<T> stack) { return stack.Peek(); }
        public static T GoBack<T>(this Stack<T> stack) { return stack.Pop(); }
        public static T GoDeeper<T>(this Stack<T> stack, T item) { stack.Push(item); }
    }
}