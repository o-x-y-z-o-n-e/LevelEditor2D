using System.Drawing;
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

		bool scenePreviewShown = false;

		Vector2 listSize = ImGui.GetContentRegionAvail();
		listSize.Y -= 200;
		ImGui.BeginChild("scene-list", listSize, ImGuiChildFlags.Borders | ImGuiChildFlags.ResizeY);
		
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
					ImGui.ResetMouseDragDelta();
					Program.File.ApplyEdit(this, new MoveOperation(world, i, n_next),
						redo: MoveOperation.ApplyNextState,
						undo: MoveOperation.ApplyPrevState
					);
				}
			}
			
			ImGui.OpenPopupOnItemClick("context", ImGuiPopupFlags.MouseButtonRight);
			if(ImGui.BeginPopup("context")) {
				if(ImGui.MenuItem("Locate")) {
					Program.CanvasPanel.LocateScene(scene);
				}
				ImGui.EndPopup();
			}
			
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
			scenePreviewShown = true;
			
			ImGui.BeginDisabled(!valid);
			if(ImGui.Button("Create")) {
				var newScene = world.CreateScene(sceneRenameBuffer, sceneResizeVar.X, sceneResizeVar.Y, sceneReposVar.X, sceneReposVar.Y);
				Program.SetSelectedScene(newScene);
				ImGui.CloseCurrentPopup();
				Program.File.ApplyEdit(this, new AddOperation(world, newScene),
					redo: AddOperation.ApplyNextState,
					undo: AddOperation.ApplyPrevState
				);
			}
			ImGui.EndDisabled();
			
			ImGui.SameLine();
			if(ImGui.Button("Cancel")) {
				ImGui.CloseCurrentPopup();
			}
			
			ImGui.EndPopup();
		}
		
		ImGui.SameLine();
		ImGui.BeginDisabled(Program.SelectedScene == null);
		
		if(ImGui.Button(Codicons.Copy)) {
			sceneReposVar = new(0);
			sceneRenameBuffer = "";
			ImGui.OpenPopup("copy-scene");
		}
		
		if(ImGui.BeginPopup("copy-scene")) {
			ImGui.Text("Copy scene");
			
			ImGui.InputText("New ID", ref sceneRenameBuffer, Program.IMGUI_STRING_MAX);
			ImGui.DragInt2("Position", ref sceneReposVar.X, 1);
			
			Scene src = Program.SelectedScene;
			
			bool valid = sceneRenameBuffer != "";
			foreach(var s in world.Scenes) {
				if(s.ID == sceneRenameBuffer) {
					valid = false;
					break;
				}
				Rectangle r = new(s.WorldX, s.WorldY, s.TileCountX, s.TileCountY);
				if(r.IntersectsWith(new(sceneReposVar.X, sceneReposVar.Y, src.TileCountX, src.TileCountY))) {
					valid = false;
					break;
				}
			}
			
			Program.CanvasPanel.EnableScenePreview(new(sceneReposVar.X, sceneReposVar.Y, src.TileCountX, src.TileCountY));
			scenePreviewShown = true;
			
			ImGui.BeginDisabled(!valid);
			if(ImGui.Button("Copy")) {
				Scene newScene = world.CopyScene(src, sceneRenameBuffer, sceneReposVar.X, sceneReposVar.Y);
				Program.SetSelectedScene(newScene);
				ImGui.CloseCurrentPopup();
				Program.File.ApplyEdit(this, new AddOperation(world, newScene),
					redo: AddOperation.ApplyNextState,
					undo: AddOperation.ApplyPrevState
				);
			}
			ImGui.EndDisabled();
			
			ImGui.SameLine();
			if(ImGui.Button("Cancel")) {
				ImGui.CloseCurrentPopup();
			}
			
			ImGui.EndPopup();
		}
		
		ImGui.SameLine();
		
		if(ImGui.Button(Codicons.Trash)) {
			ImGui.OpenPopup("delete-scene");
		}
		
		if(ImGui.BeginPopup("delete-scene")) {
			ImGui.Text("Delete scene?");
			if(ImGui.Button("Confirm")) {
				ImGui.CloseCurrentPopup();
				Program.File.ApplyEdit(this, new RemoveOperation(world, Program.SelectedScene),
					redo: RemoveOperation.ApplyNextState,
					undo: RemoveOperation.ApplyPrevState
				);
				// if(world.SceneCount > 0) {
				// 	Program.SetSelectedScene(world.GetScene(0));
				// } else {
				// 	Program.SetSelectedScene(null);
				// }
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

		int sceneIndex = -1;
		if(Program.SelectedScene != null) {
			sceneIndex = world.GetSceneIndex(Program.SelectedScene);
		}
		ImGui.BeginDisabled(sceneIndex <= 0);
		if(ImGui.Button(Codicons.ChevronUp)) {
			Program.File.ApplyEdit(this, new MoveOperation(world, sceneIndex, sceneIndex-1),
				redo: MoveOperation.ApplyNextState,
				undo: MoveOperation.ApplyPrevState
			);
		}
		ImGui.EndDisabled();
		ImGui.SameLine();
		ImGui.BeginDisabled(sceneIndex >= world.SceneCount - 1);
		if(ImGui.Button(Codicons.ChevronDown)) { 
			Program.File.ApplyEdit(this, new MoveOperation(world, sceneIndex, sceneIndex+1),
				redo: MoveOperation.ApplyNextState,
				undo: MoveOperation.ApplyPrevState
			);
		}
		ImGui.EndDisabled();
		
		ImGui.SameLine();
		ImGui.SetCursorPosX(ImGui.GetCursorPosX() + ImGui.GetContentRegionAvail().X - ImGui.CalcTextSize(Codicons.OpenInProduct).X - 12);
		if(ImGui.Button(Codicons.OpenInProduct)) {
			Program.CanvasPanel.LocateScene(Program.SelectedScene);
		}
		
		ImGui.EndDisabled();

		if(Program.SelectedScene != null) {
			ImGui.SeparatorText("Scene Options");
			
			Scene scene = Program.SelectedScene;
			string id = scene.ID;
			if(ImGui.InputText("ID", ref id, 512, ImGuiInputTextFlags.EnterReturnsTrue)) {
				bool valid = true;
				foreach(var s in world.Scenes) {
					if(s.ID == id) {
						valid = false;
						break;
					}
				}
				if(valid) {
					Program.File.ApplyEdit(this, new RenameOperation(scene, id),
						redo: RenameOperation.ApplyNextState,
						undo: RenameOperation.ApplyPrevState
					);
				}
			}
			
			if(ImGui.Button($"{scene.WorldX}, {scene.WorldY}", new Vector2(ImGui.CalcItemWidth(), 0))) {
				sceneReposVar = new(scene.WorldX, scene.WorldY);
				ImGui.OpenPopup("repos");
			}
			ImGui.SameLine();
			ImGui.SetCursorPosX(ImGui.GetCursorPosX() - ImGui.GetStyle().ItemInnerSpacing.X);
			ImGui.Text("Position");
			
			if(ImGui.BeginPopup("repos")) {
				ImGui.Text("Position scene");
				ImGui.DragInt2("##New Position", ref sceneReposVar.X, 1);
				
				Program.CanvasPanel.EnableScenePreview(new(sceneReposVar.X, sceneReposVar.Y, scene.TileCountX, scene.TileCountY), scene);
				scenePreviewShown = true;

				bool valid = true;
				foreach(var s in world.Scenes) {
					if(s == scene) continue;
					Rectangle r = new(s.WorldX, s.WorldY, s.TileCountX, s.TileCountY);
					if(r.IntersectsWith(new(sceneReposVar.X, sceneReposVar.Y, scene.TileCountX, scene.TileCountY))) {
						valid = false;
						break;
					}
				}
				
				ImGui.BeginDisabled(!valid);
				if(ImGui.Button("Confirm")) {
					ImGui.CloseCurrentPopup();
					Program.File.ApplyEdit(this, new RepositionOperation(scene, new(sceneReposVar.X, sceneReposVar.Y)),
						redo: RepositionOperation.ApplyNextState,
						undo: RepositionOperation.ApplyPrevState
					);
				}
				ImGui.EndDisabled();
				
				ImGui.SameLine();
				ImGui.SetCursorPosX(ImGui.GetCursorPosX() + ImGui.GetContentRegionAvail().X - ImGui.CalcTextSize("Cancel").X - ImGui.GetStyle().FramePadding.X * 2);
				if(ImGui.Button("Cancel")) {
					ImGui.CloseCurrentPopup();
				}
				ImGui.EndPopup();
			}
			
			
			if(ImGui.Button($"{scene.TileCountX}, {scene.TileCountY}", new Vector2(ImGui.CalcItemWidth(), 0))) {
				sceneResizeVar = new(scene.TileCountX, scene.TileCountY);
				ImGui.OpenPopup("resize");
			}
			ImGui.SameLine();
			ImGui.SetCursorPosX(ImGui.GetCursorPosX() - ImGui.GetStyle().ItemInnerSpacing.X);
			ImGui.Text("Size");
			
			if(ImGui.BeginPopup("resize")) {
				ImGui.Text("Resize scene");
				if(ImGui.DragInt2("##New Size", ref sceneResizeVar.X, 1)) {
					if(sceneResizeVar.X < 1) sceneResizeVar.X = 1;
					if(sceneResizeVar.Y < 1) sceneResizeVar.Y = 1;
				}
				
				Program.CanvasPanel.EnableScenePreview(new(scene.WorldX, scene.WorldY, sceneResizeVar.X, sceneResizeVar.Y), scene);
				scenePreviewShown = true;
				
				bool valid = true;
				foreach(var s in world.Scenes) {
					if(s == scene) continue;
					Rectangle r = new(s.WorldX, s.WorldY, s.TileCountX, s.TileCountY);
					if(r.IntersectsWith(new(scene.WorldX, scene.WorldY, sceneResizeVar.X, sceneResizeVar.Y))) {
						valid = false;
						break;
					}
				}
				
				ImGui.BeginDisabled(!valid);
				if(ImGui.Button("Confirm")) {
					ImGui.CloseCurrentPopup();
					
					// TODO: undo/redo
					scene.Resize(sceneResizeVar.X, sceneResizeVar.Y);
					Program.File.MarkDirty();
					Program.File.ClearEditHistory();
				}
				ImGui.EndDisabled();
				
				ImGui.SameLine();
				ImGui.SetCursorPosX(ImGui.GetCursorPosX() + ImGui.GetContentRegionAvail().X - ImGui.CalcTextSize("Cancel").X - ImGui.GetStyle().FramePadding.X * 2);
				if(ImGui.Button("Cancel")) {
					ImGui.CloseCurrentPopup();
				}
				ImGui.EndPopup();
			}
			
			PropertyView.Run(scene.Properties);
		} else {
			ImGui.Text("No scene selected...");
		}

		if(!scenePreviewShown) {
			Program.CanvasPanel.DisableScenePreview();
		}
	}

	public class MoveOperation {
		private World world;
		private int index1;
		private int index2;
		public MoveOperation(World world, int index1, int index2) {
			this.world = world;
			this.index1 = index1;
			this.index2 = index2;
		}
		public static void ApplyNextState(FileEditEntry entry) {
			var op = entry.GetData<MoveOperation>();
			op.world.SwapScenes(op.index1, op.index2);
		}
		public static void ApplyPrevState(FileEditEntry entry) {
			var op = entry.GetData<MoveOperation>();
			op.world.SwapScenes(op.index2, op.index1);
		}
	}
	
	public class AddOperation {
		private World world;
		private Scene scene;
		public AddOperation(World world, Scene scene) {
			this.world = world;
			this.scene = scene;
		}
		public static void ApplyNextState(FileEditEntry entry) {
			var op = entry.GetData<AddOperation>();
			op.world.InsertScene(op.scene, op.world.SceneCount);
		}
		public static void ApplyPrevState(FileEditEntry entry) {
			var op = entry.GetData<AddOperation>();
			op.world.RemoveScene(op.scene);
			if(op.scene == Program.SelectedScene) {
				Program.SetSelectedScene(null);
			}
		}
	}
	
	public class RemoveOperation {
		private World world;
		private Scene scene;
		private int index;
		public RemoveOperation(World world, Scene scene) {
			this.world = world;
			this.scene = scene;
			this.index = world.GetSceneIndex(scene);
		}
		public static void ApplyNextState(FileEditEntry entry) {
			var op = entry.GetData<RemoveOperation>();
			op.world.RemoveScene(op.scene);
			if(op.scene == Program.SelectedScene) {
				Program.SetSelectedScene(null);
			}
		}
		public static void ApplyPrevState(FileEditEntry entry) {
			var op = entry.GetData<RemoveOperation>();
			op.world.InsertScene(op.scene, op.index);
		}
	}
	
	public class RenameOperation {
		private Scene scene;
		private string oldName;
		private string newName;
		public RenameOperation(Scene scene, string newName) {
			this.scene = scene;
			this.oldName = scene.ID;
			this.newName = newName;
		}
		public static void ApplyNextState(FileEditEntry entry) {
			var op = entry.GetData<RenameOperation>();
			op.scene.ID = op.newName;
		}
		public static void ApplyPrevState(FileEditEntry entry) {
			var op = entry.GetData<RenameOperation>();
			op.scene.ID = op.oldName;
		}
	}
	
	public class RepositionOperation {
		private Scene scene;
		private Point oldPosition;
		private Point newPosition;
		public RepositionOperation(Scene scene, Point newPosition) {
			this.scene = scene;
			this.oldPosition = new(scene.WorldX, scene.WorldY);
			this.newPosition = newPosition;
		}
		public static void ApplyNextState(FileEditEntry entry) {
			var op = entry.GetData<RepositionOperation>();
			op.scene.WorldX = op.newPosition.X;
			op.scene.WorldY = op.newPosition.Y;
		}
		public static void ApplyPrevState(FileEditEntry entry) {
			var op = entry.GetData<RepositionOperation>();
			op.scene.WorldX = op.oldPosition.X;
			op.scene.WorldY = op.oldPosition.Y;
		}
	}
	
}