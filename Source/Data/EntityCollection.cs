using System.Numerics;
using System.Xml.Linq;

namespace L2D;

public class EntityCollection {

	public int Count => entities.Count;
	
	public Layer Layer => layer;
	public World World => world;
	
	public IEnumerable<Entity> All => entities;

	private List<Entity> entities;
	private World world;
	private Layer layer;

	public EntityCollection(Layer layer) {
		this.layer = layer;
		this.world = layer.Scene.World;
		entities = new();
	}
	
	public EntityCollection(World world) {
		this.world = world;
		layer = null;
		entities = new();
	}
	
	public void SerializeToElement(XElement element) {
		foreach(var entity in entities) {
			var e = new XElement("entity");
			if(entity.Template != null) {
				e.Add(new XAttribute("template", entity.Template.Name));
			}
			if(entity.HasOwnName) {
				e.Add(new XAttribute("name", entity.Name));
			}
			if(entity.HasOwnType) {
				e.Add(new XAttribute("type", entity.Type));
			}
			if(entity.HasOwnPosition) {
				e.Add(new XAttribute("position.x", entity.Position.X));
				e.Add(new XAttribute("position.y", entity.Position.Y));
			}
			if(entity.HasOwnSize) {
				e.Add(new XAttribute("size.x", entity.Size.X));
				e.Add(new XAttribute("size.y", entity.Size.Y));
			}
			entity.Properties.SerializeToElement(e);
			element.Add(e);
		}
	}
	
	public void ParseFromElement(XElement element) {
		foreach(var e in element.Elements("entity")) {
			string templateName = e.Attribute("template").ParseAsString();
			string? name = e.Attribute("name")?.ParseAsString() ?? null;
			string? type = e.Attribute("type")?.ParseAsString() ?? null;
			float? px = e.Attribute("position.x")?.ParseAsFloat() ?? null;
			float? py = e.Attribute("position.y")?.ParseAsFloat() ?? null;
			float? sx = e.Attribute("size.x")?.ParseAsFloat() ?? null;
			float? sy = e.Attribute("size.y")?.ParseAsFloat() ?? null;
			Entity template = null;
			if(templateName != "") {
				foreach(var t in world.Templates.All) {
					if(t.Name == templateName) {
						template = t;
						break;
					}
				}
			}
			Entity entity = new Entity(this, templateName);
			entity.SetName(name);
			entity.SetType(type);
			if(px != null || py != null) {
				entity.SetPosition(new(px ?? 0.0F, py ?? 0.0F));
			}
			if(sx != null || sy != null) {
				entity.SetSize(new(sx ?? 0.0F, sy ?? 0.0F));
			}
			entity.Properties.ParseFromElement(e);
			entities.Add(entity);
		}
	}

	public int IndexOf(Entity entity) {
		return entities.IndexOf(entity);
	}

	public Entity Get(int index) {
		if(index < 0 || index >= entities.Count) return null;
		return entities[index];
	}
	
	public Entity Get(string name) {
		if(name == null) return null;
		foreach(var e in entities) {
			if(e.Name == name) return e;
		}
		return null;
	}

	public void Add(Entity entity) {
		if(entity.Collection != this) return;
		if(entities.Contains(entity)) return;
		entities.Add(entity);
	}
	
	public Entity Add() {
		Entity entity = new Entity(this);
		entities.Add(entity);
		return entity;
	}

	public Entity Copy(int index) {
		return Copy(entities[index]);
	}

	public Entity Copy(Entity srcEntity) {
		Entity dstEntity = new Entity(this, srcEntity.Template?.Name);
		dstEntity.SetName(srcEntity.OwnName);
		dstEntity.SetType(srcEntity.OwnType);
		dstEntity.SetPosition(srcEntity.OwnPosition);
		dstEntity.SetSize(srcEntity.OwnSize);
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
		if(entity == null || entity.Collection != this || entities.Contains(entity)) return;
		entities.Insert(index, entity);
	}

}

public class Entity {

	public const float POINT_HANDLE_SIZE = 14;

	public EntityCollection Collection => collection;

	public bool IsPoint => Size.X == 0.0F && Size.Y == 0.0F;
	public bool IsTemplate => collection.Layer == null;

	public Entity Template => collection.World.Templates.Get(template);

	public string Name {
		get {
			if(name != null) {
				return name;
			} else if(template != null) {
				return Template?.Name ?? "";
			} else {
				return "";
			}
		}
	}

	public string Type {
		get {
			if(type != null) {
				return type;
			} else if(template != null) {
				return Template?.Type ?? "";
			} else {
				return "";
			}
		}
	}
	
	public Vector2 Position {
		get {
			if(position != null) {
				return position.Value;
			} else if(template != null) {
				return Template?.Position ?? Vector2.Zero;
			} else {
				return Vector2.Zero;
			}
		}
	}

	public Vector2 Size {
		get {
			if(size != null) {
				return size.Value;
			} else if(template != null) {
				return Template?.Size ?? Vector2.Zero;
			} else {
				return Vector2.Zero;
			}
		}
	}

	internal bool HasOwnName => name != null;
	internal bool HasOwnType => type != null;
	internal bool HasOwnPosition => position != null;
	internal bool HasOwnSize => size != null;
	internal string? OwnName => name;
	internal string? OwnType => type;
	internal Vector2? OwnPosition => position;
	internal Vector2? OwnSize => size;

	private string? name;
	private string? type;
	private Vector2? position;
	private Vector2? size;

	private EntityCollection collection;
	private string template;

	public PropertyCollection Properties => properties;

	private PropertyCollection properties;

	public Entity(EntityCollection collection) : this(collection, null) {}
	
	public Entity(EntityCollection collection, string template) {
		this.collection = collection;
		this.template = template;
		properties = new();
		if(template != null) {
			name = null;
			type = null;
			position = Vector2.Zero;
			size = null;
		} else {
			name = "";
			type = "";
			position = Vector2.Zero;
			size = Vector2.Zero;
		}
	}

	public void SetName(string? name) {
		if(name == "") {
			this.name = null;
		} else {
			this.name = name;
		}
	}

	public void SetType(string? type) {
		if(type == "") {
			this.type = null;
		} else {
			this.type = type;
		}
	}
	
	public void SetPosition(Vector2? position) {
		this.position = position;
	}

	public void SetSize(Vector2? size) {
		this.size = size;
	}
	
	public class AddOperation : IFileEditOperation {
		public object? Context => collection;
		private EntityCollection collection;
		private Entity entity;
		public AddOperation(EntityCollection collection, Entity entity) {
			this.collection = collection;
			this.entity = entity;
		}
		public void ApplyNextState(FileEditEntry entry) {
			collection.Insert(entity, collection.Count);
		}
		public void ApplyPrevState(FileEditEntry entry) {
			collection.Remove(entity);
			if(Program.SelectedEntity == entity) {
				Program.SetSelectedEntity(null);
			}
		}
		public bool HasChanges() => true;
		public string GetNextStateMessage() => $"Add entity";
		public string GetPrevStateMessage() => $"Undo add entity";
	}

	public class MoveOperation : IFileEditOperation {
		public object? Context => collection;
		private EntityCollection collection;
		private int oldIndex;
		private int newIndex;
		public MoveOperation(EntityCollection collection, int oldIndex, int newIndex) {
			this.collection = collection;
			this.oldIndex = oldIndex;
			this.newIndex = newIndex;
		}
		public void ApplyNextState(FileEditEntry entry) {
			Entity entity = collection.Get(oldIndex);
			collection.Remove(oldIndex);
			collection.Insert(entity, newIndex);
		}
		public void ApplyPrevState(FileEditEntry entry) {
			Entity entity = collection.Get(newIndex);
			collection.Remove(newIndex);
			collection.Insert(entity, oldIndex);
		}
		public bool HasChanges() => oldIndex != newIndex;
		public string GetNextStateMessage() => $"Reorder entities";
		public string GetPrevStateMessage() => $"Undo reorder entities";
	}
	
	public class RemoveOperation : IFileEditOperation {
		public object? Context => collection;
		private EntityCollection collection;
		private Entity entity;
		private int index;
		public RemoveOperation(EntityCollection collection, Entity entity) {
			this.collection = collection;
			this.entity = entity;
			this.index = collection.IndexOf(entity);
		}
		public void ApplyNextState(FileEditEntry entry) {
			collection.Remove(entity);
			if(Program.SelectedEntity == entity) {
				Program.SetSelectedEntity(null);
			}
		}
		public void ApplyPrevState(FileEditEntry entry) {
			collection.Insert(entity, index);
		}
		public bool HasChanges() => true;
		public string GetNextStateMessage() => $"Remove entity";
		public string GetPrevStateMessage() => $"Undo remove entity";
	}
	
	public class NameOperation : IFileEditOperation {
		public object? Context => entity.collection;
		public Entity Entity => entity;
		private Entity entity;
		private string oldName;
		private string newName;
		public NameOperation(Entity entity, string newName) {
			this.entity = entity;
			this.oldName = entity.OwnName;
			SetName(newName);
		}
		public void SetName(string name) {
			newName = name;
			entity.SetName(name);
		}
		public void ApplyNextState(FileEditEntry entry) {
			entity.SetName(newName);
		}
		public void ApplyPrevState(FileEditEntry entry) {
			entity.SetName(oldName);
		}
		public bool HasChanges() => oldName != newName;
		public string GetNextStateMessage() => $"Rename entity";
		public string GetPrevStateMessage() => $"Undo rename entity";
	}
	
	public class TypeOperation : IFileEditOperation {
		public object? Context => entity.collection;
		public Entity Entity => entity;
		private Entity entity;
		private string? oldType;
		private string? newType;
		public TypeOperation(Entity entity, string newType) {
			this.entity = entity;
			this.oldType = entity.OwnType;
			SetType(newType);
		}
		public void SetType(string? type) {
			newType = type;
			entity.SetType(type);
		}
		public void ApplyNextState(FileEditEntry entry) {
			entity.SetType(newType);
		}
		public void ApplyPrevState(FileEditEntry entry) {
			entity.SetType(oldType);
		}
		public bool HasChanges() => oldType != newType;
		public string GetNextStateMessage() => $"Change entity type";
		public string GetPrevStateMessage() => $"Undo change entity type";
	}
	
	public class PositionOperation : IFileEditOperation {
		public object? Context => entity.collection;
		public Entity Entity => entity;
		private Entity entity;
		private Vector2? oldPosition;
		private Vector2? newPosition;
		public PositionOperation(Entity entity, Vector2? newPosition) {
			this.entity = entity;
			this.oldPosition = entity.OwnPosition;
			this.newPosition = newPosition;
		}
		public void SetPosition(Vector2? position) {
			newPosition = position;
			entity.SetPosition(position);
		}
		public void ApplyNextState(FileEditEntry entry) {
			entity.SetPosition(newPosition);
		}
		public void ApplyPrevState(FileEditEntry entry) {
			entity.SetPosition(oldPosition);
		}
		public bool HasChanges() => oldPosition != newPosition;
		public string GetNextStateMessage() => $"Position entity";
		public string GetPrevStateMessage() => $"Undo position entity";
	}
	
	public class SizeOperation : IFileEditOperation {
		public object? Context => entity.collection;
		public Entity Entity => entity;
		private Entity entity;
		private Vector2? oldSize;
		private Vector2? newSize;
		public SizeOperation(Entity entity, Vector2? newSize) {
			this.entity = entity;
			this.oldSize = entity.OwnSize;
			this.newSize = newSize;
		}
		public void SetSize(Vector2? size) {
			newSize = size;
			entity.SetSize(size);
		}
		public void ApplyNextState(FileEditEntry entry) {
			entity.SetSize(newSize);
		}
		public void ApplyPrevState(FileEditEntry entry) {
			entity.SetSize(oldSize);
		}
		public bool HasChanges() => oldSize != newSize;
		public string GetNextStateMessage() => $"Resize entity";
		public string GetPrevStateMessage() => $"Undo resize entity";
	}
	
	public class TransformOperation : IFileEditOperation {
		public object? Context => entity.collection?.Layer?.Scene?.World ?? (object?)entity.collection;
		public Entity Entity => entity;
		private Entity entity;
		private Vector2? oldPosition;
		private Vector2? oldSize;
		private Vector2? newPosition;
		private Vector2? newSize;
		public TransformOperation(Entity entity) {
			this.entity = entity;
			this.oldPosition = entity.OwnPosition;
			this.oldSize = entity.OwnSize;
			this.newPosition = this.oldPosition;
			this.newSize = this.oldSize;
		}
		public void SetPosition(Vector2 position) {
			newPosition = position;
		}
		public void SetSize(Vector2 size) {
			newSize = size;
		}
		public void ApplyNextState(FileEditEntry entry) {
			entity.SetPosition(newPosition);
			entity.SetSize(newSize);
		}
		public void ApplyPrevState(FileEditEntry entry) {
			entity.SetPosition(oldPosition);
			entity.SetSize(oldSize);
		}
		public bool HasChanges() => oldPosition != newPosition || oldSize != newSize;
		public string GetNextStateMessage() => $"Transform entity";
		public string GetPrevStateMessage() => $"Undo transform entity";
	}

}