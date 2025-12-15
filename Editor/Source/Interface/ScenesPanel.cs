using ImGuiNET;

namespace L2D; 

public class ScenesPanel : Panel {

	public ScenesPanel() {
		Title = "Scenes";
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
		
		World world = Program.File.World;
		
		for(int i = 0; i < world.SceneCount; i++) {
			Scene scene = world.GetScene(i);
			bool active = Program.ActiveScene == scene;
			if(ImGui.Selectable(scene.ID, active, ImGuiSelectableFlags.SpanAllColumns)) {
				if(active) {
					Program.ActiveScene = null;
				} else {
					Program.ActiveScene = scene;
				}
			}
		}
		
		// TODO: local tool bar for actions; new, up, down, copy, delete
		// TODO: drag selectables
	}
}