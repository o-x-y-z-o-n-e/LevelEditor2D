using ImGuiNET;
using NativeFileDialogSharp;

namespace L2D; 

public class MenuBar {

	internal MenuBar() {
		
	}
	
	internal void Execute() {
		if(ImGui.BeginMainMenuBar()) {
			if(ImGui.BeginMenu("File")) {
				if(ImGui.MenuItem("New")) {
					if(Program.File != null && Program.File.UnsavedChanges) {
						Program.ConfirmModal.Open(
							"Confirm New File",
							"You have unsaved changes.\nAre you sure you want to create a new file?",
							Program.NewProjectModal.Open
						);
					} else {
						Program.NewProjectModal.Open();
					}
				}
				if(ImGui.MenuItem("Open", "Ctrl+O")) {
					if(Program.File != null && Program.File.UnsavedChanges) {
						Program.ConfirmModal.Open(
							"Confirm Open",
							"You have unsaved changes.\nAre you sure you want to open another file?",
							Program.OpenFileDialog
						);
					} else {
						Program.OpenFileDialog();
					}
				}

				bool disabled = Program.File == null;
				
				if(disabled) ImGui.BeginDisabled();
				if(ImGui.MenuItem("Save", "Ctrl+S")) {
					Program.SaveFile();
				}
				if(ImGui.MenuItem("Save as..", "Ctrl+Shift+S")) {
					Program.SaveFileDialog();
				}
				if(ImGui.MenuItem("Reload", "Ctrl+R")) {
					if(Program.File != null && Program.File.UnsavedChanges) {
						Program.ConfirmModal.Open(
							"Confirm Reload",
							"You have unsaved changes.\nAre you sure you want to reload file from disk?",
							Program.ReloadFile
						);
					} else {
						Program.ReloadFile();
					}
				}
				if(disabled) ImGui.EndDisabled();
				
				if(ImGui.MenuItem("Quit", "Ctrl+Q")) {
					if(Program.File != null && Program.File.UnsavedChanges) {
						Program.ConfirmModal.Open(
							"Confirm Quit",
							"You have unsaved changes.\nAre you sure you want to quit?",
							Program.Close
						);
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
		}
	}
	
}