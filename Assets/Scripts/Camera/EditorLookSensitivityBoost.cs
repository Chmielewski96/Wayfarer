using UnityEngine;
using Unity.Cinemachine;

namespace Wayfarer.CameraSystems
{
    /// <summary>
    /// Compensates for the fact that the Unity Editor's Game View does not perform a true
    /// OS-level pointer lock the way a real build does (WebGL, standalone, etc.). Because of
    /// that, the same physical mouse movement produces a much smaller raw delta value in-Editor
    /// than it does in a shipped build, which makes camera look sensitivity feel tiny when
    /// testing in-Editor even though it will feel correct (or even too strong) once built.
    ///
    /// This script multiplies the look-axis Gain on a CinemachineInputAxisController, but only
    /// while running inside the Editor (Application.isEditor). Builds are completely unaffected —
    /// the Gain values you see in the Inspector are what ships.
    ///
    /// The compensation multiplier is empirical and will need one real calibration pass: build a
    /// throwaway WebGL/standalone build, compare feel to the Editor, and adjust
    /// editorSensitivityMultiplier until the two roughly match. Until then this ships with a
    /// rough starting guess based on the gap observed on a previous project (Turtling: Editor
    /// Gain 140 vs WebGL Gain ~15-20, a ratio of roughly 7-9x).
    /// </summary>
    [RequireComponent(typeof(CinemachineInputAxisController))]
    public class EditorLookSensitivityBoost : MonoBehaviour
    {
        [Tooltip("Multiplies look-axis Gain only when running in the Editor. Does not affect builds. " +
                 "Tune this after doing a real test build to compare Editor feel vs build feel.")]
        [SerializeField] private float editorSensitivityMultiplier = 7f;

        [Tooltip("Names of axis controllers this should affect (must match the 'Name' field shown " +
                 "in the CinemachineInputAxisController inspector, e.g. 'Look Orbit X', 'Look Orbit Y'). " +
                 "Leave empty to affect all controllers except zoom/scale axes.")]
        [SerializeField] private string[] targetAxisNames = { "Look Orbit X", "Look Orbit Y" };

        private CinemachineInputAxisController _axisController;
        private readonly System.Collections.Generic.Dictionary<string, float> _originalGains = new();

        private void Awake()
        {
            _axisController = GetComponent<CinemachineInputAxisController>();

            if (!Application.isEditor)
            {
                // Builds ship with whatever Gain is set in the Inspector, untouched.
                return;
            }

            ApplyEditorBoost();
        }

        private void ApplyEditorBoost()
        {
            for (int i = 0; i < _axisController.Controllers.Count; i++)
            {
                var controller = _axisController.Controllers[i];

                if (!ShouldAffect(controller.Name)) continue;

                _originalGains[controller.Name] = controller.Input.Gain;
                controller.Input.Gain *= editorSensitivityMultiplier;
                _axisController.Controllers[i] = controller;
            }
        }

        private bool ShouldAffect(string axisName)
        {
            if (targetAxisNames == null || targetAxisNames.Length == 0) return true;

            foreach (var name in targetAxisNames)
            {
                if (name == axisName) return true;
            }

            return false;
        }
    }
}
