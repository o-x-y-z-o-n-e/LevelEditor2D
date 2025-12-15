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
			Parse(document, path);
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

	private void Parse(XDocument doc, string dir) {
		world = new World();
		world.Parse(doc.Root, dir);
	}

	public bool Write() {
		// TODO
		return false;
	}

	public static int ParseAsInt(XAttribute? attr, int defaultValue = 0) {
		if(attr == null) return defaultValue;
		if(int.TryParse(attr.Value, out int value)) {
			return value;
		} else {
			return defaultValue;
		}
	}
	
	public static float ParseAsFloat(XAttribute? attr, float defaultValue = 0) {
		if(attr == null) return defaultValue;
		if(float.TryParse(attr.Value, out float value)) {
			return value;
		} else {
			return defaultValue;
		}
	}
	
	public static bool ParseAsBool(XAttribute? attr, bool defaultValue = false) {
		if(attr == null) return defaultValue;
		string str = attr.Value.ToLower();
		if(defaultValue) {
			if(str == "false" || str == "0") {
				return false;
			}
		} else {
			if(str == "true" || str == "1") {
				return true;
			}
		}
		return defaultValue;
	}
	
}