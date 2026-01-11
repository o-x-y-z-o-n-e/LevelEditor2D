using ImGuiNET;

namespace L2D; 

public class MenuBar {

	internal MenuBar() {
		
	}
	
	internal void Execute() {
		if(ImGui.BeginMainMenuBar()) {

			if(ImGui.BeginMenu("File")) {
				if(ImGui.MenuItem("New")) {
					// TODO
				}
				if(ImGui.MenuItem("Open", "Ctrl+O")) {
					// TODO
				}
				if(Program.File == null) ImGui.BeginDisabled();
				if(ImGui.MenuItem("Save", "Ctrl+S")) {
					Program.File.Write();
				}
				if(ImGui.MenuItem("Save as..", "Ctrl+Shift+S")) {
					// TODO
				}
				if(ImGui.MenuItem("Reload", "Ctrl+R")) {
					Program.File.Read();
				}
				if(Program.File == null) ImGui.EndDisabled();
				if(ImGui.MenuItem("Quit", "Ctrl+Q")) {
					Program.Close();
				}
				ImGui.EndMenu();
			}

			if(ImGui.BeginMenu("Edit")) {
				ImGui.BeginDisabled();
				if(ImGui.MenuItem("Undo", "Ctrl+Z")) {
					// TODO
				}
				if(ImGui.MenuItem("Redo", "Ctrl+Y")) {
					// TODO
				}
				if(ImGui.MenuItem("Copy", "Ctrl+C")) {
					// TODO
				}
				if(ImGui.MenuItem("Cut", "Ctrl+X")) {
					// TODO
				}
				if(ImGui.MenuItem("Paste", "Ctrl+V")) {
					// TODO
				}
				if(ImGui.MenuItem("Delete", "Del")) {
					// TODO
				}
				ImGui.EndDisabled();
				ImGui.EndMenu();
			}
			
			if(ImGui.BeginMenu("Help")) {
				ImGui.BeginDisabled();
				if(ImGui.MenuItem("Manual")) {
					// TODO
				}
				if(ImGui.MenuItem("About")) {
					// TODO
				}
				ImGui.EndDisabled();
				ImGui.EndMenu();
			}

			ImGui.EndMainMenuBar();
		}
	}
	
}