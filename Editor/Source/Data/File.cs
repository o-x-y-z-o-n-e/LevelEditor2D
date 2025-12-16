using System;
using System.Linq;
using System.Xml.Linq;

namespace L2D;

public class File {

	public World World => world;

	private string path;
	private World world;

	internal File(string path) {
		this.path = path;
	}

	public bool Read() {
		FileStream stream = null;
		try {
			stream = new FileStream(path, FileMode.Open, FileAccess.ReadWrite);
			XDocument document = XDocument.Load(stream);
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
		world = new World(this);
		world.Parse(doc.Root);
	}

	public bool Write() {
		// TODO
		return false;
	}

	public string GetAbsolutePath(string localPath) {
		return Path.GetFullPath(localPath, Path.GetFullPath(Path.GetDirectoryName(path)));
	}
	
	public string GetRelativePath(string fullPath) {
		return Path.GetRelativePath(Path.GetDirectoryName(path), fullPath);
	}
	
}