using System.Numerics;
using System.Xml.Linq;

namespace L2D;

public class EntityCollection {

	public IEnumerable<EntityDefinition> All => entities;

	private List<EntityDefinition> entities;

	public EntityCollection() {
		entities = new();
	}
	
	public void SerializeToElement(XElement element) {
		foreach(var entity in entities) {
			var e = new XElement("entity");
			e.Add(new XAttribute("name", entity.Name));
			e.Add(new XAttribute("type", entity.Type));
			e.Add(new XAttribute("position.x", entity.Position.X));
			e.Add(new XAttribute("position.y", entity.Position.Y));
			e.Add(new XAttribute("size.x", entity.Size.X));
			e.Add(new XAttribute("size.y", entity.Size.Y));
			entity.Properties.SerializeToElement(e);
			element.Add(e);
		}
	}
	
	public void ParseFromElement(XElement element) {
		foreach(var e in element.Elements("entity")) {
			string name = e.Attribute("name").ParseAsString();
			string type = e.Attribute("type").ParseAsString();
			float px = e.Attribute("position.x").ParseAsFloat();
			float py = e.Attribute("position.y").ParseAsFloat();
			float sx = e.Attribute("size.x").ParseAsFloat();
			float sy = e.Attribute("size.y").ParseAsFloat();
			EntityDefinition entity = new EntityDefinition();
			entity.Name = name;
			entity.Type = type;
			entity.Position = new(px, py);
			entity.Size = new(sx, sy);
			entity.Properties.ParseFromElement(e);
			entities.Add(entity);
		}
	}

}

public class EntityDefinition {

	public string Name;
	public string Type;
	public Vector2 Position;
	public Vector2 Size;

	public PropertyCollection Properties => properties;

	private PropertyCollection properties;

	public EntityDefinition() {
		properties = new();
	}

}