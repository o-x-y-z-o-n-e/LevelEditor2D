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
		srcEntity.Properties.CopyTo(dstEntity.Properties);
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
	
	public void Remove(Entity entity) {
		if(entity == null || !entities.Contains(entity)) return;
		entities.Remove(entity);
	}
	
	public void Insert(Entity entity, int index) {
		if(entity == null || entities.Contains(entity)) return;
		entities.Insert(index, entity);
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
		Name = "";
		Type = "";
		Position = Vector2.Zero;
		Size = Vector2.Zero;
	}
	
	public class AddOperation : IFileEditOperation {
		private EntityCollection collection;
		private Entity entity;
		public AddOperation(EntityCollection collection, Entity entity) {
			this.collection = collection;
			this.entity = entity;
		}
		public void ApplyNextState(FileEditEntry entry) {
			var op = entry.GetData<AddOperation>();
			op.collection.Insert(op.entity, op.collection.Count);
		}
		public void ApplyPrevState(FileEditEntry entry) {
			var op = entry.GetData<AddOperation>();
			op.collection.Remove(op.entity);
			if(Program.SelectedEntity == op.entity) {
				Program.SetSelectedEntity(null);
			}
		}
		public bool HasChanges() => true;
	}

	public class MoveOperation : IFileEditOperation {
		private EntityCollection collection;
		private int index1;
		private int index2;
		public MoveOperation(EntityCollection collection, int index1, int index2) {
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
	
	public class RemoveOperation : IFileEditOperation {
		private EntityCollection collection;
		private Entity entity;
		private int index;
		public RemoveOperation(EntityCollection collection, Entity entity) {
			this.collection = collection;
			this.entity = entity;
			this.index = collection.IndexOf(entity);
		}
		public void ApplyNextState(FileEditEntry entry) {
			var op = entry.GetData<RemoveOperation>();
			op.collection.Remove(op.entity);
			if(Program.SelectedEntity == op.entity) {
				Program.SetSelectedEntity(null);
			}
		}
		public void ApplyPrevState(FileEditEntry entry) {
			var op = entry.GetData<RemoveOperation>();
			op.collection.Insert(op.entity, op.index);
		}
		public bool HasChanges() => true;
	}
	
	public class PositionOperation : IFileEditOperation {
		public Entity Entity => entity;
		private Entity entity;
		private Vector2 oldPosition;
		private Vector2 newPosition;
		public PositionOperation(Entity entity, Vector2 newPosition) {
			this.entity = entity;
			this.oldPosition = entity.Position;
			this.newPosition = newPosition;
		}
		public void SetPosition(Vector2 position) {
			newPosition = position;
		}
		public void ApplyNextState(FileEditEntry entry) {
			var op = entry.GetData<PositionOperation>();
			op.entity.Position = newPosition;
		}
		public void ApplyPrevState(FileEditEntry entry) {
			var op = entry.GetData<PositionOperation>();
			op.entity.Position = oldPosition;
		}
		public bool HasChanges() => oldPosition != newPosition;
	}
	
	public class SizeOperation : IFileEditOperation {
		public Entity Entity => entity;
		private Entity entity;
		private Vector2 oldSize;
		private Vector2 newSize;
		public SizeOperation(Entity entity, Vector2 newSize) {
			this.entity = entity;
			this.oldSize = entity.Size;
			this.newSize = newSize;
		}
		public void SetSize(Vector2 size) {
			newSize = size;
		}
		public void ApplyNextState(FileEditEntry entry) {
			var op = entry.GetData<SizeOperation>();
			op.entity.Size = newSize;
		}
		public void ApplyPrevState(FileEditEntry entry) {
			var op = entry.GetData<SizeOperation>();
			op.entity.Size = oldSize;
		}
		public bool HasChanges() => oldSize != newSize;
	}

}