using UnityEngine;

public enum Hand { Unknown, Left, Right }

// Vibrations des manettes, sans dépendre d'un composant du rig.
public static class Haptics
{
    public static void Pulse(Hand hand, float amplitude, float duration)
    {
        if (hand == Hand.Unknown)
        {
            Pulse(Hand.Left, amplitude, duration);
            Pulse(Hand.Right, amplitude, duration);
            return;
        }

        // Input System (manettes OpenXR Meta Touch)
        UnityEngine.InputSystem.XR.XRController controller = hand == Hand.Left
            ? UnityEngine.InputSystem.XR.XRController.leftHand
            : UnityEngine.InputSystem.XR.XRController.rightHand;
        if (controller is UnityEngine.InputSystem.XR.XRControllerWithRumble rumble)
        {
            rumble.SendImpulse(amplitude, duration);
            return;
        }

        // Repli : API XR historique
        UnityEngine.XR.InputDevice device = UnityEngine.XR.InputDevices.GetDeviceAtXRNode(
            hand == Hand.Left ? UnityEngine.XR.XRNode.LeftHand : UnityEngine.XR.XRNode.RightHand);
        if (device.isValid)
            device.SendHapticImpulse(0u, amplitude, duration);
    }

    // Déduit la main à partir de la hiérarchie du rig ("Left Controller", "Right Controller").
    public static Hand HandFromTransform(Transform t)
    {
        while (t != null)
        {
            string n = t.name;
            if (n.Contains("Left")) return Hand.Left;
            if (n.Contains("Right")) return Hand.Right;
            t = t.parent;
        }
        return Hand.Unknown;
    }
}
