using System;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using Serilog;

namespace L2D;

public class File {

	public World World => world;
	
	public bool UnsavedChanges => dirty;

	private string path;
	private World world;
	private bool dirty;
	
	private object? editContext;
	private List<FileEditEntry> editStack;
	private int editPointer;

	private FileSystemWatcher watcher;

	internal File(string path) {
		this.path = Path.GetFullPath(path).Replace('\\', '/');
		world = null;
		dirty = false;
		watcher = new FileSystemWatcher(Path.GetDirectoryName(this.path));
		watcher.NotifyFilter = NotifyFilters.LastWrite;
		watcher.Filter = "*.l2d";
		watcher.EnableRaisingEvents = true;
		watcher.Changed += OnChanged;
		editContext = "";
		editStack = new List<FileEditEntry>();
		editPointer = 0;
	}

	public bool Read() {
		FileStream stream = null;
		Log.Information("Reading file... [{@path}]", path);
		try {
			ClearEditHistory();
			stream = new FileStream(path, FileMode.Open, FileAccess.ReadWrite);
			XDocument document = XDocument.Load(stream);
			UnmarkDirty();
			Parse(document);
			stream.Close();
			return true;
		} catch(Exception e) {
			Log.Error(e, "Failed to read project file: {@path}", path);
			stream?.Close();
			New();
			return false;
		}
	}

	private void Parse(XDocument doc) {
		world?.Dispose();
		world = new World(this);
		world.Parse(doc.Root);
	}

	public bool Write() {
		XmlWriter writer = null;
		FileStream stream = null;
		watcher.EnableRaisingEvents = false;
		Log.Information("Writing file... [{@path}]", path);
		try {
			stream = System.IO.File.Create(path);
			XDocument document = new XDocument();
			UnmarkDirty();
			Serialize(document);
			XmlWriterSettings settings = new XmlWriterSettings();
			settings.OmitXmlDeclaration = true;
			settings.CloseOutput = false;
			settings.Indent = true;
			writer = XmlTextWriter.Create(stream, settings);
			document.Save(writer);
			writer.Close();
			stream.Close();
			return true;
		} catch(Exception e) {
			Log.Error(e, "Failed to write project file: {@path}", path);
			writer?.Close();
			stream?.Close();
			return false;
		} finally {
			watcher.EnableRaisingEvents = true;
		}
	}

	public void New() {
		world?.Dispose();
		world = new World(this);
	}

	private void Serialize(XDocument doc) {
		doc.Add(world.Serialize());
	}

	public string GetPath(string localPath) {
		return Path.GetFullPath(localPath, Path.GetDirectoryName(path)).Replace('\\', '/');
	}

	public string GetPath() => path;
	
	public string GetRelativePath(string fullPath) {
		return Path.GetRelativePath(Path.GetDirectoryName(path), fullPath).Replace('\\', '/');
	}

	public void SetPath(string path) {
		this.path = Path.GetFullPath(path).Replace('\\', '/');
		watcher.Path = Path.GetDirectoryName(this.path);
		Program.UpdateWindowTitle();
	}

	public void MarkDirty() {
		if(dirty) return;
		dirty = true;
		Program.UpdateWindowTitle();
	}
	
	private void UnmarkDirty() {
		if(!dirty) return;
		dirty = false;
		Program.UpdateWindowTitle();
	}

	public string GetFileName() => Path.GetFileName(path);

	public void Dispose() {
		world?.Dispose();
		watcher?.Dispose();
	}
	
	private void OnChanged(object sender, FileSystemEventArgs e) {
		if(e.ChangeType != WatcherChangeTypes.Changed) {
			return;
		}
		string p = e.FullPath.Replace('\\', '/');
		Program.SendMessage(() => {
			if(p == path) {
				Log.Information("Detected change in file: {@path}", path);
				MarkDirty();
				Program.ReloadFileModal.Open();
			}
		});
	}

	public void ApplyEdit(object? context, object? data, Action<FileEditEntry> redo, Action<FileEditEntry> undo) {
		if(redo == null || undo == null) throw new Exception("File edit needs an action & a reverse");
		var edit = BeginEdit(context, data, redo, undo);
		EndEdit(ref edit);
	}

	public FileEditEntry BeginEdit(object? context, object? data, Action<FileEditEntry> redo, Action<FileEditEntry> undo) {
		return new FileEditEntry(context, redo, undo, data);
	}

	public void EndEdit(ref FileEditEntry edit, bool discard = false) {
		if(edit == null) return;
		if(discard) {
			edit = null;
			return;
		}
		if(editStack.Contains(edit)) return;
		if(editPointer != editStack.Count) {
			// clear the 'undone' edit history to override with new change
			editStack.RemoveRange(editPointer, editStack.Count - editPointer);
		}
		editStack.Add(edit);
		editPointer++;
		editContext = edit.Context;
		edit.Action.Invoke(edit);
		edit = null;
		MarkDirty();
	}

	public void Undo() {
		if(editPointer == 0) return;
		var entry = editStack[editPointer - 1];
		editPointer--;
		editContext = entry.Context;
		entry.Reverse.Invoke(entry);
		MarkDirty();
	}

	public void Redo() {
		if(editPointer == editStack.Count) return;
		var entry = editStack[editPointer];
		editPointer++;
		editContext = entry.Context;
		entry.Action.Invoke(entry);
		MarkDirty();
	}
	
	public void SetEditContext(object? context) {
		editContext = context;
	}

	public object? GetEditContext() {
		return editContext;
	}

	public bool WillUndoChangeContext() {
		if(editPointer == 0) return false;
		return editContext != editStack[editPointer - 1].Context;
	}
	
	public bool WillRedoChangeContext() {
		if(editPointer == editStack.Count) return false;
		return editContext != editStack[editPointer].Context;
	}

	public void ClearEditHistory() {
		editStack.Clear();
		editPointer = 0;
		editContext = "";
	}

}


public class FileEditEntry {
	public object? Context => context;
	public Action<FileEditEntry> Action => action;
	public Action<FileEditEntry> Reverse => reverse;
	private object? context;
	private Action<FileEditEntry> action;
	private Action<FileEditEntry> reverse;
	private object? data;
	public FileEditEntry(object? context, Action<FileEditEntry> action, Action<FileEditEntry> reverse, object? data) {
		this.context = context;
		this.action = action;
		this.reverse = reverse;
		this.data = data;
	}
	// internal void SetActions(Action<FileEditEntry> action, Action<FileEditEntry> reverse) {
	// 	this.action = action;
	// 	this.reverse = reverse;
	// }
	// internal void SetData(object? data) {
	// 	this.data = data;
	// }
	public T? GetData<T>() => (T?)data;
}