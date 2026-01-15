namespace L2D; 

public static class EditHistory {

	private static string currentContext;
	private static Stack<EditEntry> stack;

	public static void Push(string context, Action<object?> reverse, object? data = null) {
		stack.Push(new EditEntry(context, reverse, data));
		currentContext = context;
	}

	public static void Pop() {
		if(stack.Count == 0) return;
		var entry = stack.Pop();
		currentContext = entry.Context;
		entry.Reverse?.Invoke(entry.Data);
		Program.File.MarkDirty();
	}

	public static EditEntry Peek() {
		if(stack.Count == 0) return null;
		return stack.Peek();
	}

	public static void Clear() {
		currentContext = "";
		stack.Clear();
	}

	public static bool WillPopChangeContext() {
		if(stack.Count == 0) return false;
		return currentContext != stack.Peek().Context;
	}

	public static void SetContext(string context) {
		currentContext = context;
	}
	
}

public class EditEntry {
	public string Context => context;
	public Action<object?> Reverse => reverse;
	public object? Data => data;
	private string context;
	private Action<object?> reverse;
	private object? data;
	public EditEntry(string context, Action<object?> reverse, object? data) {
		this.context = context;
		this.reverse = reverse;
		this.data = data;
	}
}