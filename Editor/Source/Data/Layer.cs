using System.Xml.Linq;

namespace L2D;

public class Layer {

	public Scene Scene => scene;

	public string Name {
		get => name;
		set => name = value;
	}
	
	public bool Visible {
		get => visible;
		set => visible = value;
	}
	
	public Tilemap Tilemap => tilemap;
	
	public bool HasGroup => group != null;
	public bool HasTilemap => tilemap != null;

	private Scene scene;
	private string name;
	private bool visible;
	private LayerGroup group;
	private Tilemap tilemap;
	private bool disposed;

	internal Layer(Scene scene) {
		this.scene = scene;
		name = "new_layer";
		visible = true;
		group = null;
		tilemap = null;
	}

	internal void Parse(XElement layerElement) {
		name = layerElement.Attribute("name").Value;
		visible = layerElement.Attribute("visible").ParseAsBool(true);
		var tilemapElement = layerElement.Element("tilemap");
		if(tilemapElement != null) {
			tilemap = new Tilemap(this);
			tilemap.Parse(tilemapElement);
		}
	}

	public void Dispose() {
		if(disposed) return;
		tilemap?.Dispose();
		disposed = true;
	}
	
}