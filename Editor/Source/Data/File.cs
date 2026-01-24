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

	internal File(string path) {
		this.path = Path.GetFullPath(path).Replace('\\', '/');
		this.world = null;
		this.dirty = false;
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

}

public class Property {
	public string Name;
	public PropertyType Type;
	public string String;
	public int Integer;
	public float Float;
	public bool Boolean;
}

public class PropertyCollection {
	private List<Property> properties;
	
	public PropertyCollection() {
		properties = new();
	}
	
	public Property Get(string name) {
		foreach(var entry in properties) {
			if(entry.Name == name) return entry;
		}
		return null;
	}

	public Property Add(string name, PropertyType type) {
		Property property = Get(name);
		if(property != null) {
			property.Type = type;
			return property;
		}
		property = new Property();
		property.Type = type;
		properties.Add(property);
		return property;
	}

	public bool Rename(string name, string newName) {
		Property property = Get(name);
		if(property == null) return false;
		foreach(var entry in properties) {
			if(entry.Name == newName) return false;
		}
		property.Name = newName;
		return true;
	}

	public bool Remove(string name) => properties.RemoveAll(p => p.Name == name) > 0;

	public void Clear() => properties.Clear();

	public void SerializeToElement(XElement element) {
		foreach(var entry in properties) {
			var e = new XElement("property");
			e.Add(new XAttribute("name", entry.Name));
			e.Add(new XAttribute("type", entry.Type.ToString().ToLower()));
			switch(entry.Type) {
				case PropertyType.String:
					e.Add(new XAttribute("value", entry.String));
					break;
				case PropertyType.Integer:
					e.Add(new XAttribute("value", entry.Integer));
					break;
				case PropertyType.Float:
					e.Add(new XAttribute("value", entry.Float));
					break;
				case PropertyType.Boolean:
					e.Add(new XAttribute("value", entry.Boolean));
					break;
			}
			element.Add(e);
		}
	}
	
	public void ParseFromElement(XElement element) {
		foreach(var e in element.Elements("property")) {
			string name = e.Attribute("name")?.Value ?? "";
			if(name == "") continue;
			Property property = new Property();
			string type = e.Attribute("type")?.Value ?? "string";
			string value = e.Attribute("value")?.Value ?? "";
			if(type == "integer") {
				property.Type = PropertyType.Integer;
				int.TryParse(value, out property.Integer);
			} else if(type == "float") {
				property.Type = PropertyType.Float;
				float.TryParse(value, out property.Float);
			} else if(type == "boolean") {
				property.Type = PropertyType.Boolean;
				bool.TryParse(value, out property.Boolean);
			} else {
				property.Type = PropertyType.String;
				property.String = value;
			}
			properties.Add(property);
		}
	}
}

public enum PropertyType {
	String,
	Integer,
	Float,
	Boolean
}