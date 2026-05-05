using System.Numerics;
using IconFonts;
using ImGuiNET;

namespace E2D;

public class MenuBar {

	internal MenuBar() {
		
	}
	
	internal void Execute() {
		ImGui.PushStyleVar(ImGuiStyleVar.WindowBorderSize, 0.0F);
		if(ImGui.BeginMainMenuBar()) {
			FileMenu();
			WorldMenu();
			SceneMenu();
			LayerMenu();
			EditMenu();
			HelpMenu();
			PerformanceCounter();
			ImGui.EndMainMenuBar();
		}
		ImGui.PopStyleVar();
	}

	private void FileMenu() {
		if(ImGui.BeginMenu("File")) {
			if(ImGui.MenuItem("New")) {
				if(Program.Project != null && Program.Project.UnsavedChanges) {
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
				if(Program.Project != null && Program.Project.UnsavedChanges) {
					Program.ConfirmModal.Open(
						"Confirm Open",
						"You have unsaved changes.\nAre you sure you want to open another file?",
						Program.OpenFileDialog
					);
				} else {
					Program.OpenFileDialog();
				}
			}

			bool disabled = Program.Project == null;
				
			if(disabled) ImGui.BeginDisabled();
			if(ImGui.MenuItem("Save", "Ctrl+S")) {
				Program.SaveFile();
			}
			if(ImGui.MenuItem("Save as..", "Ctrl+Shift+S")) {
				Program.SaveFileDialog();
			}
			if(ImGui.MenuItem("Reload", "Ctrl+R")) {
				if(Program.Project != null && Program.Project.UnsavedChanges) {
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
				if(Program.Project != null && Program.Project.UnsavedChanges) {
					Program.ConfirmModal.Open(
						"Confirm Quit",
						"You have unsaved changes.\nAre you sure you want to quit?",
						Program.Close
					);
				} else {
					Program.Close();
				}
			}
			ImGui.EndMenu(); // File
		}
	}

	private void WorldMenu() {
		if(ImGui.BeginMenu("World")) {
			if(ImGui.MenuItem("Settings")) {
				Program.WorldSettingsModal.Open();
			}
			ImGui.EndMenu();
		}
	}

	private void SceneMenu() {
		if(ImGui.BeginMenu("Scene")) {
			ImGui.BeginDisabled(Program.Project == null);
			if(ImGui.MenuItem("Create")) {
				Program.ScenesPanel.OpenAddPopup();
			}
			ImGui.EndDisabled(); // Program.File == null
			ImGui.BeginDisabled(Program.SelectedScene == null);
			if(ImGui.MenuItem("Rename")) {
				Program.ScenesPanel.OpenRenamePopup(Program.SelectedScene);
			}
			if(ImGui.MenuItem("Copy")) {
				Program.ScenesPanel.OpenCopyPopup(Program.SelectedScene);
			}
			if(ImGui.MenuItem("Delete")) {
				Program.ScenesPanel.OpenDeletePopup(Program.SelectedScene);
			}
			ImGui.EndDisabled(); // Program.SelectedScene == null
			ImGui.EndMenu(); // Scene
		}
	}

	private void LayerMenu() {
		if(ImGui.BeginMenu("Layer")) {
			ImGui.BeginDisabled(Program.SelectedScene == null);
			if(ImGui.MenuItem("Create")) {
				Program.LayersPanel.OpenAddPopup();
			}
			ImGui.EndDisabled(); // Program.SelectedScene == null
			ImGui.BeginDisabled(Program.SelectedLayer == null);
			if(ImGui.MenuItem("Rename")) {
				Program.LayersPanel.OpenRenamePopup(Program.SelectedLayer);
			}
			if(ImGui.MenuItem("Copy")) {
				Program.LayersPanel.OpenCopyPopup(Program.SelectedLayer);
			}
			if(ImGui.MenuItem("Delete")) {
				Program.LayersPanel.OpenDeletePopup(Program.SelectedLayer);
			}
			ImGui.EndDisabled(); // Program.SelectedLayer == null
			ImGui.EndMenu(); // Layer
		}
	}

	private void EditMenu() {
		if(ImGui.BeginMenu("Edit")) {
			ImGui.BeginDisabled(!Program.Project.CanUndo());
			if(ImGui.MenuItem("Undo", "Ctrl+Z")) {
				Program.Project.Undo();
			}
			if(Program.Project.CanUndo()) {
				ImGui.SetItemTooltip(Program.Project.GetUndoMessage());
			}
			ImGui.EndDisabled(); // !Program.File.CanUndo()
			ImGui.BeginDisabled(!Program.Project.CanRedo());
			if(ImGui.MenuItem("Redo", "Ctrl+Y")) {
				Program.Project.Redo();
			}
			if(Program.Project.CanRedo()) {
				ImGui.SetItemTooltip(Program.Project.GetRedoMessage());
			}
			ImGui.EndDisabled(); // !Program.File.CanRedo()
			ImGui.EndMenu(); // Edit
		}
	}

	private void HelpMenu() {
		if(ImGui.BeginMenu("Help")) {
			if(ImGui.MenuItem("Manual")) {
				Utilities.OpenWebLink("https://www.youtube.com/watch?v=dQw4w9WgXcQ");
			}
			ImGui.BeginDisabled();
			if(ImGui.MenuItem("About")) {
				// TODO
			}
			ImGui.SetItemTooltip("TODO");
			if(ImGui.MenuItem("Check for Updates")) {
				// TODO
			}
			ImGui.SetItemTooltip("TODO");
			ImGui.EndDisabled();
			ImGui.EndMenu(); // Help
		}
	}

	private void PerformanceCounter() {
		string str = $"{Program.FrameRate:F2} fps";
		ImGui.SetCursorPosX(ImGui.GetCursorPosX() + ImGui.GetContentRegionAvail().X - ImGui.CalcTextSize(str).X);
		ImGui.Text(str);
	}

	private void ExperimentalButtons() {
		/* custom title bar experiment
		ImGui.SetCursorPosX(
			ImGui.GetCursorPosX() +
			ImGui.GetContentRegionAvail().X -
			ImGui.CalcTextSize(Codicons.ChromeMinimize).X -
			ImGui.CalcTextSize(Codicons.ChromeRestore).X -
			ImGui.CalcTextSize(Codicons.ChromeClose).X -
			ImGui.GetStyle().FramePadding.X * 5
			+ 4
		);
		ImGui.PushStyleColor(ImGuiCol.Button, 0);
		ImGui.PushStyleVar(ImGuiStyleVar.FrameRounding, 0);
		ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, Vector2.Zero);
		ImGui.Button(Codicons.ChromeMinimize);
		ImGui.Button(Codicons.ChromeRestore);
		ImGui.Button(Codicons.ChromeClose);
		ImGui.PopStyleVar();
		ImGui.PopStyleVar();
		ImGui.PopStyleColor();
		*/
	}
	
}