using System.Numerics;
using ImGuiNET;

namespace L2D;

public class CanvasPanel : Panel {

	public CanvasPanel() {
		Title = "Canvas";

		flags |= ImGuiWindowFlags.NoScrollWithMouse;
		flags |= ImGuiWindowFlags.AlwaysHorizontalScrollbar;
		flags |= ImGuiWindowFlags.AlwaysVerticalScrollbar;
	}

	protected override void Update() {
		if(Program.File == null) {
			ImGui.Text("No file loaded...");
			return;
		}
		if(Program.File.World == null) {
			ImGui.Text("No world active...");
			return;
		}
		
		ImGui.Dummy(Vector2.One*4000);
	}
}