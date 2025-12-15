using ImGuiNET;

namespace L2D; 

public class LayersPanel : Panel {

	public LayersPanel() {
		Title = "Layers";
	}

	protected override void Update() {
		if(Program.File == null) {
			ImGui.Text("No file loaded...");
			return;
		}
		if(Program.File.World == null) {
			ImGui.Text("No world active...");
			return;
		}
		if(Program.ActiveScene == null) {
			ImGui.Text("No scene active...");
			return;
		}

		World world = Program.File.World;
		Scene scene = Program.ActiveScene;
		
		for(int i = 0; i < scene.Layers.Count; i++) {
			// TODO: visibility toggle
			Layer layer = scene.Layers[i];
			if(ImGui.Selectable(layer.Name, Program.ActiveLayer == layer, ImGuiSelectableFlags.SpanAllColumns)) {
				Program.ActiveLayer = layer;
			}
		}
	
		// TODO: local tool bar for actions; new, up, down, copy, delete
		// TODO: drag selectables
	}
}