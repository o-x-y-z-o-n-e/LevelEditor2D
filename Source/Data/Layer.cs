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
	
	public Vector3 Color {
		get => color;
		set => color = value;
	}
	
	public Tilemap Tilemap => tilemap;
	
	public EntityCollection Entities => entities;

	public PropertyCollection Properties => properties;
	
	public bool HasGroup => group != null;
	public bool HasTilemap => tilemap != null;
	public bool HasEntities => entities != null;

	private Scene scene;
	private string name;
	private bool visible;
	private Vector3 color;
	private LayerType type;
	private LayerGroup group;
	private Tilemap tilemap;
	private EntityCollection entities;
	private PropertyCollection properties;
	private bool disposed;

	internal Layer(Scene scene, LayerType type) {
		this.scene = scene;
		this.type = type;
		name = "new_layer";
		visible = true;
		color = Vector3.One;
		group = null;
		properties = new();
		if(type == LayerType.Tiles) {
			tilemap = new Tilemap(this);
		} else {
			entities = new EntityCollection(this);
		}
	}
	
	internal Layer(Scene scene) {
		this.scene = scene;
		this.type = LayerType.Tiles;
		name = "new_layer";
		visible = true;
		color = Vector3.One;
		group = null;
		properties = new();
	}

	internal void Parse(XElement layerElement) {
		name = layerElement.Attribute("name").Value;
		visible = layerElement.Attribute("visible").ParseAsBool(true);
		color = layerElement.Attribute("color").ParseAsColor(Vector3.One);
		string type = layerElement.Attribute("type")?.Value ?? "tiles";
		if(type == "entities") {
			this.type = LayerType.Entities;
			entities = new EntityCollection(this);
			var entitiesElement = layerElement.Element("entities");
			if(entitiesElement != null) {
				entities.ParseFromElement(entitiesElement);
			}
		} else {
			this.type = LayerType.Tiles;
			tilemap = new Tilemap(this);
			var tilemapElement = layerElement.Element("tilemap");
			if(tilemapElement != null) {
				tilemap.Parse(tilemapElement);
			}
		}
		properties.ParseFromElement(layerElement);
	}

	internal XElement Serialize() {
		var element = new XElement("layer");
		element.Add(
			new XAttribute("name", name),
			new XAttribute("group", ""), // TODO
			new XAttribute("visible", visible),
			new XAttribute("color", Utilities.SerializeColor(color)),
			new XAttribute("type", type.ToString().ToLower())
		);
		
		properties.SerializeToElement(element);

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
	
	public class AddOperation : IFileEditOperation {
		private Scene scene;
		private Layer layer;
		public AddOperation(Scene scene, Layer layer) {
			this.scene = scene;
			this.layer = layer;
		}
		public void ApplyNextState(FileEditEntry entry) {
			var op = entry.GetData<AddOperation>();
			op.scene.InsertLayer(op.layer, op.scene.LayerCount);
		}
		public void ApplyPrevState(FileEditEntry entry) {
			var op = entry.GetData<AddOperation>();
			op.scene.RemoveLayer(op.layer);
			if(Program.SelectedLayer == op.layer) {
				Program.SetSelectedLayer(null);
			}
		}
		public bool HasChanges() => true;
	}

	public class MoveOperation : IFileEditOperation {
		private Scene scene;
		private int index1;
		private int index2;
		public MoveOperation(Scene scene, int index1, int index2) {
			this.scene = scene;
			this.index1 = index1;
			this.index2 = index2;
		}
		public void ApplyNextState(FileEditEntry entry) {
			var op = entry.GetData<MoveOperation>();
			op.scene.SwapLayers(op.index1, op.index2);
		}
		public void ApplyPrevState(FileEditEntry entry) {
			var op = entry.GetData<MoveOperation>();
			op.scene.SwapLayers(op.index2, op.index1);
		}
		public bool HasChanges() => index1 != index2;
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
			var op = entry.GetData<VisiblityOperation>();
			op.layer.Visible = op.newValue;
		}
		public void ApplyPrevState(FileEditEntry entry) {
			var op = entry.GetData<VisiblityOperation>();
			op.layer.Visible = op.oldValue;
		}
		public bool HasChanges() => oldValue != newValue;
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
			var op = entry.GetData<RenameOperation>();
			op.layer.Name = op.newName;
		}
		public void ApplyPrevState(FileEditEntry entry) {
			var op = entry.GetData<RenameOperation>();
			op.layer.Name = op.oldName;
		}
		public bool HasChanges() => oldName != newName;
	}
	
	public class RemoveOperation : IFileEditOperation {
		private Scene scene;
		private Layer layer;
		private int index;
		public RemoveOperation(Scene scene, Layer layer) {
			this.scene = scene;
			this.layer = layer;
			this.index = scene.GetLayerIndex(layer);
		}
		public void ApplyNextState(FileEditEntry entry) {
			var op = entry.GetData<RemoveOperation>();
			op.scene.RemoveLayer(op.layer);
			if(op.layer == Program.SelectedLayer) {
				Program.SetSelectedLayer(null);
			}
		}
		public void ApplyPrevState(FileEditEntry entry) {
			var op = entry.GetData<RemoveOperation>();
			op.scene.InsertLayer(op.layer, op.index);
		}
		public bool HasChanges() => true;
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
			var op = entry.GetData<ColorOperation>();
			op.layer.Color = op.newColor;
		}
		
		public void ApplyPrevState(FileEditEntry entry) {
			var op = entry.GetData<ColorOperation>();
			op.layer.Color = op.oldColor;
		}
		
		public bool HasChanges() => oldColor != newColor;
	}
	
}

public enum LayerType {
	Tiles,
	Entities,
}