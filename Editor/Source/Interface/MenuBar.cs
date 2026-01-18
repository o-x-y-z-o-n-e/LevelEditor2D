using ImGuiNET;
using NativeFileDialogSharp;

namespace L2D; 

public class MenuBar {

	internal MenuBar() {
		
	}
	
	internal void Execute() {
		bool confirmNew = false;
		bool confirmQuit = false;
		bool confirmOpen = false;
		bool confirmReload = false;
		
		if(ImGui.BeginMainMenuBar()) {
			if(ImGui.BeginMenu("File")) {
				if(ImGui.MenuItem("New")) {
					if(Program.File != null && Program.File.UnsavedChanges) {
						confirmNew = true;
					} else {
						Program.NewProjectModal.Open();
					}
				}
				if(ImGui.MenuItem("Open", "Ctrl+O")) {
					if(Program.File != null && Program.File.UnsavedChanges) {
						confirmOpen = true;
					} else {
						FileDialog.Open(Program.File.GetPath(), Program.FILE_EXTENSION, result => {
							if(result != null) Program.OpenFile(result);
						});
					}
				}

				bool disabled = Program.File == null;
				
				if(disabled) ImGui.BeginDisabled();
				if(ImGui.MenuItem("Save", "Ctrl+S")) {
					Program.SaveFile();
				}
				if(ImGui.MenuItem("Save as..", "Ctrl+Shift+S")) {
					FileDialog.Save(Program.File.GetPath(), Program.FILE_EXTENSION, result => {
						if(result != null) {
							Program.SaveFile(result);
						}
					});
				}
				if(ImGui.MenuItem("Reload", "Ctrl+R")) {
					if(Program.File.UnsavedChanges) {
						confirmReload = true;
					} else {
						Program.ReloadFile();
					}
				}
				if(disabled) ImGui.EndDisabled();
				
				if(ImGui.MenuItem("Quit", "Ctrl+Q")) {
					if(Program.File != null && Program.File.UnsavedChanges) {
						confirmQuit = true;
					} else {
						Program.Close();
					}
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
			
			if(confirmNew) ImGui.OpenPopup("Confirm New File");
			if(ImGui.BeginPopupModal("Confirm New File", ImGuiWindowFlags.AlwaysAutoResize)) {
				ImGui.Text("You have unsaved changes. Are you sure you want to create a new file?");
				if(ImGui.Button("Yes")) {
					Program.NewProjectModal.Open();
				}
				ImGui.SameLine();
				if(ImGui.Button("No")) {
					ImGui.CloseCurrentPopup();
				}
				ImGui.EndPopup();
			}
			
			if(confirmQuit) ImGui.OpenPopup("Confirm Quit");
			if(ImGui.BeginPopupModal("Confirm Quit", ImGuiWindowFlags.AlwaysAutoResize)) {
				ImGui.Text("You have unsaved changes. Are you sure you want to quit?");
				if(ImGui.Button("Yes")) {
					Program.Close();
				}
				ImGui.SameLine();
				if(ImGui.Button("No")) {
					ImGui.CloseCurrentPopup();
				}
				ImGui.EndPopup();
			}
			
			if(confirmOpen) ImGui.OpenPopup("Confirm Open");
			if(ImGui.BeginPopupModal("Confirm Open", ImGuiWindowFlags.AlwaysAutoResize)) {
				ImGui.Text("You have unsaved changes. Are you sure you want to open another file?");
				if(ImGui.Button("Yes")) {
					FileDialog.Open(Program.File.GetPath(), Program.FILE_EXTENSION, result => {
						if(result != null) Program.OpenFile(result);
					});
				}
				ImGui.SameLine();
				if(ImGui.Button("No")) {
					ImGui.CloseCurrentPopup();
				}
				ImGui.EndPopup();
			}
			
			if(confirmReload) ImGui.OpenPopup("Confirm Reload");
			if(ImGui.BeginPopupModal("Confirm Reload", ImGuiWindowFlags.AlwaysAutoResize)) {
				ImGui.Text("You have unsaved changes. Are you sure you want to reload file from disk?");
				if(ImGui.Button("Yes")) {
					Program.ReloadFile();
				}
				ImGui.SameLine();
				if(ImGui.Button("No")) {
					ImGui.CloseCurrentPopup();
				}
				ImGui.EndPopup();
			}
		}

		Program.NewProjectModal.Body();
	}
	
}