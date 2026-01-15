using System.Numerics;
using ImGuiNET;

namespace L2D; 

public class NewProjectModal {

	private bool open;
	private string path;

	public void Open() {
		open = true;
		path = "";
	}

	internal void Execute() {
		if(open) {
			ImGui.OpenPopup("New Project");
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
			
			if(ImGui.Button("Create")) {
				if(Program.File != null) {
					// TODO: confirm popup
				} else {
					// TODO
				}
			}
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