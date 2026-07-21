using UnityEngine;
using Unity.Cinemachine;

namespace Wayfarer.CameraSystems
{
    /// <summary>
    /// Inverts the vertical look axis when running on macOS (Editor or a built Mac player).
    /// Many Mac users expect inverted-Y camera look by convention/preference; this keeps that
    /// behavior automatic rather than something that has to be toggled per machine.
    ///
    /// If you'd rather this be a user-facing settings toggle instead of an automatic
    /// platform check, let me know and I'll wire it to a preference instead.
    /// </summary>
    [RequireComponent(typeof(CinemachineInputAxisController))]
    public class MacLookAxisInverter : MonoBehaviour
    {
        [Tooltip("Names of axis controllers to invert on Mac (must match the 'Name' field shown " +
                 "in the CinemachineInputAxisController inspector).")]
        [SerializeField] private string[] verticalAxisNames = { "Look Orbit Y" };

        private void Awake()
        {
            bool isMac = Application.platform == RuntimePlatform.OSXEditor ||
                         Application.platform == RuntimePlatform.OSXPlayer;

            if (!isMac) return;

            var axisController = GetComponent<CinemachineInputAxisController>();

            for (int i = 0; i < axisController.Controllers.Count; i++)
            {
                var controller = axisController.Controllers[i];
                if (!ShouldInvert(controller.Name)) continue;

                controller.Input.Gain *= -1f;
                axisController.Controllers[i] = controller;
            }
        }

        private bool ShouldInvert(string axisName)
        {
            foreach (var name in verticalAxisNames)
            {
                if (name == axisName) return true;
            }
            return false;
        }
    }
}
