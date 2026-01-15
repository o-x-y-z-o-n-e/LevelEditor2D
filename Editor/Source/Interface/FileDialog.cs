using ImGuiNET;
using NativeFileDialogSharp;

namespace L2D;

public class FileDialog {

	private bool active;
	private bool finished;
	private string defaultPath;
	private string filter;
	private bool open;
	private Action<string> callback;
	private DialogResult result;
	
	private static FileDialog instance = new FileDialog();

	public static void Save(string defaultPath, string filter, Action<string> onResult) {
		lock(instance) {
			if(instance.active) return;
			instance.active = true;
			instance.finished = false;
			instance.defaultPath = defaultPath;
			instance.filter = filter;
			instance.callback = onResult;
			instance.open = false;
		}
		Program.AllowInput(false);
		Thread myThread = new Thread(new ThreadStart(Internal));
		myThread.Start();
	}
	
	public static void Open(string defaultPath, string filter, Action<string> onResult) {
		lock(instance) {
			if(instance.active) return;
			instance.active = true;
			instance.finished = false;
			instance.defaultPath = defaultPath;
			instance.filter = filter;
			instance.callback = onResult;
			instance.open = true;
		}
		Program.AllowInput(false);
		Thread myThread = new Thread(new ThreadStart(Internal));
		myThread.Start();
	}

	private static void Internal() {
		string defaultPath;
		string filter;
		lock(instance) {
			defaultPath = instance.defaultPath;
			filter = instance.filter;
		}
		var result = instance.open ? Dialog.FileOpen(filter, defaultPath) : Dialog.FileSave(filter, defaultPath);
		lock(instance) {
			instance.result = result;
			instance.active = false;
			instance.finished = true;
		}
	}

	internal static void CompleteThreads() {
		lock(instance) {
			if(instance.finished) {
				instance.finished = false;
				if(instance.result.IsOk) {
					instance.callback?.Invoke(instance.result.Path);
				} else {
					instance.callback?.Invoke(null);
				}
				Program.AllowInput(true);
			}
		}
	}
	
}