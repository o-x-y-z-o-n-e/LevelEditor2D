using ImGuiNET;

namespace L2D; 

public class Panel {

	public string Title;

	private bool open;
	protected ImGuiWindowFlags flags;

	public Panel() {
		flags |= ImGuiWindowFlags.NoCollapse;
	}
	
	public void Execute() {
		if(ImGui.Begin(Title, flags)) {
			Update();
		}
		ImGui.End();
	}

	protected virtual void Update() {
		
	}
	
}