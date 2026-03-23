using IconFonts;
using ImGuiNET;

namespace L2D; 


public static class PropertyView {

	private static string newPropertyName = "";
	private static PropertyType newPropertyType = PropertyType.String;
	private static string newPropertyString = "";
	private static int newPropertyInteger = 0;
	private static float newPropertyFloat = 0;
	private static bool newPropertyBoolean = false;

	public static void Run(PropertyCollection collection) {
		ImGui.PushID("custom-properties");
		ImGui.SeparatorText("Custom Properties");

		int pIndex = 0;
		int pMoveSrc = -1;
		int pMoveDst = -1;
		int pDelete = -1;
		foreach(var p in collection.All) {
			ImGui.PushID(pIndex);
			if(p.Type == PropertyType.String) {
				string str = p.String;
				if(ImGui.InputText("##", ref str, 512, ImGuiInputTextFlags.EnterReturnsTrue)) {
					Program.File.ApplyEdit(collection, new Tuple<string, string>(p.String, str),
						redo: entry => { p.String = entry.GetData<Tuple<string, string>>().Item2; },
						undo: entry => { p.String = entry.GetData<Tuple<string, string>>().Item1; }
					);
				}
			} else if(p.Type == PropertyType.Integer) {
				int i = p.Integer;
				if(ImGui.InputInt("##", ref i, 1, 10)) {
					if(ImGui.IsItemDeactivatedAfterEdit() || ImGui.IsItemClicked()) {
						Program.File.ApplyEdit(collection, new Tuple<int, int>(p.Integer, i),
							redo: entry => { p.Integer = entry.GetData<Tuple<int, int>>().Item2; },
							undo: entry => { p.Integer = entry.GetData<Tuple<int, int>>().Item1; }
						);
					}
				}
			} else if(p.Type == PropertyType.Float) {
				float f = p.Float;
				if(ImGui.InputFloat("##", ref f, 1.0F, 10.0F, "%.2f")) {
					if(ImGui.IsItemDeactivatedAfterEdit() || ImGui.IsItemClicked()) {
						Program.File.ApplyEdit(collection, new Tuple<float, float>(p.Float, f),
							redo: entry => { p.Float = entry.GetData<Tuple<float, float>>().Item2; },
							undo: entry => { p.Float = entry.GetData<Tuple<float, float>>().Item1; }
						);
					}
				}
			} else if(p.Type == PropertyType.Boolean) {
				if(ImGui.BeginCombo("##", p.Boolean ? "True" : "False")) {
					bool b = p.Boolean;
					if(ImGui.Selectable("True", b)) {
						b = true;
					}
					if(ImGui.Selectable("False", !b)) {
						b = false;
					}
					if(b != p.Boolean) {
						Program.File.ApplyEdit(collection, new Tuple<bool, bool>(p.Boolean, b),
							redo: entry => { p.Boolean = entry.GetData<Tuple<bool, bool>>().Item2; },
							undo: entry => { p.Boolean = entry.GetData<Tuple<bool, bool>>().Item1; }
						);
					}
					ImGui.EndCombo();
				}
			}

			ImGui.SameLine();
			ImGui.SetCursorPosX(ImGui.GetCursorPosX() - ImGui.GetStyle().ItemInnerSpacing.X);
			if(ImGui.Selectable(p.Name)) {
				ImGui.OpenPopup("context");
			}

			bool rename = false;
			bool convert = false;
			if(ImGui.BeginPopup("context")) {
				if(ImGui.MenuItem("Rename")) {
					rename = true;
					ImGui.CloseCurrentPopup();
				}

				if(ImGui.MenuItem("Convert")) {
					convert = true;
					ImGui.CloseCurrentPopup();
				}

				ImGui.BeginDisabled(pIndex == 0);
				if(ImGui.MenuItem("Move Up")) {
					pMoveSrc = pIndex;
					pMoveDst = pIndex - 1;
				}

				ImGui.EndDisabled();
				ImGui.BeginDisabled(pIndex == collection.Count - 1);
				if(ImGui.MenuItem("Move Down")) {
					pMoveSrc = pIndex;
					pMoveDst = pIndex + 1;
				}

				ImGui.EndDisabled();
				if(ImGui.MenuItem("Remove")) {
					pDelete = pIndex;
				}

				ImGui.EndPopup();
			}

			if(rename) ImGui.OpenPopup("rename");
			if(convert) ImGui.OpenPopup("convert");
			
			if(ImGui.BeginPopup("rename")) {
				string name = p.Name;
				if(ImGui.InputText("Name", ref name, 512, ImGuiInputTextFlags.EnterReturnsTrue)) {
					Program.File.ApplyEdit(collection, new Tuple<string, string>(p.Name, name),
						redo: entry => {
							p.Name = entry.GetData<Tuple<string, string>>().Item2;
						},
						undo: entry => {
							p.Name = entry.GetData<Tuple<string, string>>().Item1;
						}
					);
				}
				ImGui.EndPopup();
			}

			if(ImGui.BeginPopup("convert")) {
				bool close = false;
				if(ImGui.BeginCombo("Type", p.Type.ToString())) {
					if(ImGui.Selectable("String", p.Type == PropertyType.String)) {
						string newString = "";
						if(p.Type == PropertyType.Integer) {
							newString = p.Integer.ToString();
						}
						if(p.Type == PropertyType.Float) {
							newString = p.Float.ToString();
						}
						if(p.Type == PropertyType.Boolean) {
							newString = p.Boolean.ToString();
						}
						Program.File.ApplyEdit(collection, new Property.ConvertOperation(p, newString));
						close = true;
					}
					if(ImGui.Selectable("Integer", p.Type == PropertyType.Integer)) {
						int newInteger = 0;
						if(p.Type == PropertyType.String) {
							int.TryParse(p.String, out newInteger);
						}
						if(p.Type == PropertyType.Float) {
							newInteger = (int)p.Float;
						}
						if(p.Type == PropertyType.Boolean) {
							newInteger = p.Boolean ? 1 : 0;
						}
						Program.File.ApplyEdit(collection, new Property.ConvertOperation(p, newInteger));
						close = true;
					}
					if(ImGui.Selectable("Float", p.Type == PropertyType.Float)) {
						float newFloat = 0.0F;
						if(p.Type == PropertyType.String) {
							float.TryParse(p.String, out newFloat);
						}
						if(p.Type == PropertyType.Integer) {
							newFloat = p.Integer;
						}
						if(p.Type == PropertyType.Boolean) {
							newFloat = p.Boolean ? 1 : 0;
						}
						Program.File.ApplyEdit(collection, new Property.ConvertOperation(p, newFloat));
						close = true;
					}

					if(ImGui.Selectable("Boolean", p.Type == PropertyType.Boolean)) {
						bool newBoolean = false;
						if(p.Type == PropertyType.String) {
							bool.TryParse(p.String, out newBoolean);
						}
						if(p.Type == PropertyType.Integer) {
							newBoolean = p.Integer != 0;
						}
						if(p.Type == PropertyType.Float) {
							newBoolean = p.Float != 0.0F;
						}
						Program.File.ApplyEdit(collection, new Property.ConvertOperation(p, newBoolean));
						close = true;
					}

					ImGui.EndCombo();
				}
				
				if(close) ImGui.CloseCurrentPopup();

				ImGui.EndPopup();
			}

			ImGui.PopID();
			pIndex++;
		}

		if(pDelete >= 0) {
			Program.File.ApplyEdit(collection, new Property.RemoveOperation(collection, pDelete));
		} else if(pMoveSrc >= 0 && pMoveDst >= 0) {
			Program.File.ApplyEdit(collection, new Property.MoveOperation(collection, pMoveSrc, pMoveDst));
		}

		if(ImGui.Button(Codicons.Add)) {
			newPropertyName = "";
			newPropertyString = "";
			newPropertyInteger = 0;
			newPropertyFloat = 0;
			newPropertyBoolean = false;
			ImGui.OpenPopup("add-property");
		}

		if(ImGui.BeginPopup("add-property")) {
			ImGui.InputText("Name", ref newPropertyName, 512);
			if(ImGui.BeginCombo("Type", newPropertyType.ToString())) {
				if(ImGui.Selectable("String", newPropertyType == PropertyType.String)) {
					newPropertyType = PropertyType.String;
					newPropertyString = "";
				}
				if(ImGui.Selectable("Integer", newPropertyType == PropertyType.Integer)) {
					newPropertyType = PropertyType.Integer;
					newPropertyInteger = 0;
				}
				if(ImGui.Selectable("Float", newPropertyType == PropertyType.Float)) {
					newPropertyType = PropertyType.Float;
					newPropertyFloat = 0;
				}
				if(ImGui.Selectable("Boolean", newPropertyType == PropertyType.Boolean)) {
					newPropertyType = PropertyType.Boolean;
					newPropertyBoolean = false;
				}
				ImGui.EndCombo();
			}
			if(newPropertyType == PropertyType.String) {
				ImGui.InputText("Value", ref newPropertyString, 512);
			}
			if(newPropertyType == PropertyType.Integer) {
				ImGui.InputInt("Value", ref newPropertyInteger);
			}
			if(newPropertyType == PropertyType.Float) {
				ImGui.InputFloat("Value", ref newPropertyFloat);
			}
			if(newPropertyType == PropertyType.Boolean) {
				if(ImGui.BeginCombo("##", newPropertyBoolean ? "True" : "False")) {
					if(ImGui.Selectable("True", newPropertyBoolean)) newPropertyBoolean = true;
					if(ImGui.Selectable("False", !newPropertyBoolean)) newPropertyBoolean = false;
					ImGui.EndCombo();
				}
				ImGui.SameLine();
				ImGui.SetCursorPosX(ImGui.GetCursorPosX() - ImGui.GetStyle().ItemInnerSpacing.X);
				ImGui.Text("Value");
			}
			ImGui.BeginDisabled(newPropertyName == "");
			if(ImGui.Button("Add")) {
				Property property = new Property();
				property.Name = newPropertyName;
				property.Type = newPropertyType;
				if(newPropertyType == PropertyType.String) property.String = newPropertyString;
				if(newPropertyType == PropertyType.Integer) property.Integer = newPropertyInteger;
				if(newPropertyType == PropertyType.Float) property.Float = newPropertyFloat;
				if(newPropertyType == PropertyType.Boolean) property.Boolean = newPropertyBoolean;
				Program.File.ApplyEdit(collection, new Property.AddOperation(collection, property));
				ImGui.CloseCurrentPopup();
			}
			ImGui.EndDisabled();
			ImGui.SameLine();
			if(ImGui.Button("Cancel")) {
				ImGui.CloseCurrentPopup();
			}
			ImGui.EndPopup();
		}
		
		ImGui.PopID();
	}
	
}