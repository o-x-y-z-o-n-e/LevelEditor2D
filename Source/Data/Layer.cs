using System.Numerics;
using System.Xml.Linq;

namespace L2D;

public class Layer {

	public Scene Scene => scene;

	public LayerType Type => type;

	public string Name {
		get => name;
		set => name = value;
	}
	
	public bool Visible {
		get => visible;
		set => visible = value;
	}

	public bool IsGloballyVisible {
		get {
			if(group != null) {
				return group.IsGloballyVisible && visible;
			} else {
				return visible;
			}
		}
	}
	
	public Vector3 Color {
		get => color;
		set => color = value;
	}

	public bool Collapsed = false; // Only used for groups
	
	public Layer Group => group;
	
	public Tilemap Tilemap => tilemap;
	
	public EntityCollection Entities => entities;
	public PropertyCollection Properties => properties;
	public IEnumerable<Layer> Children => children;
	public int ChildrenCount => children?.Count ?? 0;

	private Scene scene;
	private string name;
	private bool visible;
	private Vector3 color;
	private Layer group;
	private LayerType type;
	private Tilemap tilemap;
	private EntityCollection entities;
	private PropertyCollection properties;
	private List<Layer> children;
	private bool disposed;

	internal Layer(Scene scene, LayerType type) {
		this.scene = scene;
		this.type = type;
		name = "new_layer";
		visible = true;
		color = Vector3.One;
		properties = new();
		if(type == LayerType.Tiles) {
			tilemap = new Tilemap(this);
		} else if(type == LayerType.Entities) {
			entities = new EntityCollection(this);
		} else if(type == LayerType.Group) {
			children = new();
		}
	}
	
	internal Layer(Scene scene) {
		this.scene = scene;
		this.type = LayerType.Tiles;
		name = "new_layer";
		visible = true;
		color = Vector3.One;
		properties = new();
	}

	internal void Parse(XElement layerElement) {
		name = layerElement.Attribute("name").Value;
		visible = layerElement.Attribute("visible").ParseAsBool(true);
		color = layerElement.Attribute("color").ParseAsColor(Vector3.One);
		properties.ParseFromElement(layerElement);
		string type = layerElement.Attribute("type")?.Value ?? "tiles";
		if(type == "entities") {
			this.type = LayerType.Entities;
			entities = new EntityCollection(this);
			var entitiesElement = layerElement.Element("entities");
			if(entitiesElement != null) {
				entities.ParseFromElement(entitiesElement);
			}
		} else if(type == "group") {
			this.type = LayerType.Group;
			children = new();
			foreach(var childElement in layerElement.Elements("layer")) {
				Layer layer = new Layer(scene);
				layer.Parse(childElement);
				AddChild(layer);
			}
		} else {
			this.type = LayerType.Tiles;
			tilemap = new Tilemap(this);
			var tilemapElement = layerElement.Element("tilemap");
			if(tilemapElement != null) {
				tilemap.Parse(tilemapElement);
			}
		}
	}

	internal XElement Serialize() {
		var element = new XElement("layer");
		element.Add(
			new XAttribute("name", name),
			new XAttribute("type", type.ToString().ToLower()),
			new XAttribute("visible", visible),
			new XAttribute("color", Utilities.SerializeColor(color))
		);
		
		properties.SerializeToElement(element);

		if(type == LayerType.Group && children != null) {
			foreach(var child in children) {
				element.Add(child.Serialize());
			}
		}

		if(type == LayerType.Tiles && tilemap != null) {
			element.Add(tilemap.Serialize());
		}

		if(type == LayerType.Entities && entities != null) {
			var entitiesParent = new XElement("entities");
			entities.SerializeToElement(entitiesParent);
			element.Add(entitiesParent);
		}
		return element;
	}

	public static void Copy(Layer src, Layer dst) {
		if(src.Type != dst.Type) return;
		
		dst.Visible = src.Visible;
		dst.Color = src.Color;
		src.Properties.CopyTo(dst.Properties);
		if(src.Type == LayerType.Tiles) {
			for(int y = 0; y < src.Scene.TileCountY && y < dst.Scene.TileCountY; y++) {
				for(int x = 0; x < src.Scene.TileCountX && x < dst.Scene.TileCountX; x++) {
					dst.Tilemap.Set(x, y, src.Tilemap.Get(x, y));
				}
			}
		} else if(src.Type == LayerType.Entities) {
			foreach(var srcEntity in src.Entities.All) {
				var newEntity = new Entity(dst.Entities, srcEntity.Template?.Name);
				newEntity.SetName(srcEntity.OwnName);
				newEntity.SetType(srcEntity.OwnType);
				newEntity.SetPosition(srcEntity.OwnPosition);
				newEntity.SetSize(srcEntity.OwnSize);
				foreach(var p in srcEntity.Properties.All) {
					var newP = newEntity.Properties.Add(p.Name, p.Type);
					newP.String = p.String;
					newP.Integer = p.Integer;
					newP.Float = p.Float;
					newP.Boolean = p.Boolean;
				}
				dst.Entities.Add(newEntity);
			}
		} else if(src.Type == LayerType.Group) {
			foreach(var srcChild in src.Children) {
				Layer dstChild = new Layer(dst.Scene, srcChild.Type);
				dstChild.Name = srcChild.Name;
				dst.AddChild(dstChild);
				Layer.Copy(srcChild, dstChild);
			}
		}
	}

	public bool AddChild(Layer layer) {
		if(type != LayerType.Group) return false;
		return AddChild(layer, children.Count);
	}

	public bool AddChild(Layer layer, int i) {
		if(type != LayerType.Group) return false;
		if(children.Contains(layer)) return false;
		children.Insert(i, layer);
		layer.group = this;
		return true;
	}

	public bool RemoveChild(Layer layer) {
		if(type != LayerType.Group) return false;
		return RemoveChild(children.IndexOf(layer));
	}
	
	public bool RemoveChild(int i) {
		if(type != LayerType.Group) return false;
		if(i < 0 || i >= children.Count) return false;
		children[i].group = null;
		if(scene.LastActiveLayer == children[i]) {
			scene.LastActiveLayer = null;
		}
		children.RemoveAt(i);
		return true;
	}

	public Layer GetChild(int i) {
		if(type != LayerType.Group) return null;
		if(i < 0 || i >= children.Count) return null;
		return children[i];
	}

	public int GetChildIndex(Layer layer) {
		if(type != LayerType.Group) return -1;
		return children.IndexOf(layer);
	}
	
	public void SwapChildren(int index1, int index2) {
		if(type != LayerType.Group) return;
		if(index1 < 0 || index1 >= children.Count || index2 < 0 || index2 >= children.Count) return;
		var t = children[index1];
		children[index1] = children[index2];
		children[index2] = t;
	}
	
	public void SwapChildren(Layer layer1, Layer layer2) {
		if(type != LayerType.Group) return;
		SwapChildren(children.IndexOf(layer1), children.IndexOf(layer2));
	}

	public bool IsChildOf(Layer group) {
		if(this.group == null) return false;
		if(this.group == group) return true;
		return this.group.IsChildOf(group);
	}
	
	public class AddOperation : IFileEditOperation {
		private Layer group;
		private Layer layer;
		private int index;
		public AddOperation(Layer group, Layer layer, int index) {
			this.group = group;
			this.layer = layer;
			this.index = index;
		}
		public void ApplyNextState(FileEditEntry entry) {
			group.AddChild(layer, index);
		}
		public void ApplyPrevState(FileEditEntry entry) {
			group.RemoveChild(layer);
			if(Program.SelectedLayer == layer) {
				Program.SetSelectedLayer(null);
			}
		}
		public bool HasChanges() => true;
		public string GetNextStateMessage() => $"Add layer [{layer.name}] to scene [{group.scene.ID}]";
		public string GetPrevStateMessage() => $"Undo add layer [{layer.name}] to scene [{group.scene.ID}]";
	}
	
	public class MoveOperation : IFileEditOperation {
		private Layer oldGroup;
		private int oldIndex;
		private Layer newGroup;
		private int newIndex;
		public MoveOperation(Layer oldGroup, int oldIndex, Layer newGroup, int newIndex) {
			this.oldGroup = oldGroup;
			this.oldIndex = oldIndex;
			this.newGroup = newGroup;
			this.newIndex = newIndex;
		}
		public void ApplyNextState(FileEditEntry entry) {
			Layer layer = oldGroup.GetChild(oldIndex);
			oldGroup.RemoveChild(oldIndex);
			newGroup.AddChild(layer, newIndex);
		}
		public void ApplyPrevState(FileEditEntry entry) {
			Layer layer = newGroup.GetChild(newIndex);
			newGroup.RemoveChild(newIndex);
			oldGroup.AddChild(layer, oldIndex);
		}
		public bool HasChanges() => oldGroup != newGroup || oldIndex != newIndex;
		public string GetNextStateMessage() => $"Reorder layers in scene [{oldGroup.scene.ID}]";
		public string GetPrevStateMessage() => $"Undo reorder layers in scene [{oldGroup.scene.ID}]";
	}
	
	public class VisiblityOperation : IFileEditOperation {
		private Layer layer;
		private bool oldValue;
		private bool newValue;
		public VisiblityOperation(Layer layer, bool newValue) {
			this.layer = layer;
			this.oldValue = layer.Visible;
			this.newValue = newValue;
		}
		public void ApplyNextState(FileEditEntry entry) {
			layer.Visible = newValue;
		}
		public void ApplyPrevState(FileEditEntry entry) {
			layer.Visible = oldValue;
		}
		public bool HasChanges() => oldValue != newValue;
		public string GetNextStateMessage() => $"Change visiblity for layer [{layer.Name}]";
		public string GetPrevStateMessage() => $"Undo change visiblity for layer [{layer.Name}]";
	}
	
	public class RenameOperation : IFileEditOperation {
		private Layer layer;
		private string oldName;
		private string newName;
		public RenameOperation(Layer layer, string newName) {
			this.layer = layer;
			this.oldName = layer.Name;
			this.newName = newName;
		}
		public void ApplyNextState(FileEditEntry entry) {
			layer.Name = newName;
		}
		public void ApplyPrevState(FileEditEntry entry) {
			layer.Name = oldName;
		}
		public bool HasChanges() => oldName != newName;
		public string GetNextStateMessage() => $"Rename layer from [{oldName}] to [{newName}]";
		public string GetPrevStateMessage() => $"Undo rename layer from [{oldName}] to [{newName}]";
	}
	
	public class RemoveOperation : IFileEditOperation {
		private Layer group;
		private Layer layer;
		private int index;
		public RemoveOperation(Layer group, Layer layer) {
			this.group = group;
			this.layer = layer;
			this.index = group.GetChildIndex(layer);
		}
		public void ApplyNextState(FileEditEntry entry) {
			group.RemoveChild(layer);
			if(layer == Program.SelectedLayer) {
				Program.SetSelectedLayer(null);
			}
		}
		public void ApplyPrevState(FileEditEntry entry) {
			group.AddChild(layer, index);
		}
		public bool HasChanges() => true;
		public string GetNextStateMessage() => $"Remove layer [{layer.name}] from scene [{group.scene.ID}]";
		public string GetPrevStateMessage() => $"Undo remove layer [{layer.name}] from scene [{group.scene.ID}]";
	}

	public class ColorOperation : IFileEditOperation {
		public ref Vector3 NewColor => ref newColor;
		private Layer layer;
		private Vector3 oldColor;
		private Vector3 newColor;
		public ColorOperation(Layer layer) {
			this.layer = layer;
			this.oldColor = layer.Color;
			this.newColor = layer.Color;
		}
		public void ApplyNextState(FileEditEntry entry) {
			layer.Color = newColor;
		}
		public void ApplyPrevState(FileEditEntry entry) {
			layer.Color = oldColor;
		}
		public bool HasChanges() => oldColor != newColor;
		public string GetNextStateMessage() => $"Color layer [{layer.name}]";
		public string GetPrevStateMessage() => $"Undo color layer [{layer.name}]";
	}
	
}

public enum LayerType {
	Tiles,
	Entities,
	Group
}