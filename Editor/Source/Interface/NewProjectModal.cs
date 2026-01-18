using System.Numerics;
using ImGuiNET;
using Silk.NET.Maths;

namespace L2D; 

public class NewProjectModal {

	private bool open;
	private string path;
	private Vector2D<int> tileSize;

	public void Open() {
		open = true;
		path = "";
		tileSize = new(16, 16);
	}

	internal void Execute() {
		if(open) {
			ImGui.OpenPopup("New Project", ImGuiPopupFlags.AnyPopup);
			open = false;
		}
		ImGui.SetNextWindowPos(ImGui.GetIO().DisplaySize / 2.0F, ImGuiCond.Always, new Vector2(0.5F, 0.5F));
		if(ImGui.BeginPopupModal("New Project", ImGuiWindowFlags.AlwaysAutoResize)) {
			if(ImGui.Button("Choose")) {
				FileDialog.Save(path, Program.FILE_EXTENSION, result => {
					if(result != null) path = result;
				});
			}
			ImGui.SameLine();
			ImGui.InputText("Path", ref path, 512);
			
			ImGui.Spacing();
			ImGui.Spacing();
			// TODO: define tile size
			// if(ImGui.InputInt2("Tile Size", ref tileSize.X)) {
			// 	if(tileSize.X < 1) tileSize.X = 1;
			// 	if(tileSize.Y < 1) tileSize.Y = 1;
			// }

			bool valid = path != "";
			
			ImGui.BeginDisabled(!valid);
			if(ImGui.Button("Create")) {
				Program.NewFile(path);
				ImGui.CloseCurrentPopup();
			}
			ImGui.EndDisabled();
			ImGui.SameLine();
			if(ImGui.Button("Close")) {
				if(Program.File != null) {
					ImGui.CloseCurrentPopup();
				} else {
					Program.Close();
				}
			}
			ImGui.EndPopup();
		}
	}
	
}