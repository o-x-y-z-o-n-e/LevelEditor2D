using System.Numerics;
using System.Xml.Linq;

namespace L2D;

public class EntityCollection {

	public int Count => entities.Count;
	
	public IEnumerable<Entity> All => entities;

	private List<Entity> entities;
	private Layer layer;

	public EntityCollection(Layer layer) {
		this.layer = layer;
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
			Entity entity = new Entity(layer);
			entity.Name = name;
			entity.Type = type;
			entity.Position = new(px, py);
			entity.Size = new(sx, sy);
			entity.Properties.ParseFromElement(e);
			entities.Add(entity);
		}
	}

	public int IndexOf(Entity entity) {
		return entities.IndexOf(entity);
	}

	public Entity Get(int index) {
		return entities[index];
	}

	public Entity Add() {
		Entity entity = new Entity(layer);
		entities.Add(entity);
		return entity;
	}

	public Entity Copy(int index) {
		Entity srcEntity = entities[index];
		Entity dstEntity = new Entity(layer);
		dstEntity.Name = srcEntity.Name;
		dstEntity.Type = srcEntity.Type;
		dstEntity.Position = srcEntity.Position;
		dstEntity.Size = srcEntity.Size;
		foreach(var srcProperty in srcEntity.Properties.All) {
			var dstProperty = dstEntity.Properties.Add(srcProperty.Name, srcProperty.Type);
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
		entities.Add(dstEntity);
		return dstEntity;
	}
	
	public bool Move(Entity entity, int indexDst) {
		if(indexDst < 0 || indexDst >= entities.Count) return false;
		if(entities[indexDst] == entity) return true;
		int srcIndex = entities.IndexOf(entity);
		entities[srcIndex] = entities[indexDst];
		entities[indexDst] = entity;
		return true;
	}
	
	public bool Move(int indexSrc, int indexDst) {
		if(indexDst < 0 || indexDst >= entities.Count) return false;
		if(indexSrc < 0 || indexSrc >= entities.Count) return false;
		if(indexSrc == indexDst) return true;
		var temp = entities[indexDst];
		entities[indexDst] = entities[indexSrc];
		entities[indexSrc] = temp;
		return true;
	}

	public void Remove(int index) {
		entities.RemoveAt(index);
	}

}

public class Entity {

	public const float POINT_HANDLE_SIZE = 14;

	public Layer Layer => layer;

	public bool IsPoint => Size.X == 0.0F && Size.Y == 0.0F;

	public string Name;
	public string Type;
	public Vector2 Position;
	public Vector2 Size;

	private Layer layer;

	public PropertyCollection Properties => properties;

	private PropertyCollection properties;

	public Entity(Layer layer) {
		this.layer = layer;
		properties = new();
	}

}