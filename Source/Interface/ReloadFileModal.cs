using System.Numerics;
using ImGuiNET;

namespace L2D;

public class ReloadFileModal {
	
	private bool open;

	public ReloadFileModal() {
		open = false;
	}

	public void Open() {
		open = true;
	}

	internal void Body() {
		if(open) {
			ImGui.OpenPopup("File Changed");
			open = false;
		}
		ImGui.SetNextWindowPos(ImGui.GetIO().DisplaySize / 2.0F, ImGuiCond.Always, new Vector2(0.5F, 0.5F));
		bool o = true;
		if(ImGui.BeginPopupModal("File Changed", ref o, ImGuiWindowFlags.AlwaysAutoResize)) {
			ImGui.Text("Detected changes to project file from outside this editor.\nDo you want to reload the file from disk?");
			ImGui.Spacing();
			if(ImGui.Button("Yes")) {
				ImGui.CloseCurrentPopup();
				Program.ReloadFile();
			}
			ImGui.SameLine();
			if(ImGui.Button("No")) {
				ImGui.CloseCurrentPopup();
			}
			ImGui.EndPopup();
		}
	}
	
}