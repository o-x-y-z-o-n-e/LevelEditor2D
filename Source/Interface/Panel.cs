using ImGuiNET;

namespace L2D; 

public class Panel {
	
	public bool IsOpen => isOpen;

	public string Title;

	private bool isOpen;
	protected ImGuiWindowFlags flags;

	public Panel() {
		flags |= ImGuiWindowFlags.NoCollapse;
	}
	
	public void Execute() {
		isOpen = ImGui.Begin(Title, flags);
		if(isOpen) {
			Update();
		}
		ImGui.End();
	}

	protected virtual void Update() {
		
	}
	
}