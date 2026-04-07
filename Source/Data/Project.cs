using System;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using Serilog;

namespace E2D;

public class Project {

	public World World => world;
	
	public bool UnsavedChanges => dirty;

	private string path;
	private World world;
	private bool dirty;
	
	private object? editContext;
	private object? dataContext;
	private List<FileEditEntry> editStack;
	private int editPointer;

	private FileSystemWatcher watcher;

	private List<string> filesToDeleteOnSave;

	internal Project(string path) {
		this.path = Path.GetFullPath(path).Replace('\\', '/');
		world = null;
		dirty = false;
		watcher = new FileSystemWatcher(Path.GetDirectoryName(this.path));
		watcher.NotifyFilter = NotifyFilters.LastWrite;
		watcher.Filter = $"*.{World.FILE_EXTENSION}";
		watcher.EnableRaisingEvents = true;
		watcher.Changed += OnChanged;
		editContext = null;
		dataContext = null;
		editStack = new List<FileEditEntry>();
		editPointer = 0;
		filesToDeleteOnSave = new();
	}

	public bool Read() {
		Log.Information("Reading project file... [{@path}]", path);
		try {
			ClearEditHistory();
			string contents = File.ReadAllText(path);
			XDocument document = XDocument.Parse(contents);
			UnmarkDirty();
			Parse(document);
			return true;
		} catch(Exception e) {
			Log.Error(e, "Failed to read project file!");
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
		watcher.EnableRaisingEvents = false;
		if(filesToDeleteOnSave.Count > 0) {
			Log.Information("Deleting old files...");
			try {
				foreach(string filePath in filesToDeleteOnSave) {
					if(File.Exists(filePath)) {
						Log.Information(filePath);
						File.Delete(filePath);
					}
				}
				filesToDeleteOnSave.Clear();
			} catch(Exception e) {
				Log.Error(e, "Failed to delete old files");
			}
		}
		Log.Information("Writing project file... [{@path}]", path);
		try {
			StringBuilder builder = new StringBuilder();
			XDocument document = new XDocument();
			UnmarkDirty();
			Serialize(document);
			XmlWriterSettings settings = new XmlWriterSettings();
			settings.OmitXmlDeclaration = true;
			settings.CloseOutput = false;
			settings.Indent = true;
			writer = XmlTextWriter.Create(builder, settings);
			document.Save(writer);
			writer.Close();
			File.WriteAllText(path, builder.ToString());
			return true;
		} catch(Exception e) {
			Log.Error(e, "Failed to write project file!");
			writer?.Close();
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

	public void DeleteFileOnSave(string filePath) {
		filesToDeleteOnSave.Add(filePath);
	}

	public void DontDeleteFileOnSave(string filePath) {
		filesToDeleteOnSave.RemoveAll(path => path == filePath);
	}

	public string GetAbsolutePath() => path;

	public string GetAbsolutePath(string localPath) {
		return Path.GetFullPath(localPath, Path.GetDirectoryName(path)).Replace('\\', '/');
	}
	
	public string GetAbsolutePath(string localPath, string basePath) {
		return Path.GetFullPath(localPath, Path.GetDirectoryName(basePath)).Replace('\\', '/');
	}
	
	public string GetRelativePath(string fullPath) {
		return Path.GetRelativePath(Path.GetDirectoryName(path), fullPath).Replace('\\', '/');
	}
	
	public string GetRelativePath(string fullPath, string relativeTo) {
		return Path.GetRelativePath(Path.GetDirectoryName(relativeTo), fullPath).Replace('\\', '/');
	}
	
	public string GetCombinedPath(params string[] paths) {
		return Path.Combine(paths).Replace('\\', '/');
	}

	public string GetDirectoryName(string path) {
		return Path.GetDirectoryName(path).Replace('\\', '/');
	}

	public string GetFileName(string path) {
		return Path.GetFileName(path);
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
	
	public void ApplyEdit(object? editContext, IFileEditOperation operation) {
		if(operation == null) return;
		var edit = BeginEdit(editContext, operation.Context, operation, operation.ApplyNextState, operation.ApplyPrevState, operation.GetNextStateMessage, operation.GetPrevStateMessage);
		EndEdit(ref edit);
	}

	public void ApplyEdit(object? editContext, object? dataContext, object? data, Action<FileEditEntry> redo, Action<FileEditEntry> undo, Func<string> redoMessage = null, Func<string> undoMessage = null) {
		if(redo == null || undo == null) throw new Exception("File edit needs an action & a reverse");
		var edit = BeginEdit(editContext, dataContext, data, redo, undo, redoMessage, undoMessage);
		EndEdit(ref edit);
	}
	
	public FileEditEntry BeginEdit(object? editContext, IFileEditOperation operation) {
		return new FileEditEntry(editContext, operation.Context, operation, operation.ApplyNextState, operation.ApplyPrevState, operation.GetNextStateMessage, operation.GetPrevStateMessage);
	}

	public FileEditEntry BeginEdit(object? editContext, object? dataContext, object? data, Action<FileEditEntry> redo, Action<FileEditEntry> undo, Func<string> redoMessage = null, Func<string> undoMessage = null) {
		return new FileEditEntry(editContext, dataContext, data, redo, undo, redoMessage, undoMessage);
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
		editContext = edit.EditContext;
		dataContext  = edit.DataContext;
		edit.Action.Invoke(edit);
		edit = null;
		MarkDirty();
	}

	public void Undo() {
		if(editPointer == 0) return;
		var entry = editStack[editPointer - 1];
		editPointer--;
		editContext = entry.EditContext;
		dataContext  = entry.DataContext;
		entry.Reverse.Invoke(entry);
		MarkDirty();
	}

	public void Redo() {
		if(editPointer == editStack.Count) return;
		var entry = editStack[editPointer];
		editPointer++;
		editContext = entry.EditContext;
		dataContext = entry.DataContext;
		entry.Action.Invoke(entry);
		MarkDirty();
	}

	public bool CanUndo() {
		return editPointer > 0;
	}

	public bool CanRedo() {
		return editPointer < editStack.Count;
	}

	public string GetUndoMessage() {
		if(editPointer == 0) return "";
		return editStack[editPointer - 1].UndoMessage?.Invoke() ?? "";
	}

	public string GetRedoMessage() {
		if(editPointer == editStack.Count) return "";
		return editStack[editPointer].RedoMessage?.Invoke() ?? "";
	}

	public bool WillUndoChangeContext() {
		if(editPointer == 0) return false;
		var entry = editStack[editPointer - 1];
		return editContext != entry.EditContext && dataContext != entry.DataContext;
	}
	
	public bool WillRedoChangeContext() {
		if(editPointer == editStack.Count) return false;
		var entry = editStack[editPointer];
		return editContext != entry.EditContext && dataContext != entry.DataContext;
	}

	public void ClearEditHistory() {
		editStack.Clear();
		editPointer = 0;
		editContext = null;
		dataContext = null;
	}

}

public interface IFileEditOperation {
	object? Context { get; }
	void ApplyNextState(FileEditEntry entry);
	void ApplyPrevState(FileEditEntry entry);
	bool HasChanges();
	string GetNextStateMessage();
	string GetPrevStateMessage();
}

public class FileEditEntry {
	public object? EditContext => editContext;
	public object? DataContext => dataContext;
	public Action<FileEditEntry> Action => action;
	public Action<FileEditEntry> Reverse => reverse;
	public Func<string> RedoMessage => redoMessage;
	public Func<string> UndoMessage => undoMessage;
	private object? editContext;
	private object? dataContext;
	private object? data;
	private Action<FileEditEntry> action;
	private Action<FileEditEntry> reverse;
	private Func<string> redoMessage;
	private Func<string> undoMessage;
	public FileEditEntry(object? editContext, object? dataContext, object? data, Action<FileEditEntry> action, Action<FileEditEntry> reverse, Func<string> redoMessage, Func<string> undoMessage) {
		this.editContext = editContext;
		this.dataContext = dataContext;
		this.data = data;
		this.action = action;
		this.reverse = reverse;
		this.redoMessage = redoMessage;
		this.undoMessage = undoMessage;
	}
	public T? GetData<T>() => (T?)data;
}