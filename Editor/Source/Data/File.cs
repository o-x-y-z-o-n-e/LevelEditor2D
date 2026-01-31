using System;
using System.Linq;
using System.Text;
using System.Xml;
using System.Xml.Linq;

namespace L2D;

public class File {

	public World World => world;
	
	public bool UnsavedChanges => dirty;

	private string path;
	private World world;
	private bool dirty;
	private bool writingToDisk;

	private FileSystemWatcher watcher;

	internal File(string path) {
		this.path = Path.GetFullPath(path).Replace('\\', '/');
		world = null;
		dirty = false;
		writingToDisk = false;
		watcher = new FileSystemWatcher(Path.GetDirectoryName(this.path));
		watcher.Filter = "*.l2d";
		watcher.EnableRaisingEvents = true;
		watcher.Changed += OnChanged;
	}

	public bool Read() {
		FileStream stream = null;
		try {
			stream = new FileStream(path, FileMode.Open, FileAccess.ReadWrite);
			XDocument document = XDocument.Load(stream);
			UnmarkDirty();
			Parse(document);
			stream.Close();
			return true;
		} catch(Exception e) {
			Console.ForegroundColor = ConsoleColor.Red;
			Console.WriteLine($"Failed to load file: {path}\nError: {e}");
			Console.ForegroundColor = ConsoleColor.White;
			stream?.Close();
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
			writingToDisk = true;
			document.Save(writer);
			writer.Close();
			stream.Close();
			writingToDisk = false;
			return true;
		} catch(Exception e) {
			Console.ForegroundColor = ConsoleColor.Red;
			Console.WriteLine($"Failed to write file: {path}\nError: {e}");
			Console.ForegroundColor = ConsoleColor.White;
			writer?.Close();
			stream?.Close();
			return false;
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
	}
	
	private void OnChanged(object sender, FileSystemEventArgs e) {
		if(e.ChangeType != WatcherChangeTypes.Changed) {
			return;
		}
		if(writingToDisk) return;
		Console.WriteLine($"Changed: {e.FullPath}");
	}

}