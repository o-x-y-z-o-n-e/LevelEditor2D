using System;
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
	}

	public bool Read() {
		FileStream stream = null;
		Log.Information("Reading file... [{@path}]", path);
		try {
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

}