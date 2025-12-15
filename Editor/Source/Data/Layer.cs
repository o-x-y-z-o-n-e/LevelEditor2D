using System.Xml.Linq;

namespace L2D;

public class Layer {

	public string Name {
		get => name;
		set => name = value;
	}
	
	public bool HasGroup => group != null;
	public bool HasTilemap => tilemap != null;

	private string name;
	private LayerGroup group;
	private Tilemap tilemap;

	internal Layer() {
		name = "new_layer";
		group = null;
		tilemap = null;
	}

	internal void Parse(XElement layerElement, string dir) {
		name = layerElement.Attribute("name").Value;
	}
	
}