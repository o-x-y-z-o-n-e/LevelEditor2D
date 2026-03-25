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

	public static void Run(PropertyCollection collection, PropertyCollection template = null) {
		ImGui.PushID("custom-properties");
		ImGui.SeparatorText("Custom Properties");

		int id = 0;
		Property delete = null;

		if(template != null) {
			foreach(var templateProperty in template.All) {
				Property overrideProperty = null;
				foreach(var entityProperty in collection.All) {
					if(entityProperty.Name == templateProperty.Name) {
						overrideProperty = entityProperty;
						break;
					}
				}
				if(overrideProperty != null) {
					if(PropertyItem(collection, overrideProperty, false, true, ref id)) {
						delete = overrideProperty;
					}
				} else {
					PropertyItem(collection, templateProperty, true, false, ref id);
				}
			}
		}
		
		foreach(var property in collection.All) {
			if(template == null || !template.Contains(property.Name)) {
				if(PropertyItem(collection, property, false, false, ref id)) {
					delete = property;
				}
			}
		}

		if(delete != null) {
			Program.File.ApplyEdit(collection, new Property.RemoveOperation(collection, delete));
		}
		
		// TODO: reordering incompatible with templates (maybe only allow for non-templated entities?)
		// if(pMoveSrc >= 0 && pMoveDst >= 0) {
		// 	Program.File.ApplyEdit(collection, new Property.MoveOperation(collection, pMoveSrc, pMoveDst));
		// }

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
				if(collection.Contains(newPropertyName)) {
					Property property = collection.Get(newPropertyName);
					if(property.Type == newPropertyType) {
						// TODO: update value
					} else {
						if(newPropertyType == PropertyType.String) {
							Program.File.ApplyEdit(collection, new Property.ConvertOperation(property, newPropertyString));
						}
						if(newPropertyType == PropertyType.Integer) {
							Program.File.ApplyEdit(collection, new Property.ConvertOperation(property, newPropertyInteger));
						}
						if(newPropertyType == PropertyType.Float) {
							Program.File.ApplyEdit(collection, new Property.ConvertOperation(property, newPropertyFloat));
						}
						if(newPropertyType == PropertyType.Boolean) {
							Program.File.ApplyEdit(collection, new Property.ConvertOperation(property, newPropertyBoolean));
						}
					}
				} else {
					Property property = new Property();
					property.Name = newPropertyName;
					property.Type = newPropertyType;
					if(newPropertyType == PropertyType.String) property.String = newPropertyString;
					if(newPropertyType == PropertyType.Integer) property.Integer = newPropertyInteger;
					if(newPropertyType == PropertyType.Float) property.Float = newPropertyFloat;
					if(newPropertyType == PropertyType.Boolean) property.Boolean = newPropertyBoolean;
					Program.File.ApplyEdit(collection, new Property.AddOperation(collection, property));
				}
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

	private static bool PropertyItem(PropertyCollection collection, Property property, bool isTemplate, bool hasTemplate, ref int id) {
		id++;

		bool delete = false;
		
		ImGui.PushID(id);
		
		if(isTemplate) {
			ImGui.PushStyleVar(ImGuiStyleVar.Alpha, ImGui.GetStyle().DisabledAlpha);
		}
		
		if(property.Type == PropertyType.String) {
			string str = property.String;
			if(ImGui.InputText("##", ref str, 512)) {
				if(ImGui.IsItemDeactivatedAfterEdit()) {
					if(isTemplate) {
						Property newProperty = new Property();
						newProperty.Name = property.Name;
						newProperty.Type = property.Type;
						newProperty.String = str;
						Program.File.ApplyEdit(collection, new Property.AddOperation(collection, newProperty));
					} else {
						Program.File.ApplyEdit(collection, new Tuple<string, string>(property.String, str),
							redo: entry => { property.String = entry.GetData<Tuple<string, string>>().Item2; },
							undo: entry => { property.String = entry.GetData<Tuple<string, string>>().Item1; }
						);
					}
				}
			}
		} else if(property.Type == PropertyType.Integer) {
			int i = property.Integer;
			if(ImGui.InputInt("##", ref i, 1, 10)) {
				if(ImGui.IsItemDeactivatedAfterEdit() || ImGui.IsItemClicked()) {
					if(isTemplate) {
						Property newProperty = new Property();
						newProperty.Name = property.Name;
						newProperty.Type = property.Type;
						newProperty.Integer = i;
						Program.File.ApplyEdit(collection, new Property.AddOperation(collection, newProperty));
					} else {
						Program.File.ApplyEdit(collection, new Tuple<int, int>(property.Integer, i),
							redo: entry => { property.Integer = entry.GetData<Tuple<int, int>>().Item2; },
							undo: entry => { property.Integer = entry.GetData<Tuple<int, int>>().Item1; }
						);
					}
				}
			}
		} else if(property.Type == PropertyType.Float) {
			float f = property.Float;
			if(ImGui.InputFloat("##", ref f, 1.0F, 10.0F, "%.2f")) {
				if(ImGui.IsItemDeactivatedAfterEdit() || ImGui.IsItemClicked()) {
					if(isTemplate) {
						Property newProperty = new Property();
						newProperty.Name = property.Name;
						newProperty.Type = property.Type;
						newProperty.Float = f;
						Program.File.ApplyEdit(collection, new Property.AddOperation(collection, newProperty));
					} else {
						Program.File.ApplyEdit(collection, new Tuple<float, float>(property.Float, f),
							redo: entry => { property.Float = entry.GetData<Tuple<float, float>>().Item2; },
							undo: entry => { property.Float = entry.GetData<Tuple<float, float>>().Item1; }
						);
					}
				}
			}
		} else if(property.Type == PropertyType.Boolean) {
			if(ImGui.BeginCombo("##", property.Boolean ? "True" : "False")) {
				bool b = property.Boolean;
				if(ImGui.Selectable("True", b)) {
					b = true;
				}

				if(ImGui.Selectable("False", !b)) {
					b = false;
				}

				if(b != property.Boolean) {
					if(isTemplate) {
						Property newProperty = new Property();
						newProperty.Name = property.Name;
						newProperty.Type = property.Type;
						newProperty.Boolean = b;
						Program.File.ApplyEdit(collection, new Property.AddOperation(collection, newProperty));
					} else {
						Program.File.ApplyEdit(collection, new Tuple<bool, bool>(property.Boolean, b),
							redo: entry => { property.Boolean = entry.GetData<Tuple<bool, bool>>().Item2; },
							undo: entry => { property.Boolean = entry.GetData<Tuple<bool, bool>>().Item1; }
						);
					}
				}

				ImGui.EndCombo();
			}
		}
		
		if(hasTemplate) {
			ImGui.OpenPopupOnItemClick("reset", ImGuiPopupFlags.MouseButtonRight);
			if(ImGui.BeginPopup("reset")) {
				if(ImGui.MenuItem("Reset")) {
					delete = true;
				}

				ImGui.EndPopup();
			}
		}

		if(isTemplate) {
			ImGui.PopStyleVar(); // ImGuiStyleVar.Alpha
		}

		ImGui.SameLine();
		ImGui.SetCursorPosX(ImGui.GetCursorPosX() - ImGui.GetStyle().ItemInnerSpacing.X);
		if(isTemplate) ImGui.PushStyleVar(ImGuiStyleVar.Alpha, ImGui.GetStyle().DisabledAlpha);
		if(ImGui.Selectable(property.Name)) {
			ImGui.OpenPopup("context");
		}
		if(isTemplate) ImGui.PopStyleVar(); // ImGuiStyleVar.Alpha

		bool rename = false;
		bool convert = false;
		if(ImGui.BeginPopup("context")) {
			ImGui.BeginDisabled(isTemplate);
			if(ImGui.MenuItem("Rename")) {
				rename = true;
				ImGui.CloseCurrentPopup();
			}

			if(ImGui.MenuItem("Convert")) {
				convert = true;
				ImGui.CloseCurrentPopup();
			}

			// TODO: reordering incompatible with templates (maybe only allow for non-templated entities?)
			// ImGui.BeginDisabled(pIndex == 0);
			// if(ImGui.MenuItem("Move Up")) {
			// 	pMoveSrc = pIndex;
			// 	pMoveDst = pIndex - 1;
			// }
			// ImGui.EndDisabled();
			// ImGui.BeginDisabled(pIndex == collection.Count - 1);
			// if(ImGui.MenuItem("Move Down")) {
			// 	pMoveSrc = pIndex;
			// 	pMoveDst = pIndex + 1;
			// }
			// ImGui.EndDisabled();
			
			if(ImGui.MenuItem("Remove")) {
				// TODO: pDelete = index;
				delete = true;
				ImGui.CloseCurrentPopup();
			}

			ImGui.EndDisabled(); // readOnly
			ImGui.EndPopup();
		}

		if(rename) ImGui.OpenPopup("rename");
		if(convert) ImGui.OpenPopup("convert");

		if(ImGui.BeginPopup("rename")) {
			string name = property.Name;
			if(ImGui.InputText("Name", ref name, 512, ImGuiInputTextFlags.EnterReturnsTrue)) {
				if(collection.Contains(name)) {
					// TODO: delete & replace
				} else {
					Program.File.ApplyEdit(collection, new Tuple<string, string>(property.Name, name),
						redo: entry => { property.Name = entry.GetData<Tuple<string, string>>().Item2; },
						undo: entry => { property.Name = entry.GetData<Tuple<string, string>>().Item1; }
					);
				}
			}
			ImGui.EndPopup();
		}

		if(ImGui.BeginPopup("convert")) {
			bool close = false;
			if(ImGui.BeginCombo("Type", property.Type.ToString())) {
				if(ImGui.Selectable("String", property.Type == PropertyType.String)) {
					string newString = "";
					if(property.Type == PropertyType.Integer) {
						newString = property.Integer.ToString();
					}

					if(property.Type == PropertyType.Float) {
						newString = property.Float.ToString();
					}

					if(property.Type == PropertyType.Boolean) {
						newString = property.Boolean.ToString();
					}

					Program.File.ApplyEdit(collection, new Property.ConvertOperation(property, newString));
					close = true;
				}

				if(ImGui.Selectable("Integer", property.Type == PropertyType.Integer)) {
					int newInteger = 0;
					if(property.Type == PropertyType.String) {
						int.TryParse(property.String, out newInteger);
					}

					if(property.Type == PropertyType.Float) {
						newInteger = (int)property.Float;
					}

					if(property.Type == PropertyType.Boolean) {
						newInteger = property.Boolean ? 1 : 0;
					}

					Program.File.ApplyEdit(collection, new Property.ConvertOperation(property, newInteger));
					close = true;
				}

				if(ImGui.Selectable("Float", property.Type == PropertyType.Float)) {
					float newFloat = 0.0F;
					if(property.Type == PropertyType.String) {
						float.TryParse(property.String, out newFloat);
					}

					if(property.Type == PropertyType.Integer) {
						newFloat = property.Integer;
					}

					if(property.Type == PropertyType.Boolean) {
						newFloat = property.Boolean ? 1 : 0;
					}

					Program.File.ApplyEdit(collection, new Property.ConvertOperation(property, newFloat));
					close = true;
				}

				if(ImGui.Selectable("Boolean", property.Type == PropertyType.Boolean)) {
					bool newBoolean = false;
					if(property.Type == PropertyType.String) {
						bool.TryParse(property.String, out newBoolean);
					}

					if(property.Type == PropertyType.Integer) {
						newBoolean = property.Integer != 0;
					}

					if(property.Type == PropertyType.Float) {
						newBoolean = property.Float != 0.0F;
					}

					Program.File.ApplyEdit(collection, new Property.ConvertOperation(property, newBoolean));
					close = true;
				}

				ImGui.EndCombo();
			}

			if(close) ImGui.CloseCurrentPopup();

			ImGui.EndPopup();
		}

		ImGui.PopID(); // id
		return delete;
	}

}