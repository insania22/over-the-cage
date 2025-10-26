using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Layouts;
using UnityEngine.InputSystem.Utilities;

[InputControlLayout(displayName = "Two Button Composite")]
public class TwoJump : InputBindingComposite<float>
{
    [InputControl(layout = "Button")] public int button1;
    [InputControl(layout = "Button")] public int button2;

    public override float ReadValue(ref InputBindingCompositeContext context)
    {
        var b1 = context.ReadValueAsButton(button1);
        var b2 = context.ReadValueAsButton(button2);
        return (b1 && b2) ? 1f : 0f;
    }

    public override float EvaluateMagnitude(ref InputBindingCompositeContext context)
        => ReadValue(ref context);

    static TwoJump()
    {
        InputSystem.RegisterBindingComposite<TwoJump>();
    }
}
