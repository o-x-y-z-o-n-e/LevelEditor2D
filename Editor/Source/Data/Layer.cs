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
	
	public Tilemap Tilemap => tilemap;
	
	public List<EntityDefinition> Entities => entities;

	public PropertyCollection Properties => properties;
	
	public bool HasGroup => group != null;
	public bool HasTilemap => tilemap != null;
	public bool HasEntities => entities != null;

	private Scene scene;
	private string name;
	private bool visible;
	private LayerType type;
	private LayerGroup group;
	private Tilemap tilemap;
	private List<EntityDefinition> entities;
	private PropertyCollection properties;
	private bool disposed;

	internal Layer(Scene scene, LayerType type) {
		this.scene = scene;
		this.type = type;
		name = "new_layer";
		visible = true;
		group = null;
		properties = new();
		if(type == LayerType.Tiles) {
			tilemap = new Tilemap(this);
		} else {
			entities = new List<EntityDefinition>();
		}
	}
	
	internal Layer(Scene scene) {
		this.scene = scene;
		this.type = LayerType.Tiles;
		name = "new_layer";
		visible = true;
		group = null;
		properties = new();
	}

	internal void Parse(XElement layerElement) {
		name = layerElement.Attribute("name").Value;
		visible = layerElement.Attribute("visible").ParseAsBool(true);
		string type = layerElement.Attribute("type")?.Value ?? "tiles";
		if(type == "entities") {
			this.type = LayerType.Entities;
			entities = new();
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
			new XAttribute("type", type.ToString().ToLower())
		);
		
		properties.SerializeToElement(element);

		if(type == LayerType.Tiles && tilemap != null) {
			element.Add(tilemap.Serialize());
		}

		if(type == LayerType.Entities && entities != null) {
			var entitiesParent = new XElement("entities");
			// TODO
			element.Add(entitiesParent);
		}
		return element;
	}
	
	public void Dispose() {
		if(disposed) return;
		tilemap?.Dispose();
		disposed = true;
	}
	
}

public enum LayerType {
	Tiles,
	Entities,
}