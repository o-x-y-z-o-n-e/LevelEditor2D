using System.Numerics;
using IconFonts;
using ImGuiNET;
using Serilog;

namespace L2D; 

public class EntitiesPanel : Panel {

	private FileEditEntry positionEdit;
	private FileEditEntry sizeEdit;

	public EntitiesPanel() {
		Title = "Entities";
		positionEdit = null;
		sizeEdit = null;
	}

	protected override void Update() {
		if(Program.File == null) {
			return;
		}
		
		if(Program.SelectedScene == null) {
			ImGui.Text("No scene selected...");
			return;
		}
		
		if(Program.SelectedLayer == null) {
			ImGui.Text("No layer selected...");
			return;
		}

		if(Program.SelectedLayer.Type != LayerType.Entities) {
			ImGui.Text("Selected layer is not used for entities...");
			return;
		}

		Layer layer = Program.SelectedLayer;
		Entity selected = Program.SelectedEntity;
		
		Vector2 listSize = ImGui.GetContentRegionAvail();
		listSize.Y -= 200;
		ImGui.BeginChild("entity-list", listSize, ImGuiChildFlags.Borders | ImGuiChildFlags.ResizeY);

		int copyIndex = -1;
		int deleteIndex = -1;
		int moveUpIndex = -1;
		int moveDownIndex = -1;

		int index = 0;
		foreach(var entity in layer.Entities.All) {
			ImGui.PushID(index);

			bool canvasHighlighted = entity == Program.CanvasPanel.EntityHighlight;
			
			if(canvasHighlighted) ImGui.PushStyleColor(ImGuiCol.Header, ImGui.GetStyle().Colors[(int)ImGuiCol.HeaderHovered]);
			if(ImGui.Selectable(entity.Name, entity == selected || canvasHighlighted)) {
				if(entity != selected) {
					Program.SetSelectedEntity(entity);
				} else {
					Program.SetSelectedEntity(null);
				}
			}
			if(canvasHighlighted) ImGui.PopStyleColor();
			
			if(ImGui.IsItemHovered()) {
				Program.CanvasPanel.ShowEntityHighlight(entity);
			}
			ImGui.OpenPopupOnItemClick("context", ImGuiPopupFlags.MouseButtonRight);
			if(ImGui.BeginPopup("context")) {
				if(ImGui.MenuItem("Copy")) {
					copyIndex = index;
				}
				if(ImGui.MenuItem("Delete")) {
					deleteIndex = index;
				}
				if(ImGui.MenuItem("Move Up")) {
					moveUpIndex = index;
				}
				if(ImGui.MenuItem("Move Down")) {
					moveDownIndex = index;
				}
				if(ImGui.MenuItem("Locate")) {
					Program.CanvasPanel.LocateEntity(entity);
				}
				ImGui.EndPopup();
			}
			ImGui.PopID();
			index++;
		}
		
		ImGui.EndChild();
		
		if(ImGui.Button(Codicons.DiffAdded)) {
			Entity entity = layer.Entities.Add();
			entity.Name = "New Entity";
			entity.Position = -Program.CanvasPanel.Camera - new Vector2(layer.Scene.WorldX * layer.Scene.World.TileWidth, layer.Scene.WorldY * layer.Scene.World.TileHeight);
			Program.SetSelectedEntity(entity);
			Program.File.ApplyEdit(this, new AddOperation(layer.Entities, entity));
		}
		
		ImGui.SameLine();
		ImGui.BeginDisabled(Program.SelectedEntity == null);
		
		if(ImGui.Button(Codicons.Copy)) {
			copyIndex = layer.Entities.IndexOf(Program.SelectedEntity);
		}

		if(copyIndex >= 0 && copyIndex < layer.Entities.Count) {
			Entity newEntity = layer.Entities.Copy(copyIndex);
			newEntity.Position += new Vector2(newEntity.Size.X + 16, 0);
			Program.File.ApplyEdit(this, new AddOperation(layer.Entities, newEntity));
		}
		
		ImGui.SameLine();
		
		if(ImGui.Button(Codicons.Trash)) {
			deleteIndex = layer.Entities.IndexOf(Program.SelectedEntity);
		}

		if(deleteIndex >= 0 && deleteIndex < layer.Entities.Count) {
			Program.File.ApplyEdit(this, new RemoveOperation(layer.Entities, layer.Entities.Get(deleteIndex)));
			if(layer.Entities.Count == 0) {
				Program.SetSelectedEntity(null);
			} else if(deleteIndex >= layer.Entities.Count) {
				Program.SetSelectedEntity(layer.Entities.Get(layer.Entities.Count - 1));
			} else {
				Program.SetSelectedEntity(layer.Entities.Get(deleteIndex));
			}
		}
		
		ImGui.SameLine();
		
		int selectedEntityIndex = -1;
		if(Program.SelectedEntity != null) {
			selectedEntityIndex = layer.Entities.IndexOf(Program.SelectedEntity);
		}
		ImGui.BeginDisabled(selectedEntityIndex <= 0);
		if(ImGui.Button(Codicons.ChevronUp)) {
			moveUpIndex = layer.Entities.IndexOf(Program.SelectedEntity);
		}
		ImGui.EndDisabled();
		ImGui.BeginDisabled(selectedEntityIndex >= layer.Entities.Count - 1);
		ImGui.SameLine();
		if(ImGui.Button(Codicons.ChevronDown)) {
			moveDownIndex = layer.Entities.IndexOf(Program.SelectedEntity);
		}
		ImGui.EndDisabled();

		if(moveUpIndex >= 1 && moveUpIndex < layer.Entities.Count) {
			Program.File.ApplyEdit(this, new MoveOperation(layer.Entities, moveUpIndex, moveUpIndex - 1));
		}
		
		if(moveDownIndex >= 0 && moveDownIndex < layer.Entities.Count - 1) {
			Program.File.ApplyEdit(this, new MoveOperation(layer.Entities, moveDownIndex, moveDownIndex + 1));
		}
		
		ImGui.SameLine();
		ImGui.SetCursorPosX(ImGui.GetCursorPosX() + ImGui.GetContentRegionAvail().X - ImGui.CalcTextSize(Codicons.OpenInProduct).X - 12);
		if(ImGui.Button(Codicons.OpenInProduct)) {
			Program.CanvasPanel.LocateEntity(Program.SelectedEntity);
		}
		
		ImGui.EndDisabled();

		if(selected != null) {
			ImGui.SeparatorText("Entity Options");
			string name = selected.Name;
			if(ImGui.InputText("Name", ref name, 512, ImGuiInputTextFlags.EnterReturnsTrue)) {
				Program.File.ApplyEdit(this, new Tuple<string, string>(selected.Name, name),
					redo: entry => {
						selected.Name = entry.GetData<Tuple<string, string>>().Item2;
					},
					undo: entry => {
						selected.Name = entry.GetData<Tuple<string, string>>().Item1;
					}
				);
            }
			string type = selected.Type;
			if(ImGui.InputText("Type", ref type, 512, ImGuiInputTextFlags.EnterReturnsTrue)) {
				Program.File.ApplyEdit(this, new Tuple<string, string>(selected.Type, type),
					redo: entry => {
						selected.Type = entry.GetData<Tuple<string, string>>().Item2;
					},
					undo: entry => {
						selected.Type = entry.GetData<Tuple<string, string>>().Item1;
					}
				);
            }
			Vector2 pos = selected.Position;
			if(ImGui.DragFloat2("Position", ref pos)) {
				if(positionEdit == null || positionEdit.GetData<PositionOperation>().Entity != selected) {
					positionEdit = Program.File.BeginEdit(this, new PositionOperation(selected, pos));
				} else {
					positionEdit.GetData<PositionOperation>().SetPosition(pos);
				}
				selected.Position = pos;
			}
			if(ImGui.IsItemDeactivatedAfterEdit() && positionEdit != null) {
				Program.File.EndEdit(ref positionEdit, !positionEdit.GetData<PositionOperation>().HasChanges());
			}
			Vector2 size = selected.Size;
			if(ImGui.DragFloat2("Size", ref size)) {
				if(size.X < 0) size.X = 0;
				if(size.Y < 0) size.Y = 0;
				if(sizeEdit == null || sizeEdit.GetData<SizeOperation>().Entity != selected) {
					sizeEdit = Program.File.BeginEdit(this, new SizeOperation(selected, size));
				} else {
					sizeEdit.GetData<SizeOperation>().SetSize(size);
				}
				selected.Size = size;
			}
			if(ImGui.IsItemDeactivatedAfterEdit() && sizeEdit != null) {
				Program.File.EndEdit(ref sizeEdit, !sizeEdit.GetData<SizeOperation>().HasChanges());
			}
			PropertyView.Run(selected.Properties);
		} else {
			ImGui.Text("No entity selected...");
		}
	}
	
	public class AddOperation : IFileEditOperation {
		private EntityCollection collection;
		private Entity entity;
		public AddOperation(EntityCollection collection, Entity entity) {
			this.collection = collection;
			this.entity = entity;
		}
		public void ApplyNextState(FileEditEntry entry) {
			var op = entry.GetData<AddOperation>();
			op.collection.Insert(op.entity, op.collection.Count);
		}
		public void ApplyPrevState(FileEditEntry entry) {
			var op = entry.GetData<AddOperation>();
			op.collection.Remove(op.entity);
			if(Program.SelectedEntity == op.entity) {
				Program.SetSelectedEntity(null);
			}
		}
		public bool HasChanges() => true;
	}

	public class MoveOperation : IFileEditOperation {
		private EntityCollection collection;
		private int index1;
		private int index2;
		public MoveOperation(EntityCollection collection, int index1, int index2) {
			this.collection = collection;
			this.index1 = index1;
			this.index2 = index2;
		}
		public void ApplyNextState(FileEditEntry entry) {
			var op = entry.GetData<MoveOperation>();
			op.collection.Move(op.index1, op.index2);
		}
		public void ApplyPrevState(FileEditEntry entry) {
			var op = entry.GetData<MoveOperation>();
			op.collection.Move(op.index2, op.index1);
		}
		public bool HasChanges() => index1 != index2;
	}
	
	public class RemoveOperation : IFileEditOperation {
		private EntityCollection collection;
		private Entity entity;
		private int index;
		public RemoveOperation(EntityCollection collection, Entity entity) {
			this.collection = collection;
			this.entity = entity;
			this.index = collection.IndexOf(entity);
		}
		public void ApplyNextState(FileEditEntry entry) {
			var op = entry.GetData<RemoveOperation>();
			op.collection.Remove(op.entity);
			if(Program.SelectedEntity == op.entity) {
				Program.SetSelectedEntity(null);
			}
		}
		public void ApplyPrevState(FileEditEntry entry) {
			var op = entry.GetData<RemoveOperation>();
			op.collection.Insert(op.entity, op.index);
		}
		public bool HasChanges() => true;
	}
	
	public class PositionOperation : IFileEditOperation {
		public Entity Entity => entity;
		private Entity entity;
		private Vector2 oldPosition;
		private Vector2 newPosition;
		public PositionOperation(Entity entity, Vector2 newPosition) {
			this.entity = entity;
			this.oldPosition = entity.Position;
			this.newPosition = newPosition;
		}
		public void SetPosition(Vector2 position) {
			newPosition = position;
		}
		public void ApplyNextState(FileEditEntry entry) {
			var op = entry.GetData<PositionOperation>();
			op.entity.Position = newPosition;
		}
		public void ApplyPrevState(FileEditEntry entry) {
			var op = entry.GetData<PositionOperation>();
			op.entity.Position = oldPosition;
		}
		public bool HasChanges() => oldPosition != newPosition;
	}
	
	public class SizeOperation : IFileEditOperation {
		public Entity Entity => entity;
		private Entity entity;
		private Vector2 oldSize;
		private Vector2 newSize;
		public SizeOperation(Entity entity, Vector2 newSize) {
			this.entity = entity;
			this.oldSize = entity.Size;
			this.newSize = newSize;
		}
		public void SetSize(Vector2 size) {
			newSize = size;
		}
		public void ApplyNextState(FileEditEntry entry) {
			var op = entry.GetData<SizeOperation>();
			op.entity.Size = newSize;
		}
		public void ApplyPrevState(FileEditEntry entry) {
			var op = entry.GetData<SizeOperation>();
			op.entity.Size = oldSize;
		}
		public bool HasChanges() => oldSize != newSize;
	}
}