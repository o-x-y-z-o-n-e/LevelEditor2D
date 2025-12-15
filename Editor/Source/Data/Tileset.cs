using System.Xml.Linq;

namespace L2D; 

public class Tileset {

	private string id;
	private string textureFilePath;
	private int offsetX;
	private int offsetY;
	private int spacingX;
	private int spacingY;

	internal Tileset() {
		id = "new_tileset";
		textureFilePath = "";
		offsetX = 0;
		offsetY = 0;
		spacingX = 0;
		spacingY = 0;
	}

	internal void Parse(XElement tilesetElement, string dir) {
		
	}
}