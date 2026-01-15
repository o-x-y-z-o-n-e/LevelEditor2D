using System.Numerics;
using IconFonts;
using ImGuiNET;
using Silk.NET.Maths;
using Rectangle = System.Drawing.Rectangle;

namespace L2D; 

public class ScenesPanel : Panel {

	private Vector2D<int> sceneResizeVar;
	private Vector2D<int> sceneReposVar;
	private string sceneRenameBuffer;
	
	public ScenesPanel() {
		Title = "Scenes";
		sceneResizeVar = new(0);
		sceneReposVar = new(0);
		sceneRenameBuffer = "";
	}

	protected override void Update() {
		if(Program.File == null) {
			return;
		}
		
		World world = Program.File.World;

		Vector2 listSize = ImGui.GetContentRegionAvail();
		listSize.Y -= 200;
		ImGui.BeginChild("scene_list", listSize, ImGuiChildFlags.Borders);
		
		ImGui.PushItemFlag(ImGuiItemFlags.AllowDuplicateId, true);
		
		int count = world.SceneCount;
		for(int i = 0; i < count; i++) {
			ImGui.PushID(i);
			
			Scene scene = world.GetScene(i);
			bool active = Program.SelectedScene == scene;
			if(active) ImGui.PushStyleColor(ImGuiCol.Text, Utilities.GetPackedColor(30, 255, 30, 255));
			if(ImGui.Selectable(scene.ID, active, ImGuiSelectableFlags.SpanAllColumns)) {
				if(active) {
					Program.SelectedScene = null;
				} else {
					Program.SelectedScene = scene;
				}
			}
			if(active) ImGui.PopStyleColor();
			
			if(ImGui.IsItemActive() && !ImGui.IsItemHovered()) {
				int n_next = i + (ImGui.GetMouseDragDelta(0).Y < 0.0F ? -1 : 1);
				if(n_next >= 0 && n_next < count) {
					world.SwapScenes(i, n_next);
					ImGui.ResetMouseDragDelta();
				}
			}
			
			// ImGui.OpenPopupOnItemClick("context", ImGuiPopupFlags.MouseButtonRight);
			// if(ImGui.BeginPopup("context")) {
			// 	ImGui.EndPopup();
			// }
			
			ImGui.PopID();
		}
		ImGui.PopItemFlag();
		
		ImGui.EndChild();
		
		if(ImGui.Button(Codicons.DiffAdded)) {
			sceneResizeVar = new(16);
			sceneReposVar = new(0);
			sceneRenameBuffer = "";
			ImGui.OpenPopup("add-scene");
		}
		
		if(ImGui.BeginPopup("add-scene")) {
			ImGui.Text("New scene");

			ImGui.InputText("ID", ref sceneRenameBuffer, Program.IMGUI_STRING_MAX);
			ImGui.DragInt2("Position", ref sceneReposVar.X, 1);
			ImGui.DragInt2("Size", ref sceneResizeVar.X, 1);

			bool valid = sceneRenameBuffer != "" && sceneResizeVar.X > 0 && sceneResizeVar.Y > 0;
			foreach(var s in world.Scenes) {
				if(s.ID == sceneRenameBuffer) {
					valid = false;
					break;
				}
				Rectangle r = new(s.WorldX, s.WorldY, s.TileCountX, s.TileCountY);
				if(r.IntersectsWith(new(sceneReposVar.X, sceneReposVar.Y, sceneResizeVar.X, sceneResizeVar.Y))) {
					valid = false;
					break;
				}
			}
			
			Program.CanvasPanel.EnableScenePreview(new(sceneReposVar.X, sceneReposVar.Y, sceneResizeVar.X, sceneResizeVar.Y));
			
			ImGui.BeginDisabled(!valid);
			if(ImGui.Button("Create")) {
				world.CreateScene(sceneRenameBuffer, sceneResizeVar.X, sceneResizeVar.Y, sceneReposVar.X, sceneReposVar.Y);
				ImGui.CloseCurrentPopup();
			}
			ImGui.EndDisabled();
			
			ImGui.SameLine();
			if(ImGui.Button("Cancel")) {
				ImGui.CloseCurrentPopup();
			}
			
			ImGui.EndPopup();
		} else {
			Program.CanvasPanel.DisableScenePreview();
		}
		
		ImGui.SameLine();
		ImGui.BeginDisabled(Program.SelectedScene == null);
		
		if(ImGui.Button(Codicons.Copy)) {
			ImGui.OpenPopup("copy-scene");
		}
		
		if(ImGui.BeginPopup("copy-scene")) {
			ImGui.Text("Copy scene");
			// TODO
			ImGui.EndPopup();
		}
		
		ImGui.SameLine();
		
		if(ImGui.Button(Codicons.Trash)) {
			ImGui.OpenPopup("delete-scene");
		}
		
		if(ImGui.BeginPopup("delete-scene")) {
			ImGui.Text("Delete scene?");
			if(ImGui.Button("Confirm")) {
				world.DeleteScene(Program.SelectedScene);
				ImGui.CloseCurrentPopup();
			}
			ImGui.SameLine();
			ImGui.Dummy(new Vector2(80, 0));
			ImGui.SameLine();
			ImGui.SetCursorPosX(ImGui.GetCursorPosX() + ImGui.GetContentRegionAvail().X - ImGui.CalcTextSize("Cancel").X - ImGui.GetStyle().FramePadding.X * 2);
			if(ImGui.Button("Cancel")) {
				ImGui.CloseCurrentPopup();
			}
			ImGui.EndPopup();
		}
		
		ImGui.SameLine();
		
		if(ImGui.Button(Codicons.ChevronUp)) {
			int i = world.GetSceneIndex(Program.SelectedScene);
			world.SwapScenes(i, i-1);
		}
		ImGui.SameLine();
		if(ImGui.Button(Codicons.ChevronDown)) { 
			int i = world.GetSceneIndex(Program.SelectedScene);
			world.SwapScenes(i, i+1);
		}
		
		ImGui.SameLine();
		ImGui.SetCursorPosX(ImGui.GetCursorPosX() + ImGui.GetContentRegionAvail().X - ImGui.CalcTextSize(Codicons.OpenInProduct).X - 12);
		if(ImGui.Button(Codicons.OpenInProduct)) {
			Program.CanvasPanel.LocateScene(Program.SelectedScene);
		}
		
		ImGui.EndDisabled();
		
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
			
			if(ImGui.Button($"{scene.TileCountX}, {scene.TileCountY}", new Vector2(ImGui.CalcItemWidth(), 0))) {
				sceneResizeVar = new(scene.TileCountX, scene.TileCountY);
				ImGui.OpenPopup("resize");
			}
			ImGui.SameLine();
			ImGui.SetCursorPosX(ImGui.GetCursorPosX() - ImGui.GetStyle().ItemInnerSpacing.X);
			ImGui.Text("Size");
			
			if(ImGui.BeginPopup("resize")) {
				ImGui.Text("Resize scene?");
				if(ImGui.InputInt2("##New Size", ref sceneResizeVar.X)) {
					if(sceneResizeVar.X < 1) sceneResizeVar.X = 1;
					if(sceneResizeVar.Y < 1) sceneResizeVar.Y = 1;
				}
				if(ImGui.Button("Confirm")) {
					// TODO
					ImGui.CloseCurrentPopup();
				}
				ImGui.SameLine();
				ImGui.SetCursorPosX(ImGui.GetCursorPosX() + ImGui.GetContentRegionAvail().X - ImGui.CalcTextSize("Cancel").X - ImGui.GetStyle().FramePadding.X * 2);
				if(ImGui.Button("Cancel")) {
					ImGui.CloseCurrentPopup();
				}
				ImGui.EndPopup();
			}
		} else {
			ImGui.Text("No scene selected...");
		}
		
		// TODO: local tool bar for actions; new, up, down, copy, delete
		// TODO: drag selectables
	}
}