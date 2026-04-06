using System.Drawing;
using System.Numerics;
using IconFonts;
using ImGuiNET;

namespace E2D;

public class ScenesPanel : Panel {

	private bool sceneAddEmbedded;
	private string sceneNameEdit;
	private Vector2Int sceneSizeEdit;
	private Vector2Int scenePosEdit;
	private bool addPopup;
	private bool copyPopup;
	private Scene copyTarget;
	private bool deletePopup;
	private Scene deleteTarget;
	private bool renamePopup;
	private Scene renameTarget;
	private bool positionPopup;
	private Scene positionTarget;
	private bool resizePopup;
	private Scene resizeTarget;
	private bool scenePreviewShown;
	
	public ScenesPanel() {
		Title = $"{Codicons.EditorLayout} Scenes";
		sceneAddEmbedded = false;
		sceneNameEdit = "";
		sceneSizeEdit = new(0, 0);
		scenePosEdit = new(0, 0);
		addPopup = false;
		copyPopup = false;
		copyTarget = null;
		deletePopup = false;
		deleteTarget = null;
		positionPopup = false;
		positionTarget = null;
		resizePopup = false;
		resizeTarget = null;
		scenePreviewShown = false;
	}

	protected override void Update() {
		if(Program.Project == null) {
			return;
		}
		
		World world = Program.Project.World;
		
		Scene.MoveOperation moveOperation = null;

		Vector2 listSize = ImGui.GetContentRegionAvail();
		listSize.Y -= 200;
		ImGui.BeginChild("scene-list", listSize, ImGuiChildFlags.Borders | ImGuiChildFlags.ResizeY);
		Scenes(world, ref moveOperation, scene => {
			ImGui.OpenPopupOnItemClick("context", ImGuiPopupFlags.MouseButtonRight);
			if(ImGui.BeginPopup("context")) {
				if(ImGui.MenuItem("Locate")) {
					Program.CanvasPanel.LocateScene(scene);
					Program.Focus(Program.CanvasPanel);
				}
				if(ImGui.MenuItem("Export as Image")) {
					FileDialog.Save("", "png", path => {
						if(path != null) {
							scene.ExportToFile(path);
						}
					});
				}
				if(ImGui.MenuItem("Copy")) {
					copyPopup = true;
					copyTarget = scene;
				}
				if(ImGui.MenuItem("Delete")) {
					deletePopup = true;
					deleteTarget = scene;
				}
				ImGui.EndPopup();
			}
		});
		ImGui.EndChild(); // scene-list

		scenePreviewShown = false;

		int selectedSceneIndex = -1;
		if(Program.SelectedScene != null) {
			selectedSceneIndex = world.GetSceneIndex(Program.SelectedScene);
		}
		
		if(ImGui.Button(Codicons.DiffAdded)) {
			addPopup = true;
		}
		ImGui.SetItemTooltip("Create");
		
		ImGui.BeginDisabled(Program.SelectedScene == null);
		
		ImGui.SameLine();
		if(ImGui.Button(Codicons.Copy)) {
			copyPopup = true;
			copyTarget = Program.SelectedScene;
		}
		ImGui.SetItemTooltip("Copy");
		
		ImGui.SameLine();
		if(ImGui.Button(Codicons.Trash)) {
			deletePopup = true;
			deleteTarget = Program.SelectedScene;
		}
		ImGui.SetItemTooltip("Delete");
		
		ImGui.BeginDisabled(selectedSceneIndex <= 0);
		ImGui.SameLine();
		if(ImGui.Button(Codicons.ChevronUp)) {
			moveOperation = new Scene.MoveOperation(world, selectedSceneIndex, selectedSceneIndex - 1);
		}
		ImGui.SetItemTooltip("Move Up");
		ImGui.EndDisabled();
		
		ImGui.BeginDisabled(selectedSceneIndex >= world.SceneCount - 1);
		ImGui.SameLine();
		if(ImGui.Button(Codicons.ChevronDown)) {
			moveOperation = new Scene.MoveOperation(world, selectedSceneIndex, selectedSceneIndex + 1);
		}
		ImGui.SetItemTooltip("Move Down");
		ImGui.EndDisabled();
		
		ImGui.SameLine();
		ImGui.SetCursorPosX(ImGui.GetCursorPosX() + ImGui.GetContentRegionAvail().X - ImGui.CalcTextSize(Codicons.OpenInProduct).X - 12);
		if(ImGui.Button(Codicons.OpenInProduct)) {
			Program.CanvasPanel.LocateScene(Program.SelectedScene);
			Program.Focus(Program.CanvasPanel);
		}
		ImGui.SetItemTooltip("Locate");
		
		ImGui.EndDisabled(); // Program.SelectedScene == null
		
		if(Program.SelectedScene != null) {
			Inspect(Program.SelectedScene);
		} else {
			ImGui.Text("No scene selected...");
		}
		
		AddPopup();
		CopyPopup();
		DeletePopup();
		RenamePopup();
		RepositionPopup();
		ResizePopup();

		if(moveOperation != null) {
			Program.Project.ApplyEdit(this, moveOperation);
		}

		if(!scenePreviewShown) {
			Program.CanvasPanel.DisableScenePreview();
		}
	}

	private unsafe void Scenes(World world, ref Scene.MoveOperation moveOperation, Action<Scene> contextMenu) {
		int count = world.SceneCount;
		Vector2 cur = ImGui.GetCursorPos();
		for(int i = 0; i < count; i++) {
			ImGui.PushID(i);
			
			cur = ImGui.GetCursorPos();
			Scene scene = world.GetScene(i);
			bool active = Program.SelectedScene == scene;
			if(active) ImGui.PushStyleColor(ImGuiCol.Text, Utilities.GetPackedColor(30, 255, 30, 255));
			if(ImGui.Selectable(scene.ID, active, ImGuiSelectableFlags.SpanAllColumns)) {
				if(active) {
					Program.SelectedScene = null;
				} else {
					Program.SelectedScene = scene;
				}
				Program.Focus(this);
			}
			if(active) ImGui.PopStyleColor();
			
			contextMenu?.Invoke(scene);
			
			if(ImGui.BeginDragDropSource()) {
				ImGui.Text(scene.ID);
				ImGui.SetDragDropPayload("MOVE_SCENE_DATA", (IntPtr)(&i), sizeof(int));
				ImGui.EndDragDropSource();
			}
			Vector2 nextCur = ImGui.GetCursorPos();
			ImGui.SetCursorPos(cur - new Vector2(0, 4));
			Vector2 scur = ImGui.GetCursorScreenPos();
			ImGui.Dummy(new Vector2(ImGui.GetContentRegionAvail().X, 6));
			if(moveOperation == null) {
				if(ImGui.BeginDragDropTarget()) {
					ImGuiPayloadPtr payloadPtr = ImGui.AcceptDragDropPayload("MOVE_SCENE_DATA", ImGuiDragDropFlags.AcceptNoDrawDefaultRect | ImGuiDragDropFlags.AcceptBeforeDelivery);
					if(payloadPtr.NativePtr != null) {
						if(payloadPtr.IsPreview()) {
							ImGui.GetWindowDrawList().AddRectFilled(
								scur,
								scur + new Vector2(ImGui.GetContentRegionAvail().X, 3),
								Utilities.GetPackedColor(50, 80, 220, 255)
							);
						}
						if(payloadPtr.IsDelivery()) {
							int srcIndex = ((int*)payloadPtr.Data)[0];
							int insertIndex = i;
							if(srcIndex < i) insertIndex--;
							if(srcIndex != insertIndex) {
								moveOperation = new Scene.MoveOperation(world, srcIndex, insertIndex);
							}
						}
					}
					ImGui.EndDragDropTarget();
				}
			}
			ImGui.SetCursorPos(nextCur);

			if(scene.IsEmbedded) {
				ImGui.PushStyleColor(ImGuiCol.Text, Utilities.GetPackedColor(255, 255, 255, 80));
				ImGui.SetCursorPos(new Vector2(
					ImGui.GetCursorPosX() + ImGui.GetContentRegionAvail().X - ImGui.GetStyle().FramePadding.X - ImGui.CalcTextSize("(Embedded)").X,
					cur.Y
				));
				ImGui.Text("(Embedded)");
				ImGui.PopStyleColor(); // ImGuiCol.Text
			}

			ImGui.PopID(); // i
		}
		if(count > 0) {
			float height = ImGui.GetCursorPosY() - cur.Y;
			ImGui.SetCursorPos(cur + new Vector2(0, height - 4));
			Vector2 scur = ImGui.GetCursorScreenPos();
			ImGui.Dummy(new Vector2(ImGui.GetContentRegionAvail().X, 6));
			if(moveOperation == null) {
				if(ImGui.BeginDragDropTarget()) {
					ImGuiPayloadPtr payloadPtr = ImGui.AcceptDragDropPayload("MOVE_SCENE_DATA", ImGuiDragDropFlags.AcceptNoDrawDefaultRect | ImGuiDragDropFlags.AcceptBeforeDelivery);
					if(payloadPtr.NativePtr != null) {
						if(payloadPtr.IsPreview()) {
							ImGui.GetWindowDrawList().AddRectFilled(
								scur,
								scur + new Vector2(ImGui.GetContentRegionAvail().X, 3),
								Utilities.GetPackedColor(50, 80, 220, 255)
							);
						}
						if(payloadPtr.IsDelivery()) {
							int srcIndex = ((int*)payloadPtr.Data)[0];
							if(srcIndex < count - 1) {
								moveOperation = new Scene.MoveOperation(world, srcIndex, count - 1);
							}
						}
					}
					ImGui.EndDragDropTarget();
				}
			}
		}
	}

	private void Inspect(Scene scene) {
		World world = scene.World;
		
		ImGui.SeparatorText("Scene Options");
		
		string id = scene.ID;
		ImGui.SetNextItemShortcut(ImGuiKey.P, ImGuiInputFlags.RouteGlobal);
		if(ImGui.InputText("ID", ref id, Program.IMGUI_STRING_MAX)) { }
		if(ImGui.IsItemDeactivatedAfterEdit()) {
			bool valid = true;
			foreach(var s in world.Scenes) {
				if(s.ID == id) {
					valid = false;
					break;
				}
			}
			if(valid) {
				Program.Project.ApplyEdit(this, new Scene.RenameOperation(scene, id));
			}
		}
			
		if(ImGui.Button($"{scene.WorldX}, {scene.WorldY}", new Vector2(ImGui.CalcItemWidth(), 0))) {
			positionPopup = true;
			positionTarget = scene;
		}
		ImGui.SameLine();
		ImGui.SetCursorPosX(ImGui.GetCursorPosX() - ImGui.GetStyle().ItemInnerSpacing.X);
		ImGui.Text("Position");
			
		if(ImGui.Button($"{scene.TileCountX}, {scene.TileCountY}", new Vector2(ImGui.CalcItemWidth(), 0))) {
			resizePopup = true;
			resizeTarget = scene;
		}
		ImGui.SameLine();
		ImGui.SetCursorPosX(ImGui.GetCursorPosX() - ImGui.GetStyle().ItemInnerSpacing.X);
		ImGui.Text("Size");
		
		PropertyView.Run(scene.Properties);
	}

	public void OpenAddPopup() {
		addPopup = true;
	}

	private void AddPopup() {
		World world = Program.Project.World;
		if(addPopup) {
			addPopup = false;
			sceneNameEdit = "";
			scenePosEdit = new(0, 0);
			sceneSizeEdit = new(16, 16);
			ImGui.OpenPopup("add-scene");
		}
		if(ImGui.BeginPopup("add-scene")) {
			ImGui.Text("New scene");

			ImGui.InputText("ID", ref sceneNameEdit, Program.IMGUI_STRING_MAX);
			ImGui.DragInt2("Position", ref scenePosEdit.X, 1);
			if(ImGui.DragInt2("Size", ref sceneSizeEdit.X, 1)) {
				if(sceneSizeEdit.X < 1) sceneSizeEdit.X = 1;
				if(sceneSizeEdit.Y < 1) sceneSizeEdit.Y = 1;
			}

			bool valid = sceneNameEdit != "" && sceneSizeEdit.X > 0 && sceneSizeEdit.Y > 0;
			foreach(var s in world.Scenes) {
				if(s.ID == sceneNameEdit) {
					valid = false;
					break;
				}
				Rectangle r = new(s.WorldX, s.WorldY, s.TileCountX, s.TileCountY);
				if(r.IntersectsWith(new(scenePosEdit.X, scenePosEdit.Y, sceneSizeEdit.X, sceneSizeEdit.Y))) {
					valid = false;
					break;
				}
			}

			if(ImGui.BeginCombo("Location", sceneAddEmbedded ? "Embedded" : "External")) {
				if(ImGui.Selectable("Embedded", sceneAddEmbedded)) {
					sceneAddEmbedded = true;
				}
				ImGui.SetItemTooltip($"Scene will be embedded into:\n{world.Project.GetAbsolutePath()}");
				if(ImGui.Selectable("External", !sceneAddEmbedded)) {
					sceneAddEmbedded = false;
				}
				ImGui.SetItemTooltip($"Scene will be saved to:\n{world.Project.GetScenePath(sceneNameEdit)}");
				ImGui.EndCombo();
			}
			if(sceneAddEmbedded) {
				ImGui.SetItemTooltip($"Scene will be embedded into:\n{world.Project.GetAbsolutePath()}");
			} else {
				ImGui.SetItemTooltip($"Scene will be saved to:\n{world.Project.GetScenePath(sceneNameEdit)}");
			}
			
			Program.CanvasPanel.EnableScenePreview(new(scenePosEdit.X, scenePosEdit.Y, sceneSizeEdit.X, sceneSizeEdit.Y));
			scenePreviewShown = true;
			
			ImGui.BeginDisabled(!valid);
			if(ImGui.Button("Confirm")) {
				var newScene = world.CreateScene(
					sceneNameEdit,
					sceneSizeEdit.X,
					sceneSizeEdit.Y,
					scenePosEdit.X,
					scenePosEdit.Y,
					sceneAddEmbedded,
					true
				);
				Program.Project.ApplyEdit(this, new Scene.AddOperation(world, newScene));
				Program.SetSelectedScene(newScene);
				Program.Focus(this);
				ImGui.CloseCurrentPopup();
			}
			ImGui.EndDisabled();
			
			ImGui.SameLine();
			if(ImGui.Button("Cancel")) {
				ImGui.CloseCurrentPopup();
			}
			
			ImGui.EndPopup();
		}
	}

	public void OpenCopyPopup(Scene scene) {
		copyPopup = true;
		copyTarget = scene;
	}

	private void CopyPopup() {
		World world = Program.Project.World;
		if(copyPopup) {
			copyPopup = false;
			sceneNameEdit = "";
			scenePosEdit = new(0, 0);
			if(copyTarget != null) {
				ImGui.OpenPopup("copy-scene");
			}
		}
		if(ImGui.BeginPopup("copy-scene")) {
			ImGui.Text("Copy scene");
			
			ImGui.InputText("New ID", ref sceneNameEdit, Program.IMGUI_STRING_MAX);
			ImGui.DragInt2("Position", ref scenePosEdit.X, 1);
			
			bool valid = sceneNameEdit != "";
			foreach(var s in world.Scenes) {
				if(s.ID == sceneNameEdit) {
					valid = false;
					break;
				}
				Rectangle r = new(s.WorldX, s.WorldY, s.TileCountX, s.TileCountY);
				if(r.IntersectsWith(new(scenePosEdit.X, scenePosEdit.Y, copyTarget.TileCountX, copyTarget.TileCountY))) {
					valid = false;
					break;
				}
			}
			
			if(ImGui.BeginCombo("Location", sceneAddEmbedded ? "Embedded" : "External")) {
				if(ImGui.Selectable("Embedded", sceneAddEmbedded)) {
					sceneAddEmbedded = true;
				}
				ImGui.SetItemTooltip($"Scene will be embedded into:\n{world.Project.GetAbsolutePath()}");
				if(ImGui.Selectable("External", !sceneAddEmbedded)) {
					sceneAddEmbedded = false;
				}
				ImGui.SetItemTooltip($"Scene will be saved to:\n{world.Project.GetScenePath(sceneNameEdit)}");
				ImGui.EndCombo();
			}
			if(sceneAddEmbedded) {
				ImGui.SetItemTooltip($"Scene will be embedded into:\n{world.Project.GetAbsolutePath()}");
			} else {
				ImGui.SetItemTooltip($"Scene will be saved to:\n{world.Project.GetScenePath(sceneNameEdit)}");
			}
			
			Program.CanvasPanel.EnableScenePreview(new(scenePosEdit.X, scenePosEdit.Y, copyTarget.TileCountX, copyTarget.TileCountY));
			scenePreviewShown = true;
			
			ImGui.BeginDisabled(!valid);
			if(ImGui.Button("Confirm")) {
				Scene newScene = world.CopyScene(
					copyTarget,
					sceneNameEdit,
					scenePosEdit.X,
					scenePosEdit.Y,
					sceneAddEmbedded
				);
				Program.Project.ApplyEdit(this, new Scene.AddOperation(world, newScene));
				Program.SetSelectedScene(newScene);
				Program.Focus(this);
				ImGui.CloseCurrentPopup();
			}
			ImGui.EndDisabled();
			
			ImGui.SameLine();
			if(ImGui.Button("Cancel")) {
				ImGui.CloseCurrentPopup();
			}
			
			ImGui.EndPopup();
		} else {
			copyTarget = null;
		}
	}

	public void OpenDeletePopup(Scene scene) {
		deletePopup = true;
		deleteTarget = scene;
	}

	private void DeletePopup() {
		World world = Program.Project.World;
		if(deletePopup) {
			deletePopup = false;
			if(deleteTarget != null) {
				ImGui.OpenPopup("delete-scene");
			}
		}
		if(ImGui.BeginPopup("delete-scene")) {
			ImGui.Text("Delete scene?");
			if(ImGui.Button("Confirm")) {
				Program.Project.ApplyEdit(this, new Scene.RemoveOperation(world, deleteTarget));
				if(deleteTarget == Program.SelectedScene) {
					Program.SetSelectedScene(null);
					Program.Focus(this);
				}
				ImGui.CloseCurrentPopup();
			}
			ImGui.SameLine();
			if(ImGui.Button("Cancel")) {
				ImGui.CloseCurrentPopup();
			}
			ImGui.EndPopup();
		} else {
			deleteTarget = null;
		}
	}
	
	public void OpenRenamePopup(Scene scene) {
		renamePopup = true;
		renameTarget = scene;
	}

	private void RenamePopup() {
		if(renamePopup) {
			renamePopup = false;
			if(renameTarget != null) {
				sceneNameEdit = renameTarget.ID;
				ImGui.OpenPopup("rename-scene");
			}
		}
		if(ImGui.BeginPopup("rename-scene")) {
			ImGui.Text("Rename selected scene");
			if(ImGui.InputText("Name", ref sceneNameEdit, Program.IMGUI_STRING_MAX)) { }
			bool invalidName = sceneNameEdit == "";
			foreach(var l in renameTarget.World.Scenes) {
				if(l.ID == sceneNameEdit) {
					invalidName = true;
					break;
				}
			}
			ImGui.BeginDisabled(invalidName);
			if(ImGui.Button("Confirm")) {
				Program.Project.ApplyEdit(this, new Scene.RenameOperation(renameTarget, sceneNameEdit));
				ImGui.CloseCurrentPopup();
			}
			ImGui.EndDisabled();
			ImGui.SameLine();
			if(ImGui.Button("Cancel")) {
				ImGui.CloseCurrentPopup();
			}
			ImGui.EndPopup();
		} else {
			renameTarget = null;
		}
	}

	private void RepositionPopup() {
		World world = Program.Project.World;
		if(positionPopup) {
			positionPopup = false;
			if(positionTarget != null) {
				scenePosEdit = new(positionTarget.WorldX, positionTarget.WorldY);
				ImGui.OpenPopup("position-scene");
			}
		}
		if(ImGui.BeginPopup("position-scene")) {
			ImGui.Text("Position scene");
			ImGui.DragInt2("##New Position", ref scenePosEdit.X, 1);
				
			Program.CanvasPanel.EnableScenePreview(new(scenePosEdit.X, scenePosEdit.Y, positionTarget.TileCountX, positionTarget.TileCountY), positionTarget);
			scenePreviewShown = true;

			bool valid = true;
			foreach(var s in world.Scenes) {
				if(s == positionTarget) continue;
				Rectangle r = new(s.WorldX, s.WorldY, s.TileCountX, s.TileCountY);
				if(r.IntersectsWith(new(scenePosEdit.X, scenePosEdit.Y, positionTarget.TileCountX, positionTarget.TileCountY))) {
					valid = false;
					break;
				}
			}
				
			ImGui.BeginDisabled(!valid);
			if(ImGui.Button("Confirm")) {
				Program.Project.ApplyEdit(this, new Scene.RepositionOperation(positionTarget, new(scenePosEdit.X, scenePosEdit.Y)));
				ImGui.CloseCurrentPopup();
			}
			ImGui.EndDisabled();
				
			ImGui.SameLine();
			ImGui.SetCursorPosX(ImGui.GetCursorPosX() + ImGui.GetContentRegionAvail().X - ImGui.CalcTextSize("Cancel").X - ImGui.GetStyle().FramePadding.X * 2);
			if(ImGui.Button("Cancel")) {
				ImGui.CloseCurrentPopup();
			}
			ImGui.EndPopup();
		} else {
			positionTarget = null;
		}
	}

	private void ResizePopup() {
		World world = Program.Project.World;
		if(resizePopup) {
			resizePopup = false;
			if(resizeTarget != null) {
				sceneSizeEdit = new(resizeTarget.TileCountX, resizeTarget.TileCountY);
				ImGui.OpenPopup("resize-scene");
			}
		}
		if(ImGui.BeginPopup("resize-scene")) {
			ImGui.Text("Resize scene");
			if(ImGui.DragInt2("##New Size", ref sceneSizeEdit.X, 1)) {
				if(sceneSizeEdit.X < 1) sceneSizeEdit.X = 1;
				if(sceneSizeEdit.Y < 1) sceneSizeEdit.Y = 1;
			}
				
			Program.CanvasPanel.EnableScenePreview(new(resizeTarget.WorldX, resizeTarget.WorldY, sceneSizeEdit.X, sceneSizeEdit.Y), resizeTarget);
			scenePreviewShown = true;
				
			bool valid = true;
			foreach(var s in world.Scenes) {
				if(s == resizeTarget) continue;
				Rectangle r = new(s.WorldX, s.WorldY, s.TileCountX, s.TileCountY);
				if(r.IntersectsWith(new(resizeTarget.WorldX, resizeTarget.WorldY, sceneSizeEdit.X, sceneSizeEdit.Y))) {
					valid = false;
					break;
				}
			}
				
			ImGui.BeginDisabled(!valid);
			if(ImGui.Button("Confirm")) {
				Program.Project.ApplyEdit(this, new Scene.ResizeOperation(resizeTarget, new(sceneSizeEdit.X, sceneSizeEdit.Y)));
				ImGui.CloseCurrentPopup();
			}
			ImGui.EndDisabled();
				
			ImGui.SameLine();
			ImGui.SetCursorPosX(ImGui.GetCursorPosX() + ImGui.GetContentRegionAvail().X - ImGui.CalcTextSize("Cancel").X - ImGui.GetStyle().FramePadding.X * 2);
			if(ImGui.Button("Cancel")) {
				ImGui.CloseCurrentPopup();
			}
			ImGui.EndPopup();
		} else {
			resizeTarget = null;
		}
	}
	
}