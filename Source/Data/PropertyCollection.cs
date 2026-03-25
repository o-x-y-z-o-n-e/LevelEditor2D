using System.Xml.Linq;
using IconFonts;
using ImGuiNET;

namespace L2D;

public class PropertyCollection {

	public int Count => properties.Count;

	public IEnumerable<Property> All => properties;
	
	private List<Property> properties;
	
	public PropertyCollection() {
		properties = new();
	}
	
	public Property Get(int index) {
		return properties[index];
	}
	
	public Property Get(string name) {
		foreach(var entry in properties) {
			if(entry.Name == name) return entry;
		}
		return null;
	}
	
	public bool Get(string name, out Property property) {
		foreach(var entry in properties) {
			if(entry.Name == name) {
				property = entry;
				return true;
			}
		}
		property = null;
		return false;
	}

	public bool Contains(string name) {
		foreach(var entry in properties) {
			if(entry.Name == name) {
				return true;
			}
		}
		return false;
	}

	public Property Add(string name, PropertyType type) {
		if(Get(name, out var p)) {
			return p;
		}
		Property property = new Property();
		property.Name = name;
		property.Type = type;
		properties.Add(property);
		return property;
	}

	public void Insert(Property property, int index) {
		if(properties.Contains(property)) return;
		properties.Insert(index, property);
	}

	public int IndexOf(Property property) => properties.IndexOf(property);

	public bool Move(Property property, int index) {
		if(index < 0 || index >= properties.Count) return false;
		if(properties[index] == property) return true;
		int srcIndex = properties.IndexOf(property);
		properties[srcIndex] = properties[index];
		properties[index] = property;
		return true;
	}
	
	public bool Move(int indexSrc, int indexDst) {
		if(indexDst < 0 || indexDst >= properties.Count) return false;
		if(indexSrc < 0 || indexSrc >= properties.Count) return false;
		if(indexSrc == indexDst) return true;
		var temp = properties[indexDst];
		properties[indexDst] = properties[indexSrc];
		properties[indexSrc] = temp;
		return true;
	}

	public void Remove(int index) => properties.RemoveAt(index);
	
	public bool Remove(Property property) => properties.Remove(property);
	
	public bool RemoveFirst(string name) => properties.Remove(properties.Find(p => p.Name == name));

	public bool RemoveAll(string name) => properties.RemoveAll(p => p.Name == name) > 0;

	public void Clear() => properties.Clear();

	public void CopyTo(PropertyCollection collection) {
		foreach(var srcProperty in properties) {
			var dstProperty = collection.Add(srcProperty.Name, srcProperty.Type);
			switch(srcProperty.Type) {
				case PropertyType.String:
					dstProperty.String = srcProperty.String;
					break;
				case PropertyType.Integer:
					dstProperty.Integer = srcProperty.Integer;
					break;
				case PropertyType.Float:
					dstProperty.Float = srcProperty.Float;
					break;
				case PropertyType.Boolean:
					dstProperty.Boolean = srcProperty.Boolean;
					break;
			}
		}
	}

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
			if(Contains(name)) continue;
			Property property = new Property();
			property.Name = name;
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

public class Property {
	public string Name;
	public PropertyType Type;
	public string String;
	public int Integer;
	public float Float;
	public bool Boolean;
	public Property() {
		Name = "";
		Type = PropertyType.String;
		String = "";
		Integer = 0;
		Float = 0.0F;
		Boolean = false;
	}
	
	public class MoveOperation : IFileEditOperation {
		private PropertyCollection collection;
		private int index1;
		private int index2;
		public MoveOperation(PropertyCollection collection, int index1, int index2) {
			this.collection = collection;
			this.index1 = index1;
			this.index2 = index2;
		}
		public void ApplyNextState(FileEditEntry entry) {
			var op = entry.GetData<MoveOperation>();
			op.collection.Move(op.index1, op.index2);
		}
		public void ApplyPrevState(FileEditEntry entry) {
			var op = entry.GetData<MoveOperation>();
			op.collection.Move(op.index2, op.index1);
		}
		public bool HasChanges() => index1 != index2;
	}

	public class AddOperation : IFileEditOperation {
		private PropertyCollection collection;
		private Property property;
		public AddOperation(PropertyCollection collection, Property property) {
			this.collection = collection;
			this.property = property;
		}
		public void ApplyNextState(FileEditEntry entry) {
			var op = entry.GetData<AddOperation>();
			op.collection.Insert(op.property, op.collection.Count);
		}
		public void ApplyPrevState(FileEditEntry entry) {
			var op = entry.GetData<AddOperation>();
			op.collection.Remove(op.collection.Count - 1);
		}
		public bool HasChanges() => true;
	}

	public class RemoveOperation : IFileEditOperation {
		private PropertyCollection collection;
		private Property property;
		private int index;
		public RemoveOperation(PropertyCollection collection, Property property) {
			this.collection = collection;
			this.property = property;
			this.index = collection.IndexOf(property);
		}
		public void ApplyNextState(FileEditEntry entry) {
			var op = entry.GetData<RemoveOperation>();
			op.collection.Remove(op.index);
		}
		public void ApplyPrevState(FileEditEntry entry) {
			var op = entry.GetData<RemoveOperation>();
			op.collection.Insert(op.property, op.index);
		}
		public bool HasChanges() => true;
	}

	public class ConvertOperation : IFileEditOperation {
		
		private Property property;
		private PropertyType oldType;
		private PropertyType newType;
		private string oldString;
		private int oldInteger;
		private float oldFloat;
		private bool oldBoolean;
		private string newString;
		private int newInteger;
		private float newFloat;
		private bool newBoolean;

		public void ApplyNextState(FileEditEntry entry) {
			var op = entry.GetData<ConvertOperation>();
			op.property.Type = op.newType;
			switch(op.newType) {
				case PropertyType.String:
					op.property.String = op.newString;
					break;
				case PropertyType.Integer:
					op.property.Integer = op.newInteger;
					break;
				case PropertyType.Float:
					op.property.Float = op.newFloat;
					break;
				case PropertyType.Boolean:
					op.property.Boolean = op.newBoolean;
					break;
			}
		}
		
		public void ApplyPrevState(FileEditEntry entry) {
			var op = entry.GetData<ConvertOperation>();
			op.property.Type = op.oldType;
			op.property.String = op.oldString;
			op.property.Integer = op.oldInteger;
			op.property.Float = op.oldFloat;
			op.property.Boolean = op.oldBoolean;
		}
		
		public bool HasChanges() => oldType != newType;
		
		public ConvertOperation(Property property, string newString) {
			this.property = property;
			this.oldType = property.Type;
			this.oldString = property.String;
			this.oldInteger = property.Integer;
			this.oldFloat = property.Float;
			this.oldBoolean = property.Boolean;
			this.newType = PropertyType.String;
			this.newString = newString;
			this.newInteger = property.Integer;
			this.newFloat = property.Float;
			this.newBoolean = property.Boolean;
		}
		
		public ConvertOperation(Property property, int newInteger) {
			this.property = property;
			this.oldType = property.Type;
			this.oldString = property.String;
			this.oldInteger = property.Integer;
			this.oldFloat = property.Float;
			this.oldBoolean = property.Boolean;
			this.newType = PropertyType.Integer;
			this.newInteger = newInteger;
			this.newString = property.String;
			this.newFloat = property.Float;
			this.newBoolean = property.Boolean;
		}
		
		public ConvertOperation(Property property, float newFloat) {
			this.property = property;
			this.oldType = property.Type;
			this.oldString = property.String;
			this.oldInteger = property.Integer;
			this.oldFloat = property.Float;
			this.oldBoolean = property.Boolean;
			this.newType = PropertyType.Float;
			this.newFloat = newFloat;
			this.newString = property.String;
			this.newInteger = property.Integer;
			this.newBoolean = property.Boolean;
		}
		
		public ConvertOperation(Property property, bool newBoolean) {
			this.property = property;
			this.oldType = property.Type;
			this.oldString = property.String;
			this.oldInteger = property.Integer;
			this.oldFloat = property.Float;
			this.oldBoolean = property.Boolean;
			this.newType = PropertyType.Boolean;
			this.newBoolean = newBoolean;
			this.newString = property.String;
			this.newInteger = property.Integer;
			this.newFloat = property.Float;
		}
		
	}
}

public enum PropertyType {
	String,
	Integer,
	Float,
	Boolean
}