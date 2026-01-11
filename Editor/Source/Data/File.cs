using System;
using System.Linq;
using System.Text;
using System.Xml;
using System.Xml.Linq;

namespace L2D;

public class File {

	public World World => world;

	private string path;
	private World world;
	private bool dirty;

	internal File(string path) {
		this.path = path;
	}

	public bool Read() {
		FileStream stream = null;
		try {
			stream = new FileStream(path, FileMode.Open, FileAccess.ReadWrite);
			XDocument document = XDocument.Load(stream);
			dirty = false;
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
			dirty = false;
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
			Console.ForegroundColor = ConsoleColor.Red;
			Console.WriteLine($"Failed to write file: {path}\nError: {e}");
			Console.ForegroundColor = ConsoleColor.White;
			writer?.Close();
			stream?.Close();
			return false;
		}
	}

	private void Serialize(XDocument doc) {
		doc.Add(world.Serialize());
	}

	public string GetAbsolutePath(string localPath) {
		return Path.GetFullPath(localPath, Path.GetFullPath(Path.GetDirectoryName(path)));
	}
	
	public string GetRelativePath(string fullPath) {
		return Path.GetRelativePath(Path.GetDirectoryName(path), fullPath);
	}

	public void MarkDirty() {
		dirty = true;
	}
	
}