using System.Numerics;
using ImGuiNET;

namespace L2D; 

public class ScenesPanel : Panel {

	public ScenesPanel() {
		Title = "Scenes";
	}

	protected override void Update() {
		if(Program.File == null) {
			return;
		}
		
		World world = Program.File.World;


		Vector2 listSize = ImGui.GetContentRegionAvail();
		listSize.Y -= 200;
		ImGui.BeginChild("scene_list", listSize, ImGuiChildFlags.Borders);
		
		for(int i = 0; i < world.SceneCount; i++) {
			Scene scene = world.GetScene(i);
			bool active = Program.SelectedScene == scene;
			if(ImGui.Selectable(scene.ID, active, ImGuiSelectableFlags.SpanAllColumns)) {
				if(active) {
					Program.SelectedScene = null;
				} else {
					Program.SelectedScene = scene;
				}
			}
		}
		
		ImGui.EndChild();
		
		ImGui.SeparatorText("Scene Settings");

		if(Program.SelectedScene != null) {
			Scene scene = Program.SelectedScene;
			string id = scene.ID;
			if(ImGui.InputText("ID", ref id, 256, ImGuiInputTextFlags.EnterReturnsTrue)) {
				// TODO: update scene id
			}
			
			int wx = scene.WorldX;
			if(ImGui.InputInt("World X", ref wx)) {
				scene.WorldX = wx;
			}
			
			int wy = scene.WorldY;
			if(ImGui.InputInt("World Y", ref wy)) {
				scene.WorldY = wy;
			}

			int tcx = scene.TileCountX;
			if(ImGui.InputInt("Tiles Width", ref tcx)) {
				// TODO: update tile count
			}
			
			int tcy = scene.TileCountY;
			if(ImGui.InputInt("Tiles Height", ref tcy)) {
				// TODO: update tile count
			}
		} else {
			ImGui.Text("No scene selected...");
		}
		
		// TODO: local tool bar for actions; new, up, down, copy, delete
		// TODO: drag selectables
	}
}